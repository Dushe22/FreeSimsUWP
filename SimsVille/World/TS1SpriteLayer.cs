using System;
using System.Collections.Generic;
using System.IO;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;

namespace FSO.LotView
{
    // CPU-owned SPR2 layers for the first TS1 renderer bring-up. No cached GPU resources
    // are borrowed from the IFF. Renderers own uploads and use WorldOffset for depth.
    public sealed class TS1SpriteLayer
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public Vector2 Offset { get; private set; }
        public bool Flip { get; private set; }
        public Vector3 WorldOffset { get; private set; }
        public Color[] Pixels { get; private set; }
        public byte[] Depth { get; private set; }
        public int DynamicIndex { get; private set; }
        public int Graphic { get; private set; }

        public static List<TS1SpriteLayer> Read(VMEntity entity, int zoom, int rotation)
        {
            return Read(entity, zoom, rotation, null);
        }

        // Cache only for one view. Repeated objects share immutable upload data;
        // callers of the public Read API still receive independent pixel arrays.
        internal static List<TS1SpriteLayer> Read(VMEntity entity, int zoom, int rotation,
            Dictionary<SPR2Frame, TS1SpriteLayer> frames)
        {
            if(entity==null)throw new ArgumentNullException("entity");
            return ReadState(entity,zoom,rotation,frames,entity.GetValue(VMStackObjectVariable.Graphic),false);
        }
        internal static List<TS1SpriteLayer> ReadState(VMEntity entity,int zoom,int rotation,
            Dictionary<SPR2Frame,TS1SpriteLayer> frames,int graphicOffset,bool allDynamic)
        {
            if (entity == null) throw new ArgumentNullException("entity");
            ValidateView(zoom, rotation);
            uint direction = (uint)entity.Direction;
            if (direction != 1 && direction != 4 && direction != 16 && direction != 64)
                throw new NotSupportedException("Only cardinal object directions are supported.");
            var definition = entity.Object.OBJ;
            int graphic = definition.BaseGraphicID + graphicOffset;
            if (graphic <= 0 || graphic > ushort.MaxValue) throw new InvalidDataException("Invalid object graphic.");
            var group = entity.Object.Resource.Get<DGRP>((ushort)graphic);
            var view = group == null ? null : group.GetImage(direction, (uint)zoom, (uint)rotation);
            if (view == null || view.Sprites == null) throw new InvalidDataException("Missing DGRP view.");
            var result = new List<TS1SpriteLayer>();
            foreach (var sprite in view.Sprites) {
                if (sprite == null || sprite.SpriteID > ushort.MaxValue) throw new InvalidDataException("Invalid sprite reference.");
                int dynamicIndex = (int)sprite.SpriteID - definition.DynamicSpriteBaseId;
                if (dynamicIndex >= 0 && dynamicIndex < definition.NumDynamicSprites) {
                    if (dynamicIndex >= 128) throw new NotSupportedException("Dynamic sprite index exceeds VM flags.");
                    if (!allDynamic && !entity.IsDynamicSpriteFlagSet((ushort)dynamicIndex)) continue;
                }
                var resource = entity.Object.Resource.Get<SPR2>((ushort)sprite.SpriteID);
                if (resource == null) throw new NotSupportedException("This renderer requires SPR2.");
                if (resource.Frames == null || sprite.SpriteFrameIndex >= resource.Frames.Length)
                    throw new InvalidDataException("Missing SPR2 frame.");
                var frame = resource.Frames[sprite.SpriteFrameIndex];
                try {frame.DecodeIfRequired();}
                catch(EndOfStreamException error) {
                    throw new InvalidDataException("Truncated SPR2: resource="+entity.Object.Resource.Name+" graphic="+graphicOffset+
                        " sprite="+sprite.SpriteID+" frame="+sprite.SpriteFrameIndex+" size="+frame.Width+"x"+frame.Height+" zoom="+zoom+" rotation="+rotation,error);
                }
                int count = checked(frame.Width * frame.Height);
                if (frame.Width == 0 && frame.Height == 0) continue; // valid empty animation frame
                if (frame.Width <= 0 || frame.Height <= 0 || frame.PixelData == null || frame.PixelData.Length != count ||
                    frame.ZBufferData == null || frame.ZBufferData.Length != count)
                    throw new InvalidDataException("Missing SPR2 color/depth pixels: sprite="+sprite.SpriteID+" frame="+sprite.SpriteFrameIndex+" size="+frame.Width+"x"+frame.Height+" flags="+frame.Flags);
                TS1SpriteLayer shared;
                if (frames == null || !frames.TryGetValue(frame, out shared)) {
                    if(allDynamic&&frames!=null) {
                        long bytes=(long)count*5;
                        foreach(var cached in frames.Values)bytes=checked(bytes+(long)cached.Pixels.Length*5);
                        if(bytes>TS1LotRenderData.LivePixelBudget)throw new InvalidDataException("Live frame pixel budget exceeded before allocation.");
                    }
                    var pixels = new Color[count];
                    for (int i = 0; i < count; i++) pixels[i] = Premultiply(frame.PixelData[i]);
                    shared = new TS1SpriteLayer {Pixels = pixels, Depth = (byte[])frame.ZBufferData.Clone()};
                    if (frames != null) frames.Add(frame, shared);
                }
                float angle = direction == 4 ? MathHelper.PiOver2 : direction == 16 ? MathHelper.Pi : direction == 64 ? MathHelper.Pi * 1.5f : 0;
                var relative = Vector3.Transform(sprite.ObjectOffset * new Vector3(1/16f, 1/16f, 1/5f), Matrix.CreateRotationZ(angle));
                var offset = sprite.SpriteOffset + Project(relative, zoom, rotation);
                offset.Y -= frame.Height; // shared ground anchor, excluding the legacy render-target padding
                result.Add(new TS1SpriteLayer {
                    Width = frame.Width, Height = frame.Height, Offset = new Vector2((int)offset.X, (int)offset.Y),
                    WorldOffset = relative, Flip = sprite.Flip, Pixels = shared.Pixels, Depth = shared.Depth,
                    Graphic = graphicOffset, DynamicIndex = dynamicIndex >= 0 && dynamicIndex < definition.NumDynamicSprites ? dynamicIndex : -1
                });
            }
            return result;
        }

