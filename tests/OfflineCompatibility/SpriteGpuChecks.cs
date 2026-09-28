using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FreeSims.Xbox.Proof;
using FSO.Common.Platform;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XnaColor=Microsoft.Xna.Framework.Color;

namespace FreeSims.Tests
{
    internal static class SpriteGpuChecks
    {
        public static int Run(string gameRoot,string scratchRoot)
        {
            try {
                Directory.CreateDirectory(scratchRoot);
                using(var control=new Control())
                using(var device=new GraphicsDevice(GraphicsAdapter.DefaultAdapter,GraphicsProfile.HiDef,
                    new PresentationParameters {BackBufferWidth=512,BackBufferHeight=512,DeviceWindowHandle=control.Handle,IsFullScreen=false}))
                using(var batch=new SpriteBatch(device))
                using(var fixture=new TS1SpriteFixture(new GamePaths(Path.Combine(scratchRoot,"Content"),gameRoot,Path.Combine(scratchRoot,"UserData")))) {
                    for(int o=0;o<2;o++) for(int z=1;z<=3;z++) for(int r=0;r<4;r++) {
                        using(var scene=new SpriteGpuScene(device,fixture.Read(o==1,z,r))) {scene.CheckUpload();scene.CheckComposite(device,batch);}
                        Console.WriteLine("PASS GPU object="+o+" zoom="+z+" rotation="+r);
                    }
                    using(var target=new RenderTarget2D(device,1280,600)) {
                        device.SetRenderTarget(target); device.Clear(new XnaColor(55,68,83));
                        for(int o=0;o<2;o++) for(int r=0;r<4;r++) using(var scene=new SpriteGpuScene(device,fixture.Read(o==1,3,r))) {
                            var bounds=scene.Source.Bounds;
                            float scale=Math.Min(2f,Math.Min(300f/bounds.Width,270f/bounds.Height));
                            batch.Begin(blendState:BlendState.AlphaBlend,samplerState:SamplerState.PointClamp);
                            scene.Draw(batch,new Vector2(r*320+160-bounds.Width*scale/2,o*300+150-bounds.Height*scale/2),scale);batch.End();
                        }
                        device.SetRenderTarget(null);
                        var pixels=new XnaColor[1280*600];target.GetData(pixels);
                        using(var bitmap=new Bitmap(1280,600)) {
                            for(int y=0;y<600;y++) for(int x=0;x<1280;x++) {var c=pixels[y*1280+x];bitmap.SetPixel(x,y,System.Drawing.Color.FromArgb(c.A,c.R,c.G,c.B));}
                            bitmap.Save(Path.Combine(scratchRoot,"sprite-views.png"),System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                }
                return 0;
            } catch(Exception ex) {Console.WriteLine("FAIL GPU "+ex);return 1;}
        }
    }
}
