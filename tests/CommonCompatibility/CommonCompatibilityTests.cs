using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FSO.Common;
using FSO.Common.Content;
using FSO.Common.Platform;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using LogThis;
using TSO.Common.utils;

namespace FreeSims.Tests
{
    public static class CommonCompatibilityTests
    {
        public static List<string> Run(GamePaths paths, Action<string> log)
        {
            var results = new List<string>();
            string scratch = paths.GetUserDataPath("Compatibility");
            Directory.CreateDirectory(scratch);
            Check(results,log,"CONTENT ID ROUNDTRIP",() => {
                const ulong packed = 0xfedcba9876543210;
                var id = new ContentID(unchecked((long)packed));
                Require(id.TypeID == 0x76543210 && id.FileID == 0xfedcba98 && id.Shift() == packed,"Content ID layout changed.");
            });
            Check(results,log,"CURVE INTERPOLATION",() => {
                var curve = new TS1Curve("(10;100) (0;0) (20;50)");
                Require(curve.GetPoint(5) == 50 && curve.GetPoint(-1) == 0 &&
                    curve.GetPoint(30) == 50,"Curve interpolation/clamping changed.");
            });
            Check(results,log,"SHARED TEXT INPUT",() => {
                var state = new UpdateState { FrameTextInput = new List<char> { 'H','i' } };
                var text = new StringBuilder();
                var result = new InputManager().ApplyKeyboardInput(text,state,0,-1,true);
                Require(text.ToString() == "Hi" && result.NumInsertions == 2,"Shared text input failed.");
            });
            Check(results,log,"THREE ROOTS AND ENVIRONMENT",() => {
                string content = FSOEnvironment.ContentDir, game = FSOEnvironment.SimsCompleteDir,
                    user = FSOEnvironment.UserDir, gfx = FSOEnvironment.GFXContentDir;
                bool dx = FSOEnvironment.DirectX;
                try {
                    paths.ApplyToEnvironment(true);
                    Require(FSOEnvironment.DirectX && FSOEnvironment.ContentDir == paths.ContentRoot &&
                        FSOEnvironment.UserDir == paths.UserDataRoot && FSOEnvironment.SimsCompleteDir == paths.GameDataRoot &&
                        FSOEnvironment.GFXContentDir.StartsWith(paths.ContentRoot),"Environment mapping failed.");
                } finally {
                    FSOEnvironment.ContentDir=content; FSOEnvironment.SimsCompleteDir=game;
                    FSOEnvironment.UserDir=user; FSOEnvironment.GFXContentDir=gfx; FSOEnvironment.DirectX=dx;
                }
            });
            Check(results,log,"PATH BOUNDARIES",() => {
                foreach (string invalid in new[] { "../GameData/file", "..\\GameData\\file",
                    paths.ContentRoot, "C:relative", "settings.ini:stream" })
                    Reject(() => paths.GetUserDataPath(invalid));
                Reject(() => new GamePaths(paths.ContentRoot,paths.GameDataRoot,paths.GameDataRoot));
                Reject(() => new GamePaths(paths.ContentRoot,paths.GameDataRoot,Path.Combine(paths.GameDataRoot,"nested")));
                Reject(() => new GamePaths("relative",paths.GameDataRoot,paths.UserDataRoot));
                Require(paths.GetUserDataPath("Compatibility/check.ini") ==
                    Path.GetFullPath(Path.Combine(scratch,"check.ini")),"Valid path resolution failed.");
            });
            Check(results,log,"INI CREATE WRITE RELOAD",() => {
                string path = Path.Combine(scratch,"roundtrip.ini");
                if (File.Exists(path)) File.Delete(path); // Only this probe-owned fixture.
                var settings = new ProbeSettings(path);
                Require(File.Exists(path),"Settings creation was silently lost.");
                settings.Store(123,"original fixture");
                var reloaded = new ProbeSettings(path);
                Require(reloaded.Counter == 123 && reloaded.Marker == "original fixture" && reloaded.Enabled,
                    "Settings serialization/reflection failed.");
            });
            Check(results,log,"INI BLANK LINES",() => {
                string path = Path.Combine(scratch,"blank-lines.ini");
                File.WriteAllLines(path,new[] { "", "  ", "# comment", "[Probe]", "Counter=7", "Marker=synthetic", "Enabled=true" });
                var settings = new ProbeSettings(path);
                Require(settings.Counter == 7 && settings.Marker == "synthetic" && settings.Enabled,"INI values missing.");
            });
            Check(results,log,"SHARED FILE LOGGER",() => {
                Log.UseSensibleDefaults("common-probe",scratch,eloglevel.info);
                string marker = "COMMON TEST " + Guid.NewGuid().ToString("N");
                Log.LogThis(marker,eloglevel.info);
                Require(File.ReadAllText(Log.LogPath).Contains(marker),"Shared log message not persisted.");
            });
            return results;
        }

        public static void Check(List<string> results, Action<string> log, string name, Action action)
        {
            try { action(); results.Add("PASS " + name); log("PASS " + name); }
            catch (Exception ex) { results.Add("FAIL " + name); log("FAIL " + name + " " + ex); }
        }

        public static void Require(bool condition,string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }

        private static void Reject(Action action)
        {
            bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Require(rejected,"Invalid path accepted.");
        }
    }
}
