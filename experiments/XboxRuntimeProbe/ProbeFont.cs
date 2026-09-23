using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeSims.Xbox.Proof
{
    internal static class ProbeFont
    {
        // Rasterize the existing original grid glyphs into a MonoGame SpriteFont.
        public static SpriteFont Create(GraphicsDevice device)
        {
            const int count = 91 - 32;
            var glyphs = new List<Rectangle>();
            var crops = new List<Rectangle>();
            var chars = new List<char>();
            var kernings = new List<Vector3>();
            var data = new Color[count * 12 * 16];
            using (var target = new RenderTarget2D(device, count * 12, 16))
            using (var batch = new SpriteBatch(device))
            using (var pixel = new Texture2D(device,1,1))
            {
                pixel.SetData(new[] { Color.White });
                device.SetRenderTarget(target);
                device.Clear(Color.Transparent);
                batch.Begin(samplerState: SamplerState.PointClamp);
                for (int i = 0; i < count; i++)
                {
                    char c = (char)(i + 32);
                    PixelText.Draw(batch,pixel,c.ToString(),i*12,0,2,Color.White);
                    glyphs.Add(new Rectangle(i*12,0,10,14));
                    crops.Add(new Rectangle(0,0,10,14));
                    chars.Add(c);
                    kernings.Add(new Vector3(0,10,2));
                }
                batch.End();
                device.SetRenderTarget(null);
                target.GetData(data);
            }
            var texture = new Texture2D(device,count*12,16);
            texture.SetData(data);
            return new SpriteFont(texture,glyphs,crops,chars,16,0,kernings,'?');
        }
    }
}
