using System;
using FreeSims.Xbox.Proof;
using Microsoft.Xna.Framework;

namespace FreeSims.Tests
{
    internal static class ProbeLayoutTests
    {
        public static bool Run(Action<string> log)
        {
            try
            {
                // A -> B -> A checks that a resume resize cannot retain stale scale.
                foreach (var size in new[] { new Point(1280,720), new Point(1920,1080),
                    new Point(3840,2160), new Point(1280,720) })
                {
                    Matrix matrix;
                    if (!ProbeLayout.TryCreateTransform(size.X, size.Y, out matrix))
                        throw new Exception("Valid viewport rejected.");
                    var title = Vector2.Transform(new Vector2(48,36), matrix);
                    Near(title.X / size.X, 48f / 1280);
                    Near(title.Y / size.Y, 36f / 720);
                    var bottomRight = Vector2.Transform(new Vector2(1280,720), matrix);
                    Near(bottomRight.X, size.X); Near(bottomRight.Y, size.Y);
                }
                // Non-16:9 displays must preserve aspect ratio and center the canvas.
                CheckFit(1024,768, new Vector2(0,96), new Vector2(1024,672));
                CheckFit(2560,1080, new Vector2(320,0), new Vector2(2240,1080));
                Matrix ignored;
                if (ProbeLayout.TryCreateTransform(0,720,out ignored) ||
                    ProbeLayout.TryCreateTransform(1280,0,out ignored) ||
                    ProbeLayout.TryCreateTransform(-1,720,out ignored))
                    throw new Exception("Non-drawable viewport accepted.");
                // Recover after a zero-size transition.
                CheckFit(1920,1080, Vector2.Zero, new Vector2(1920,1080));
                log("PASS PROBE LAYOUT - RESIZE SEQUENCE, ASPECT FIT, ZERO-SIZE RECOVERY");
                return true;
            }
            catch (Exception ex) { log("FAIL PROBE LAYOUT " + ex); return false; }
        }

        private static void CheckFit(int width, int height, Vector2 start, Vector2 end)
        {
            Matrix matrix;
            if (!ProbeLayout.TryCreateTransform(width,height,out matrix))
                throw new Exception("Valid viewport rejected.");
            var actualStart = Vector2.Transform(Vector2.Zero,matrix);
            var actualEnd = Vector2.Transform(new Vector2(1280,720),matrix);
            Near(actualStart.X,start.X); Near(actualStart.Y,start.Y);
            Near(actualEnd.X,end.X); Near(actualEnd.Y,end.Y);
            Near(matrix.M11,matrix.M22);
        }

        private static void Near(float actual, float expected)
        {
            if (Math.Abs(actual-expected) > 0.001f)
                throw new Exception("Expected " + expected + ", got " + actual);
        }
    }
}