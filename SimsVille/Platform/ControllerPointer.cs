using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace FSO.Client.Platform
{
    /// <summary>Controller pointer in logical UI coordinates; no desktop cursor APIs.</summary>
    public sealed class ControllerPointer
    {
        public Vector2 Position { get; private set; }
        public float Speed { get; set; }
        public float DeadZone { get; set; }
        private bool waitForRelease = true;

        public ControllerPointer(Vector2 position)
        {
            Position = position;
            Speed = 600;
            DeadZone = 0.2f;
        }

        public void Cancel() { waitForRelease = true; }

        public MouseState Update(Vector2 stick, bool select, bool active, double elapsed, int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("width");
            if (!active) waitForRelease = true;
            else if (!select) waitForRelease = false;
            if (active)
            {
                float magnitude = stick.Length();
                float deadZone = MathHelper.Clamp(DeadZone, 0, 0.95f);
                if (magnitude > deadZone)
                {
                    var direction = stick / magnitude;
                    float amount = (Math.Min(1, magnitude) - deadZone) / (1 - deadZone);
                    float seconds = (float)Math.Max(0, Math.Min(0.1, elapsed));
                    Position += new Vector2(direction.X, -direction.Y) * amount * Math.Max(0, Speed) * seconds;
                }
            }
            Position = new Vector2(MathHelper.Clamp(Position.X, 0, width - 1), MathHelper.Clamp(Position.Y, 0, height - 1));
            return new MouseState((int)Position.X, (int)Position.Y, 0,
                active && select && !waitForRelease ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        }
    }
}
