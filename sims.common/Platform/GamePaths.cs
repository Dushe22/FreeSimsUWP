using System;
using System.IO;

namespace FSO.Common.Platform
{
    /// <summary>Separate engine-owned package content, user-provisioned game data,
    /// and writable user data. Resolves relative paths without changing cwd.</summary>
    public sealed class GamePaths : IGamePaths
    {
        public string ContentRoot { get; private set; }
        public string GameDataRoot { get; private set; }
        public string UserDataRoot { get; private set; }

        public GamePaths(string contentRoot, string gameDataRoot, string userDataRoot)
        {
            ContentRoot = Normalize(contentRoot);
            GameDataRoot = Normalize(gameDataRoot);
            UserDataRoot = Normalize(userDataRoot);
            if (Overlap(ContentRoot,GameDataRoot) || Overlap(ContentRoot,UserDataRoot) ||
                Overlap(GameDataRoot,UserDataRoot))
                throw new ArgumentException("Content, game-data and user-data roots must be disjoint.");
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                throw new ArgumentException("An absolute directory is required.", nameof(path));
            string full = Path.GetFullPath(path);
            if (!string.Equals(Path.GetPathRoot(path),Path.GetPathRoot(full),StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("A fully qualified directory is required.", nameof(path));
            return full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
        }

        private static bool Overlap(string left, string right)
        {
            return left.StartsWith(right,StringComparison.OrdinalIgnoreCase) ||
                right.StartsWith(left,StringComparison.OrdinalIgnoreCase);
        }

        private static string Resolve(string root, string relativePath)
        {
            if (relativePath == null) throw new ArgumentNullException(nameof(relativePath));
            if (relativePath.Length == 0) return root;
            if (Path.IsPathRooted(relativePath) || relativePath.IndexOf(':') >= 0)
                throw new ArgumentException("A relative path without a drive or stream name is required.", nameof(relativePath));
            string path = Path.GetFullPath(Path.Combine(root,relativePath));
            if (!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Path leaves its configured root.", nameof(relativePath));
            return path;
        }

        public string GetContentPath(string relativePath) { return Resolve(ContentRoot,relativePath); }
        public string GetGameDataPath(string relativePath) { return Resolve(GameDataRoot,relativePath); }
        public string GetUserDataPath(string relativePath) { return Resolve(UserDataRoot,relativePath); }

        public void ApplyToEnvironment(bool directX)
        {
            FSOEnvironment.ContentDir = ContentRoot;
            FSOEnvironment.SimsCompleteDir = GameDataRoot;
            FSOEnvironment.UserDir = UserDataRoot;
            FSOEnvironment.GFXContentDir = GetContentPath(directX ? "DX" : "OGL") + Path.DirectorySeparatorChar;
            FSOEnvironment.DirectX = directX;
        }
    }
}
