using System;
using System.IO;
using FSO.Common.Platform.Uwp;

namespace LogThis
{
    internal static class PlatformLog
    {
        private static readonly object Sync = new object();

        public static string FallbackDirectory
        {
            get
            {
                var directory = UwpGameStorage.CreatePaths().GetUserDataPath("Logs");
                Directory.CreateDirectory(directory);
                return directory;
            }
        }

        public static string[] DefaultFile()
        {
            return new[] { FallbackDirectory, "FreeSims", "log" };
        }

        // Windows EventLog is unavailable in UWP. Retain diagnostic output in LocalState.
        public static void WriteEvent(string source, string text, eloglevel level)
        {
            lock (Sync)
            {
                var path = Path.Combine(FallbackDirectory,"events.log");
                if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
                {
                    string previous = path + ".previous";
                    if (File.Exists(previous)) File.Delete(previous);
                    File.Move(path,previous);
                }
                File.AppendAllText(path,DateTimeOffset.UtcNow.ToString("O") + " " + source +
                    " " + level + " " + text + Environment.NewLine);
            }
        }
    }
}
