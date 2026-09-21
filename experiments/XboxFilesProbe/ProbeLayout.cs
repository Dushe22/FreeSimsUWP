using System;
using Microsoft.Xna.Framework;

namespace FreeSims.Xbox.Proof
{
    // Layout coordinates are independent of UWP's current presentation size.
    internal static class ProbeLayout
    {
        public const int Width = 1280;
        public const int Height = 720;

        public static bool TryCreateTransform(int viewportWidth, int viewportHeight, out Matrix transform)
        {
            transform = Matrix.Identity;
            // A window can have no drawable area while minimized/suspended.
            if (viewportWidth <= 0 || viewportHeight <= 0) return false;
            float scale = Math.Min(viewportWidth / (float)Width, viewportHeight / (float)Height);
            transform = Matrix.CreateScale(scale, scale, 1f) * Matrix.CreateTranslation(
                (viewportWidth - Width * scale) / 2f,
                (viewportHeight - Height * scale) / 2f, 0f);
            return true;
        }
    }
}
