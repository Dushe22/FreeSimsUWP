using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FSO.LotView
{
    public sealed class TS1LotRenderer : IDisposable
    {
        private sealed class Item
        {
            public Texture2D Color,Depth; public VertexPositionColorTexture[] Vertices;
            public float Nearness; public short ID; public bool HasOpaque, HasAlpha, Hidden;
            public string[] WallHosts; public ushort LightRoom;
            // Textures are owned once by the renderer, not by individual instances.
        }
        private sealed class MaterialItem
        {
            public Texture2D Texture; public VertexPositionColorTexture[] Vertices, Active; public bool Wall, Roof;
            public TS1LotRenderData.Surface Source;
        }
        private readonly List<MaterialItem> materialItems=new List<MaterialItem>();
        private readonly GraphicsDevice device;
        private readonly Dictionary<TS1LotRenderData.Surface,MaterialItem> wallItems=new Dictionary<TS1LotRenderData.Surface,MaterialItem>();
        private VertexPositionColor[] caps;
        private int capCount, preparedRevision=int.MinValue;
        // Reused scratch state; pointer/mode changes allocate no GPU resources.
        private readonly HashSet<string> cutKeys=new HashSet<string>();
        private readonly Dictionary<string,int> fullEnds=new Dictionary<string,int>(), allEnds=new Dictionary<string,int>();
        public int CutWallCount {get;private set;}
        public int HiddenAttachmentCount {get;private set;}
        public int WallCapVertexCount {get {return capCount;}}
        // At most 4 MiB of colored GPU-batch vertices per scene.
        public const int WallGeometryVertexBudget=262144;
        public int WallGeometryCapacity {get {return caps==null?0:caps.Length;}}
        private readonly Color[] roomColors;
        private bool lightingEnabled;
        private Color outsideColor=Color.White;
        public double LightingHour {get;private set;}
        public int LightingRevision {get;private set;}
        public Color OutsideColor {get {return outsideColor;}}
        private Color RoomColor(ushort room) {
            return !lightingEnabled||room==TS1LotRenderData.EmissiveLightRoom?Color.White:room<roomColors.Length?roomColors[room]:outsideColor;
        }
        private static Color Tint(Color original,Color light) {
            return new Color(original.R*light.R/255,original.G*light.G/255,original.B*light.B/255,(int)original.A);
        }
        // Relight cached vertices, never pixels/textures, SPR2 depth or saved state.
        // Return false when quantized palette colors are unchanged (including midday).
        public bool UpdateLighting(double hour,bool enabled=true)
        {
            if(double.IsNaN(hour)||double.IsInfinity(hour))throw new ArgumentOutOfRangeException("hour");
            LightingHour=hour-Math.Floor(hour/24)*24;
            var outside=enabled?FSO.SimAntics.VMArchitecture.OutsideLightAt(LightingHour/24):Color.White;
            if(lightingEnabled==enabled&&outside==outsideColor)return false;
            lightingEnabled=enabled;outsideColor=outside;LightingRevision++;
            for(int i=0;i<roomColors.Length;i++)
                roomColors[i]=i<Data.Lighting.Length?Data.Lighting[i].ColorAt(outside,Data.OutsideRooms[i]):outside;
            foreach(var item in materialItems)for(int i=0;i<item.Vertices.Length;i++)
                item.Vertices[i].Color=Tint(item.Source.Vertices[i].Color,RoomColor(
                    i<item.Source.LightRooms.Count?item.Source.LightRooms[i]:(ushort)0));

            foreach(var item in items)for(int i=0;i<item.Vertices.Length;i++)item.Vertices[i].Color=RoomColor(item.LightRoom);
            for(int i=0;i<ground.Length;i++)ground[i].Color=Tint(Data.Ground[i].Color,outside);
            for(int i=0;i<walls.Length;i++)walls[i].Color=Tint(Data.Walls[i].Color,outside);
            preparedRevision=int.MinValue;
            return true;
        }
        private readonly BasicEffect surfaces;
        private readonly AlphaTestEffect materials;
        private readonly Effect sprites;
        private readonly List<Item> items=new List<Item>();
        private Item[] alphaItems;
        private readonly List<Texture2D> textures=new List<Texture2D>();
        public int TextureCount {get {return textures.Count;}}
        public long TextureBytes {get; private set;}
        private readonly VertexPositionColor[] ground,walls;
        public readonly TS1LotRenderData.View Data;
        public TS1LotRenderer(GraphicsDevice device,TS1LotRenderData.View data,byte[] effect)
        {
            this.device=device;Data=data;
            if(data.Lighting.Length!=data.OutsideRooms.Length)throw new ArgumentException("Mismatched room lighting snapshot.");
            roomColors=new Color[Math.Max(1,data.Lighting.Length)];
            try {
                surfaces=new BasicEffect(device) {VertexColorEnabled=true};sprites=new Effect(device,effect);
                materials=new AlphaTestEffect(device) {VertexColorEnabled=true,ReferenceAlpha=128,AlphaFunction=CompareFunction.GreaterEqual};
                ground=data.Ground.ToArray();walls=data.Walls.ToArray();
                var colors=new Dictionary<Color[],Texture2D>();
                var depths=new Dictionary<byte[],Texture2D>();
                var alphaKinds=new Dictionary<Color[],int>();
                Func<int,int,Color[],Texture2D> colorTexture=(width,height,pixels)=> {
                    Texture2D texture;
                    if(!colors.TryGetValue(pixels,out texture)) {
                        texture=new Texture2D(device,width,height);textures.Add(texture);
                        texture.SetData(pixels);colors.Add(pixels,texture);TextureBytes+=(long)width*height*4;
                    }
                    return texture;
                };
                foreach(var source in data.TerrainMaterials.Concat(data.FloorMaterials).Concat(data.WallMaterials).Concat(data.RoofMaterials)) {
                    var item=new MaterialItem {Wall=data.WallMaterials.Contains(source) && !source.KeepWhenWallsHidden,Roof=data.RoofMaterials.Contains(source),Source=source,Vertices=source.Vertices.ToArray()};materialItems.Add(item);
                    if(item.Wall){item.Active=new VertexPositionColorTexture[item.Vertices.Length];wallItems.Add(source,item);}
                    item.Texture=colorTexture(source.Material.Width,source.Material.Height,source.Material.Pixels);
                }
                foreach(var source in data.Sprites) {
                    var layer=source.Layer;var item=new Item {Nearness=source.BackNearness,ID=source.ObjectID,WallHosts=source.WallHosts,LightRoom=source.LightRoom};items.Add(item);
                    item.Color=colorTexture(layer.Width,layer.Height,layer.Pixels);
                    Texture2D depth;
                    if(!depths.TryGetValue(layer.Depth,out depth)) {
                        depth=new Texture2D(device,layer.Width,layer.Height,false,SurfaceFormat.Alpha8);textures.Add(depth);
                        depth.SetData(layer.Depth);depths.Add(layer.Depth,depth);TextureBytes+=(long)layer.Width*layer.Height;
                    }
                    item.Depth=depth;
                    int kind;
                    if(!alphaKinds.TryGetValue(layer.Pixels,out kind)) {
                        kind=0;foreach(var pixel in layer.Pixels){if(pixel.A==255)kind|=1;else if(pixel.A>0)kind|=2;if(kind==3)break;}
                        alphaKinds.Add(layer.Pixels,kind);
                    }
                    item.HasOpaque=(kind&1)!=0;item.HasAlpha=(kind&2)!=0;
                    float l=layer.Flip?1:0,r=1-l;var p=source.Position;float z=source.BackNearness;
                    var a=new VertexPositionColorTexture(new Vector3(p,z),Color.White,new Vector2(l,0));
                    var b=new VertexPositionColorTexture(new Vector3(p.X+layer.Width,p.Y,z),Color.White,new Vector2(r,0));
                    var c=new VertexPositionColorTexture(new Vector3(p.X+layer.Width,p.Y+layer.Height,z),Color.White,new Vector2(r,1));
                    var d=new VertexPositionColorTexture(new Vector3(p.X,p.Y+layer.Height,z),Color.White,new Vector2(l,1));
                    item.Vertices=new[]{a,b,c,a,c,d};
                }
                int capCapacity=0;
                foreach(var section in data.WallSections) {
                    capCapacity=checked(capCapacity+Math.Max(section.Top.Length,section.LowTop.Length)+
                        Math.Max(section.EndFaceA.Length,section.LowEndA.Length)+Math.Max(section.EndFaceB.Length,section.LowEndB.Length)+
                        Math.Max(section.Reveals.Length,section.LowReveals.Length));
                    CountEnd(allEnds,section.EndA);CountEnd(allEnds,section.EndB);
                }
                if(capCapacity>WallGeometryVertexBudget)throw new InvalidOperationException("Wall thickness exceeds the scene geometry budget.");
                caps=new VertexPositionColor[capCapacity];
                alphaItems=items.Where(i=>i.HasAlpha).OrderBy(i=>i.Nearness).ThenBy(i=>i.ID).ToArray();
            } catch {Dispose();throw;}
        }
        // Keep the original bool overload for legacy rendering and its pixel regressions.
        public void Draw(Matrix projection,bool showWalls,bool reverseOpaque=false)
        {
            DrawCore(projection,showWalls,reverseOpaque,false);
        }
        public void Draw(Matrix projection,TS1WallMode mode,bool reverseOpaque=false)
        {
            if(Data.WallMode!=mode)throw new InvalidOperationException("Update wall visibility before drawing.");
            PrepareWalls();
            DrawCore(projection,true,reverseOpaque,true);
        }
        private void PrepareWalls()
        {
            if(preparedRevision==Data.WallRevision)return;
            preparedRevision=Data.WallRevision;CutWallCount=0;HiddenAttachmentCount=0;capCount=0;
            cutKeys.Clear();fullEnds.Clear();
            foreach(var section in Data.WallSections) {
                if(section.Cut){cutKeys.Add(section.Key);CutWallCount++;}
                else {CountEnd(fullEnds,section.EndA);CountEnd(fullEnds,section.EndB);}
            }
            foreach(var pair in wallItems){Array.Copy(pair.Value.Vertices,pair.Value.Active,pair.Value.Vertices.Length);}
            foreach(var section in Data.WallSections) {
                if(section.Cut){
                    var active=wallItems[section.Surface].Active;
                    for(int i=0;i<6;i++){
                        active[section.Offset+i]=section.Low[i];
                        active[section.Offset+i].Color=Tint(section.Low[i].Color,RoomColor(section.LightRoom));
                    }
                }
                AddCap(section.LightRoom,section.Cut?section.LowTop:section.Top);
                AddCap(section.LightRoom,section.Cut?section.LowReveals:section.Reveals);
                // Only expose full ends at a physical end or a transition to low walls.
                if(section.Cut?allEnds[section.EndA]==1:fullEnds[section.EndA]==1)
                    AddCap(section.LightRoom,section.Cut?section.LowEndA:section.EndFaceA);
                if(section.Cut?allEnds[section.EndB]==1:fullEnds[section.EndB]==1)
                    AddCap(section.LightRoom,section.Cut?section.LowEndB:section.EndFaceB);
            }
            foreach(var item in items) {
                item.Hidden=item.WallHosts!=null&&item.WallHosts.Length>0;
                if(item.Hidden)foreach(var host in item.WallHosts)if(!cutKeys.Contains(host)){item.Hidden=false;break;}
                if(item.Hidden)HiddenAttachmentCount++;
            }
        }
        private static void CountEnd(Dictionary<string,int> map,string key)
        {
            int count;map.TryGetValue(key,out count);map[key]=count+1;
        }
        private void AddCap(ushort room,VertexPositionColor[] vertices)
        {
            var light=RoomColor(room);
            for(int i=0;i<vertices.Length;i++){
                caps[capCount]=vertices[i];caps[capCount].Color=Tint(vertices[i].Color,light);capCount++;
            }
        }
        private void DrawCore(Matrix projection,bool showWalls,bool reverseOpaque,bool dynamicWalls)
        {
            device.RasterizerState=RasterizerState.CullNone;device.DepthStencilState=DepthStencilState.Default;device.BlendState=BlendState.Opaque;
            surfaces.World=Matrix.Identity;surfaces.View=Matrix.Identity;surfaces.Projection=projection;
            surfaces.TextureEnabled=false;
            foreach(var pass in surfaces.CurrentTechnique.Passes) {pass.Apply();DrawSurface(ground);if(showWalls)DrawSurface(walls);if(dynamicWalls)DrawSurface(caps,capCount);}
            materials.World=Matrix.Identity;materials.View=Matrix.Identity;materials.Projection=projection;device.SamplerStates[0]=SamplerState.PointClamp;
            foreach(var item in materialItems) {
                if(item.Wall&&!showWalls)continue;
                device.SamplerStates[0]=item.Roof?SamplerState.PointWrap:SamplerState.PointClamp;
                materials.Texture=item.Texture;
                var vertices=dynamicWalls&&item.Wall?item.Active:item.Vertices;
                foreach(var pass in materials.CurrentTechnique.Passes) {pass.Apply();for(int offset=0;offset<vertices.Length;offset+=18000)device.DrawUserPrimitives(PrimitiveType.TriangleList,vertices,offset,Math.Min(18000,vertices.Length-offset)/3);}
            }
            sprites.Parameters["Projection"].SetValue(projection);
            // Same depth calibration as 2DWorldBatch.fx: a two-tile diagonal span
            // multiplied by (1 - SPR2 depth/255) / 0.4. Projection maps 256 units to [0,1].
            sprites.Parameters["DepthSpan"].SetValue((float)Math.Sqrt(1.5)/0.4f/256f);
            sprites.Parameters["AlphaPass"].SetValue(0f);
            var opaque=reverseOpaque?items.AsEnumerable().Reverse():items;
            foreach(var item in opaque) if(item.HasOpaque&&(!dynamicWalls||!item.Hidden))DrawItem(item);
            // Opaque texels write depth; partial alpha edges only read it, avoiding
            // invisible depth writes. Intersecting translucent surfaces are not OIT.
            device.BlendState=BlendState.AlphaBlend;device.DepthStencilState=DepthStencilState.DepthRead;
            sprites.Parameters["AlphaPass"].SetValue(1f);
            foreach(var item in alphaItems) if(!dynamicWalls||!item.Hidden)DrawItem(item);
        }
        private void DrawSurface(VertexPositionColor[] vertices) {DrawSurface(vertices,vertices.Length);}
        private void DrawSurface(VertexPositionColor[] vertices,int count)
        {
            // Bounded batches avoid primitive-count limits on large lots.
            for(int offset=0;offset<count;offset+=18000) device.DrawUserPrimitives(PrimitiveType.TriangleList,vertices,offset,Math.Min(18000,count-offset)/3);
        }
        private void DrawItem(Item item)
        {
            sprites.Parameters["ColorTexture"].SetValue(item.Color);sprites.Parameters["DepthTexture"].SetValue(item.Depth);
            foreach(var pass in sprites.CurrentTechnique.Passes) {pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,item.Vertices,0,2);}
        }
        public static Matrix Camera(int size,int zoom,int rotation,int width,int height,Vector2 pan)
        {
            float scale=Math.Min((width-40f)/(size*32),(height-60f)/(size*16+90));
            var center=TS1SpriteLayer.Project(new Vector3(size/2f,size/2f,0),zoom,rotation);
            return Matrix.CreateScale(scale,scale,1)*Matrix.CreateTranslation(width/2f-center.X*scale+pan.X,height/2f-center.Y*scale+pan.Y,0)*
                Matrix.CreateOrthographicOffCenter(0,width,height,0,-128,128);
        }
        public void Dispose() {foreach(var texture in textures)texture.Dispose();textures.Clear();materialItems.Clear();wallItems.Clear();items.Clear();cutKeys.Clear();fullEnds.Clear();allEnds.Clear();alphaItems=null;caps=null;if(surfaces!=null)surfaces.Dispose();if(materials!=null)materials.Dispose();if(sprites!=null)sprites.Dispose();}
    }
}
