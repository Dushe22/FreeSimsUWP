using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Common.Platform;
using FSO.Files.FAR1;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;

namespace FSO.Content
{
    public interface IVMContentProvider
    {
        GameObject GetObject(uint guid, bool ts1);
        GameGlobal GetGlobal(string name, bool ts1);
    }
}

namespace FSO.Content.TS1
{
    /// <summary>
    /// Read-only base-game VM content without TSO catalogs, patching or a graphics device.
    /// Resources load lazily after indexing OBJD chunks. Use on the VM thread.
    /// Expansion/download precedence and saved-lot restoration are separate work.
    /// </summary>
    public sealed class TS1ObjectProvider : IVMContentProvider
    {
        private readonly string objectsPath;
        private readonly string globalsPath;
        private readonly Dictionary<uint, string> entries = new Dictionary<uint, string>();
        private readonly Dictionary<uint, GameObject> objects = new Dictionary<uint, GameObject>();
        private readonly Dictionary<string, GameGlobal> globals = new Dictionary<string, GameGlobal>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> globalNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public int DefinitionCount { get { return entries.Count; } }


        public TS1ObjectProvider(GamePaths paths)
        {
            if (paths == null) throw new ArgumentNullException("paths");
            objectsPath = paths.GetGameDataPath("GameData/Objects/Objects.far");
            globalsPath = paths.GetGameDataPath("GameData/Global/Global.far");
            var archive = new FAR1Archive(globalsPath, false);
            try {
                foreach (var entry in archive.GetAllFarEntries().Where(x => x.Filename.EndsWith(".iff", StringComparison.OrdinalIgnoreCase)))
                    if (!globalNames.Add(entry.Filename)) throw new InvalidDataException("Duplicate TS1 global: " + entry.Filename);
            } finally { archive.Close(); }
            archive = new FAR1Archive(objectsPath, false);
            try {
                foreach (var entry in archive.GetAllFarEntries().Where(x => x.Filename.EndsWith(".iff", StringComparison.OrdinalIgnoreCase))) {
                    var iff = Read(archive.GetEntry(entry), entry.Filename);
                    foreach (var def in iff.List<OBJD>() ?? new List<OBJD>()) {
                        if (def.GUID == 0) continue;
                        if (entries.ContainsKey(def.GUID)) throw new InvalidDataException("Duplicate TS1 GUID: " + def.GUID.ToString("X8"));
                        entries.Add(def.GUID, entry.Filename);
                    }
                }
            } finally { archive.Close(); }
        }

        private static IffFile Read(byte[] data, string name)
        {
            if (data == null) throw new FileNotFoundException("Missing TS1 archive entry: " + name);
            var iff = new IffFile();
            using (var stream = new MemoryStream(data, false)) iff.Read(stream);
            iff.RuntimeInfo.Path = name;
            return iff;
        }

        private static IffFile ReadEntry(string path, string name)
        {
            var archive = new FAR1Archive(path, false);
            try {
                var entry = archive.GetAllFarEntries().SingleOrDefault(x => string.Equals(x.Filename, name, StringComparison.OrdinalIgnoreCase));
                if (entry == null) throw new FileNotFoundException("Missing TS1 archive entry: " + name);
                return Read(archive.GetEntry(entry), name);
            } finally { archive.Close(); }
        }

        private static void RequireTS1(bool ts1)
        {
            if (!ts1) throw new InvalidOperationException("TS1 content cannot serve a TSO VM.");
        }

        public GameGlobal GetGlobal(string name, bool ts1)
        {
            RequireTS1(ts1);
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || name.Contains(".."))
                throw new ArgumentException("Expected a TS1 global name.", "name");
            name = name.TrimEnd('\0');
            if (!name.EndsWith(".iff", StringComparison.OrdinalIgnoreCase)) name += ".iff";
            GameGlobal result;
            if (globals.TryGetValue(name, out result)) return result;
            if (!globalNames.Contains(name)) throw new FileNotFoundException("Missing TS1 global: " + name);
            result = new GameGlobal { Resource = new GameGlobalResource(ReadEntry(globalsPath, name), null) };
            globals.Add(name, result);
            return result;
        }

        public GameObject GetObject(uint guid, bool ts1)
        {
            RequireTS1(ts1);
            GameObject result;
            if (objects.TryGetValue(guid, out result)) return result;
            string name;
            if (!entries.TryGetValue(guid, out name)) return null;
            var iff = ReadEntry(objectsPath, name);
            var resource = new GameObjectResource(iff, null, null, name, true, n => GetGlobal(n, true));
            foreach (var def in iff.List<OBJD>()) {
                if (def.GUID != 0) objects.Add(def.GUID, new GameObject { GUID = def.GUID, OBJ = def, Resource = resource });
            }
            return objects[guid];
        }
    }
}