        public static Color Premultiply(Color value)
        {
            return new Color((byte)((value.R * value.A + 127) / 255), (byte)((value.G * value.A + 127) / 255),
                (byte)((value.B * value.A + 127) / 255), value.A);
        }
        private static void ValidateView(int zoom, int rotation)
        {
            if (zoom < 1 || zoom > 3) throw new ArgumentOutOfRangeException("zoom");
            if (rotation < 0 || rotation > 3) throw new ArgumentOutOfRangeException("rotation");
        }
        public static Vector2 Project(Vector3 tile, int zoom, int rotation)
        {
            var point = ProjectWithDepth(tile, zoom, rotation);
            return new Vector2(point.X, point.Y);
        }

        // Shared camera coordinates for sprites and future terrain/wall vertices.
        // Z is camera-space nearness (larger is nearer), independent of zoom.
        // It is NOT a normalized GPU depth value or a decoded SPR2 depth sample.
        public static Vector3 ProjectWithDepth(Vector3 tile, int zoom, int rotation)
        {
            ValidateView(zoom, rotation);
            float halfWidth = 16 * (1 << (zoom - 1));
            float x = tile.X, y = tile.Y;
            switch (rotation) {
                case 1: x = -tile.Y; y = tile.X; break;
                case 2: x = -tile.X; y = -tile.Y; break;
                case 3: x = tile.Y; y = -tile.X; break;
            }
            // Camera elevation is 30 degrees: orthogonal screen-up and view axes.
            float nearness = (x+y)*(float)Math.Sqrt(3.0/8.0) + tile.Z/2;
            return new Vector3((x-y)*halfWidth,
                (x+y)*halfWidth/2 - tile.Z*halfWidth*(float)Math.Sqrt(1.5), nearness);
        }
    }
}
