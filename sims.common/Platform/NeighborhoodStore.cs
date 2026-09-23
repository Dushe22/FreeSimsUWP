using System;
using System.IO;

namespace FSO.Common.Platform
{
    /// <summary>Copy-on-write neighborhood data. Original installation files are never opened for writing.</summary>
    public sealed class NeighborhoodStore
    {
        private readonly GamePaths paths;
        private readonly string folder;
        public NeighborhoodStore(GamePaths paths, int neighborhood)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            if (neighborhood < 0 || neighborhood > 7) throw new ArgumentOutOfRangeException(nameof(neighborhood));
            this.paths = paths;
            folder = neighborhood == 0 ? "UserData" : "UserData" + (neighborhood + 1);
        }

        private static string Validate(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(":"))
                throw new ArgumentException("A neighborhood-relative filename is required.");
            foreach (var part in relative.Replace('\\', '/').Split('/'))
                if (part == ".." || part == "." || part.Length == 0)
                    throw new ArgumentException("Invalid neighborhood path.");
            return relative;
        }

        public string GetReadPath(string relative)
        {
            relative = Validate(relative);
            string saved = paths.GetUserDataPath(Path.Combine("Neighborhoods", folder, relative));
            return File.Exists(saved) ? saved : paths.GetGameDataPath(Path.Combine(folder, relative));
        }

        public void Write(string relative, Action<Stream> write)
        {
            relative = Validate(relative);
            if (write == null) throw new ArgumentNullException(nameof(write));
            string target = paths.GetUserDataPath(Path.Combine("Neighborhoods", folder, relative));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    write(stream);
                if (File.Exists(target)) File.Replace(temporary, target, null);
                else File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
