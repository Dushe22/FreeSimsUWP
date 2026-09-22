using System;
using FSO.Common.Rendering.Framework;
using FSO.Common.Rendering.Framework.Model;
using Microsoft.Xna.Framework.Graphics;

namespace FreeSims.Xbox.Proof
{
    internal sealed class ProbeLayer : IGraphicsLayer
    {
        private readonly Action<GraphicsDevice> draw;
        public int Initializes, Updates, PreDraws, Draws;
        public double Elapsed;
        public ProbeLayer(Action<GraphicsDevice> draw = null) { this.draw = draw; }
        public void Initialize(GraphicsDevice device) { Initializes++; }
        public void Update(UpdateState state) { Updates++; Elapsed = state.Time.TotalGameTime.TotalSeconds; }
        public void PreDraw(GraphicsDevice device) { PreDraws++; }
        public void Draw(GraphicsDevice device) { Draws++; if (draw != null) draw(device); }
    }
}
