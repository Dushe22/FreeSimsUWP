using System;
using System.Collections.Generic;
using FSO.Client.Platform;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace FreeSims.Tests
{
    public static class ControllerPointerTests
    {
        public static List<string> Run(Action<string> log)
        {
            var results = new List<string>();
                CommonCompatibilityTests.Check(results, log, "POINTER DEAD ZONE", () => {
                    var p = new ControllerPointer(new Vector2(100,100));
                    p.Update(new Vector2(0.1f,0.1f), false, true, 0.1, 1280, 720);
                    CommonCompatibilityTests.Require(p.Position == new Vector2(100,100), "Pointer drifted in dead zone.");
                    p.Update(Vector2.UnitX, false, true, 0.1, 1280, 720);
                    CommonCompatibilityTests.Require(Math.Abs(p.Position.X - 160) < 0.01f, "Pointer speed incorrect.");
                });
                CommonCompatibilityTests.Check(results, log, "POINTER FOCUS AND CLAMP", () => {
                    var p = new ControllerPointer(new Vector2(1275,1));
                    p.Update(Vector2.One, false, true, 10, 1280, 720);
                    CommonCompatibilityTests.Require(p.Position.X == 1279 && p.Position.Y == 0, "Pointer escaped bounds.");
                    p.Update(Vector2.Zero, true, false, 0.1, 1280, 720);
                    CommonCompatibilityTests.Require(p.Update(Vector2.Zero, true, true, 0.1, 1280, 720).LeftButton == ButtonState.Released,
                        "Resume generated a held click.");
                    p.Update(Vector2.Zero, false, true, 0.1, 1280, 720);
                    CommonCompatibilityTests.Require(p.Update(Vector2.Zero, true, true, 0.1, 1280, 720).LeftButton == ButtonState.Pressed,
                        "Pointer did not rearm.");
                });
            return results;
        }
    }
}
