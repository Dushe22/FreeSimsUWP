using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Common.Platform;
using FSO.Common.Rendering.Framework;
using FSO.Common.Utils;
using FreeSims.Tests;
using LogThis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeSims.Xbox.Proof
{
    internal static class CommonNativeTests
    {
        public static void Run(GamePaths paths, GraphicsDevice device, List<string> results)
        {
            CommonCompatibilityTests.Check(results,ProofLog.Write,"PACKAGED CONTENT READ",() => {
                CommonCompatibilityTests.Require(File.ReadAllText(paths.GetContentPath("package-probe.txt")).Trim() ==
                    "FreeSims common package fixture v1","Packaged content unavailable.");
            });
            CommonCompatibilityTests.Check(results,ProofLog.Write,"UWP EVENT LOG FALLBACK",() => {
                Log.UseSensibleDefaults();
                Log.LogWhere = elogwhere.eventlog;
                try {
                    string marker = "NATIVE EVENT " + Guid.NewGuid().ToString("N");
                    Log.LogThis(marker,eloglevel.info);
                    CommonCompatibilityTests.Require(File.ReadAllText(paths.GetUserDataPath("Logs/events.log")).Contains(marker),
                        "Event fallback did not persist.");
                } finally { Log.LogWhere = elogwhere.file; }
            });
            CommonCompatibilityTests.Check(results,ProofLog.Write,"SHARED TARGET CLEAR",() => {
                using (var target = new RenderTarget2D(device,4,4))
                {
                    try { PPXDepthEngine.SetPPXTarget(target,null,true); }
                    finally { PPXDepthEngine.SetPPXTarget(null,null,false); }
                    var pixels = new Color[16];
                    target.GetData(pixels);
                    CommonCompatibilityTests.Require(pixels.All(x => x.PackedValue == 0),"Target clear is not transparent black.");
                }
            });
            CommonCompatibilityTests.Check(results,ProofLog.Write,"SHARED SCREEN LAYER FLOW",() => {
                var screen = new GameScreen(device);
                var layer = new ProbeLayer();
                screen.Add(layer);
                screen.Update(new GameTime(TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1.0/60)),false);
                screen.Draw(new GameTime());
                CommonCompatibilityTests.Require(layer.Initializes == 1 && layer.Updates == 1 &&
                    layer.PreDraws == 1 && layer.Draws == 1,"Shared screen callbacks failed.");
            });
        }
    }
}
