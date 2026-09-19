using System;
using System.Diagnostics;
using System.IO;
using Windows.Storage;

namespace FreeSims.Xbox.Proof
{
    internal static class ProofLog
    {
        private static readonly object Sync = new object();

        public static void Write(string message)
        {
            string line = DateTimeOffset.UtcNow.ToString("O") + " " + message;
            Debug.WriteLine(line);
            try
            {
                lock (Sync)
                {
                    string path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "proof.log");
                    if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
                    {
                        string previous = path + ".previous";
                        if (File.Exists(previous)) File.Delete(previous);
                        File.Move(path, previous);
                    }
                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // Keep a file-system failure from hiding the original graphics/runtime error.
                Debug.WriteLine("LOG WRITE FAILED " + ex);
            }
        }
    }
}
