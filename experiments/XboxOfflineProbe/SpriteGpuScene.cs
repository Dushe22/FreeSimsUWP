using System;
using System.Collections.Generic;
using System.Linq;
using FreeSims.Tests;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeSims.Xbox.Proof
{
    internal sealed class SpriteGpuScene : IDisposable
    {
        public readonly SpriteScene Source;
        private readonly List<Texture2D> colors = new List<Texture2D>();
        private readonly List<Texture2D> depths = new List<Texture2D>();
        public SpriteGpuScene(GraphicsDevice device, SpriteScene source)
        {
            Source=source;
            try {
                foreach(var layer in source.Layers) {
                    var color=new Texture2D(device,layer.Width,layer.Height,false,SurfaceFormat.Color);
                    colors.Add(color); color.SetData(layer.Pixels);
                    var depth=new Texture2D(device,layer.Width,layer.Height,false,SurfaceFormat.Alpha8);
                    depths.Add(depth); depth.SetData(layer.Depth);
                }
            } catch { Dispose(); throw; }
        }
        // Ground-position painter ordering is only for these isolated fixtures.
        // Uploaded depth is checked below, but full lot depth compositing is not enabled.
        public void Draw(SpriteBatch batch, Vector2 position, float scale)
        {
            for(int i=0;i<colors.Count;i++) batch.Draw(colors[i],
                position+(Source.Positions[i]-new Vector2(Source.Bounds.X,Source.Bounds.Y))*scale,
                null,Color.White,0,Vector2.Zero,scale,Source.Layers[i].Flip?SpriteEffects.FlipHorizontally:SpriteEffects.None,0);
        }
        public void CheckUpload()
        {
            for(int i=0;i<colors.Count;i++) {
                var layer=Source.Layers[i];
                var rgba=new Color[layer.Pixels.Length]; colors[i].GetData(rgba);
                var depth=new byte[layer.Depth.Length]; depths[i].GetData(depth);
                if(!rgba.SequenceEqual(layer.Pixels) || !depth.SequenceEqual(layer.Depth))
                    throw new InvalidOperationException("SPR2 GPU color/depth upload changed pixels.");
            }
        }
        public void CheckComposite(GraphicsDevice device, SpriteBatch batch)
        {
            var previous=device.GetRenderTargets(); var viewport=device.Viewport;
            int width=Source.Bounds.Width+8, height=Source.Bounds.Height+8;
            var background=new Color(30,45,60,255);
            using(var target=new RenderTarget2D(device,width,height,false,SurfaceFormat.Color,DepthFormat.None)) {
                try {
                    device.SetRenderTarget(target); device.Clear(background);
                    batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
                    Draw(batch,new Vector2(4,4),1); batch.End();
                    device.SetRenderTargets(previous); device.Viewport=viewport;
                    var actual=new Color[width*height]; target.GetData(actual);
                    var expected=Enumerable.Repeat(background,width*height).ToArray();
                    for(int i=0;i<Source.Layers.Count;i++) {
                        var layer=Source.Layers[i];
                        int ox=4+(int)Source.Positions[i].X-Source.Bounds.X;
                        int oy=4+(int)Source.Positions[i].Y-Source.Bounds.Y;
                        for(int y=0;y<layer.Height;y++) for(int x=0;x<layer.Width;x++) {
                            var src=layer.Pixels[y*layer.Width+(layer.Flip?layer.Width-1-x:x)];
                            int index=(oy+y)*width+ox+x; var dst=expected[index];
                            expected[index]=new Color(Over(src.R,dst.R,src.A),Over(src.G,dst.G,src.A),Over(src.B,dst.B,src.A),(byte)255);
                        }
                    }
                    for(int i=0;i<actual.Length;i++) if(Math.Abs(actual[i].R-expected[i].R)>2 || Math.Abs(actual[i].G-expected[i].G)>2 ||
                        Math.Abs(actual[i].B-expected[i].B)>2 || actual[i].A!=255)
                        throw new InvalidOperationException("Sprite composite mismatch at "+i+": GPU="+actual[i]+" CPU="+expected[i]);
                } finally { device.SetRenderTargets(previous); device.Viewport=viewport; }
            }
        }
        private static byte Over(byte source,byte destination,byte alpha) { return (byte)Math.Min(255,source+(destination*(255-alpha)+127)/255); }
        public void Dispose() { foreach(var t in colors) t.Dispose(); foreach(var t in depths) t.Dispose(); colors.Clear(); depths.Clear(); }
    }
}
