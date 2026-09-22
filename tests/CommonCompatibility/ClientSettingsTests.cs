using System;
using System.Collections.Generic;
using System.IO;
using FSO.Client;

namespace FreeSims.Tests
{
    // Exercises the real SimsVille settings source, not a replacement model.
    public static class ClientSettingsTests
    {
        public static List<string> Run(string scratch, Action<string> log)
        {
            Directory.CreateDirectory(scratch);
            var results = new List<string>();
            CommonCompatibilityTests.Check(results, log, "CLIENT FIRST RUN DEFAULTS", () => {
                string path = Path.Combine(scratch, "fresh.ini");
                if (File.Exists(path)) File.Delete(path);
                var settings = new GlobalSettings(path);
                CommonCompatibilityTests.Require(settings.GraphicsWidth == 1024 && settings.GraphicsHeight == 768 &&
                    settings.Windowed && settings.Language == 1 && settings.FXVolume == 10 && settings.SkipIntro,
                    "Fresh client settings did not initialize declared defaults.");
                CommonCompatibilityTests.Require(File.Exists(path), "First-run settings were not written.");
            });
            CommonCompatibilityTests.Check(results, log, "CLIENT PROPERTY SAVE RELOAD", () => {
                string path = Path.Combine(scratch, "saved.ini");
                File.WriteAllText(path, "GraphicsWidth=1024\nGraphicsHeight=768\nWindowed=true\n");
                var settings = new GlobalSettings(path);
                settings.GraphicsWidth = 1280;
                settings.GraphicsHeight = 720;
                settings.Windowed = false;
                settings.Language = 2;
                settings.StartupPath = Path.Combine(scratch, "Game Data");
                settings.LastHead = ulong.MaxValue;
                settings.Save();
                var loaded = new GlobalSettings(path);
                CommonCompatibilityTests.Require(loaded.GraphicsWidth == 1280 && loaded.GraphicsHeight == 720 &&
                    !loaded.Windowed && loaded.Language == 2 && loaded.StartupPath == settings.StartupPath &&
                    loaded.LastHead == ulong.MaxValue, "Save discarded modified client properties.");
            });
            CommonCompatibilityTests.Check(results, log, "CLIENT PARTIAL AND INVALID SETTINGS", () => {
                string path = Path.Combine(scratch, "partial.ini");
                File.WriteAllText(path, "\n# original fixture\n[Settings]\nGraphicsWidth=1600\nGraphicsHeight=invalid\nFXVolume=999\nUnknownKey=ignored\n");
                var settings = new GlobalSettings(path);
                CommonCompatibilityTests.Require(settings.GraphicsWidth == 1600 && settings.GraphicsHeight == 768 &&
                    settings.FXVolume == 10 && settings.Windowed && settings.Language == 1,
                    "Missing or malformed settings did not retain declared defaults.");
                File.WriteAllText(path, "GraphicsWidth=bad\n");
                settings.Load();
                CommonCompatibilityTests.Require(settings.GraphicsWidth == 1024, "Reload used stale values as defaults.");
            });
            CommonCompatibilityTests.Check(results, log, "CLIENT SAVE FAILURE IS VISIBLE", () => {
                string path = Path.Combine(scratch, "locked.ini");
                File.WriteAllText(path, "GraphicsWidth=1024\n");
                var settings = new GlobalSettings(path);
                bool failed = false;
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    try { settings.Save(); } catch (IOException) { failed = true; }
                }
                CommonCompatibilityTests.Require(failed, "A failed settings write was reported as successful.");
            });
            return results;
        }
    }
}
