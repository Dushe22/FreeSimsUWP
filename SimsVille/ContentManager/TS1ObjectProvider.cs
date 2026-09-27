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
    public interface IVMTS1LotInfo
    {
        short GetLotZoning(short lot);
    }
    public interface IVMContentProvider
    {
        GameObject GetObject(uint guid, bool ts1);
        GameGlobal GetGlobal(string name, bool ts1);
    }
}

namespace FSO.Content.TS1
{
    /// <summary>
    /// Read-only TS1 VM content. Deterministic precedence (later wins): base, Deluxe,
    /// ExpansionShared, ExpansionPack 1..7, Downloads. Within a root, ordinal archive
    /// paths precede ordinal loose IFF paths. This is the port's explicit override policy.
    /// No TSO catalogs or graphics device. Use on the VM thread.
    /// </summary>
    public sealed class TS1ObjectProvider : IVMContentProvider, IVMTS1LotInfo
    {
        private sealed class Source
        {
            public string Path, RelativePath, Entry;
            public IffFile Read()
            {
                if (Entry == null) return new IffFile(Path);
                var archive = new FAR1Archive(Path, false);
                try {
                    var bytes = archive.GetEntry(new KeyValuePair<string, byte[]>(Entry, null));
                    if (bytes == null) throw new FileNotFoundException("Missing TS1 archive entry: " + Entry);
                    return ReadIff(bytes, Entry);
                } finally { archive.Close(); }
            }
        }
        private readonly GamePaths paths;
        private readonly int neighborhood;
        private Dictionary<short, short> zoning;
        public short GetLotZoning(short lot)
        {
            if (lot <= 0) throw new ArgumentOutOfRangeException("lot");
            if (zoning == null) {
                var file = new NeighborhoodStore(paths, neighborhood).GetReadPath("LotZoning.iff");
                var strings = new IffFile(file).Get<STR>(1);
                if (strings == null) throw new InvalidDataException("LotZoning.iff requires STR# 1.");
                var parsed = new Dictionary<short, short>();
                for (int i = 0; i < strings.Length; i++) {
                    var parts = strings.GetString(i, STRLangCode.EnglishUS).Split(',');
                    short id;
                    if (parts.Length != 2 || !short.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out id) || id <= 0 || parsed.ContainsKey(id))
                        throw new InvalidDataException("Invalid or duplicate lot zoning entry " + i);
                    string kind = parts[1].Trim();
                    if (kind != "residential" && kind != "community") throw new InvalidDataException("Unknown lot zoning: " + kind);
                    parsed.Add(id, (short)(kind == "community" ? 1 : 0));
                }
                zoning = parsed; // Publish only after validating the complete table.
            }
            short value;
            // TS1 fixed destination lots are omitted from the editable zoning table.
            return zoning.TryGetValue(lot, out value) ? value : (short)(lot >= 81 && lot <= 89 ? 2 : 1);
        }
        private readonly Dictionary<uint, Source> entries = new Dictionary<uint, Source>();
        private readonly Dictionary<uint, GameObject> objects = new Dictionary<uint, GameObject>();
        private readonly Dictionary<Source, GameObjectResource> resources = new Dictionary<Source, GameObjectResource>();
        private readonly Dictionary<string, Source> globalEntries = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameGlobal> globals = new Dictionary<string, GameGlobal>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> sourceFiles = new List<string>();
        public int DefinitionCount { get { return entries.Count; } }
        public int OverrideCount { get; private set; }
        public IList<string> SourceFiles { get { return sourceFiles.AsReadOnly(); } }
        public IEnumerable<uint> Guids { get { return entries.Keys.OrderBy(x => x).ToArray(); } }
        public string GetSourceFile(uint guid)
        {
            Source source;
            return entries.TryGetValue(guid, out source) ? source.RelativePath : null;
        }

        public TS1ObjectProvider(GamePaths paths, bool includeExpansionContent = true, int neighborhood = 0)
        {
            if (paths == null) throw new ArgumentNullException("paths");
            this.paths = paths;
            this.neighborhood = neighborhood;
            IndexArchive("GameData/Global/Global.far");
            IndexArchive("GameData/Objects/Objects.far");
            if (!includeExpansionContent) return; // Stable base-game regression fixture.
            IndexRoot("GameData/Objects", true);
            foreach (var root in new[] { "Deluxe", "ExpansionShared", "ExpansionPack", "ExpansionPack2", "ExpansionPack3", "ExpansionPack4", "ExpansionPack5", "ExpansionPack6", "ExpansionPack7", "Downloads" })
                IndexRoot(root, false);
        }

        // Do not follow junctions/symlinks outside the selected game tree.
        private static IEnumerable<string> Files(string directory)
        {
            if (!Directory.Exists(directory) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) yield break;
            foreach (var file in Directory.GetFiles(directory))
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) yield return file;
            foreach (var child in Directory.GetDirectories(directory))
                foreach (var file in Files(child)) yield return file;
        }
        private void IndexRoot(string root, bool looseOnly)
        {
            string directory = paths.GetGameDataPath(root);
            var files = Files(directory).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ThenBy(x => x, StringComparer.Ordinal).ToArray();
            if (!looseOnly)
                foreach (var file in files.Where(x => string.Equals(System.IO.Path.GetExtension(x), ".far", StringComparison.OrdinalIgnoreCase)))
                    IndexArchive(root + "/" + file.Substring(directory.Length + 1).Replace('\\', '/'));
            foreach (var file in files.Where(x => string.Equals(System.IO.Path.GetExtension(x), ".iff", StringComparison.OrdinalIgnoreCase))) {
                var source = new Source { Path = file, RelativePath = root + "/" + file.Substring(directory.Length + 1).Replace('\\', '/') };
                Index(source, source.Read(), new HashSet<uint>());
                sourceFiles.Add(source.RelativePath);
            }
        }
        private void IndexArchive(string relative)
        {
            string file = paths.GetGameDataPath(relative);
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("TS1 archives cannot be symbolic links: " + relative);
            var archive = new FAR1Archive(file, false);
            try {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var guids = new HashSet<uint>();
                bool used = false;
                foreach (var entry in archive.GetAllFarEntries().Where(x => x.Filename.EndsWith(".iff", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Filename, StringComparer.Ordinal)) {
                    if (!names.Add(entry.Filename)) throw new InvalidDataException("Duplicate TS1 archive entry: " + relative + "!" + entry.Filename);
                    var source = new Source { Path = file, RelativePath = relative, Entry = entry.Filename };
                    Index(source, ReadIff(archive.GetEntry(entry), entry.Filename), guids);
                    used = true;
                }
                if (used) sourceFiles.Add(relative);
            } finally { archive.Close(); }
        }
        private void Index(Source source, IffFile iff, HashSet<uint> localGuids)
        {
            string name = System.IO.Path.GetFileName(source.Entry ?? source.Path);
            globalEntries[name] = source;
            foreach (var def in iff.List<OBJD>() ?? new List<OBJD>()) {
                if (def.GUID == 0) continue;
                if (!localGuids.Add(def.GUID)) throw new InvalidDataException("Duplicate GUID within " + source.RelativePath + ": " + def.GUID.ToString("X8"));
                if (entries.ContainsKey(def.GUID)) OverrideCount++;
                entries[def.GUID] = source;
            }
        }
        private static IffFile ReadIff(byte[] data, string name)
        {
            var iff = new IffFile();
            using (var stream = new MemoryStream(data, false)) iff.Read(stream);
            iff.RuntimeInfo.Path = name;
            return iff;
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
            Source source;
            if (!globalEntries.TryGetValue(name, out source)) throw new FileNotFoundException("Missing TS1 global: " + name);
            result = new GameGlobal { Resource = new GameGlobalResource(source.Read(), null) };
            globals.Add(name, result);
            return result;
        }
        public GameObject GetObject(uint guid, bool ts1)
        {
            RequireTS1(ts1);
            GameObject result;
            if (objects.TryGetValue(guid, out result)) return result;
            Source source;
            if (!entries.TryGetValue(guid, out source)) return null;
            GameObjectResource resource;
            if (!resources.TryGetValue(source, out resource)) {
                resource = new GameObjectResource(source.Read(), null, null, source.Entry ?? source.RelativePath, true, n => GetGlobal(n, true));
                resources.Add(source, resource);
            }
            // Cache only winning definitions; loading a sibling must never undo an override.
            foreach (var def in resource.List<OBJD>()) {
                Source winner;
                if (def.GUID != 0 && entries.TryGetValue(def.GUID, out winner) && ReferenceEquals(winner, source) && !objects.ContainsKey(def.GUID))
                    objects.Add(def.GUID, new GameObject { GUID = def.GUID, OBJ = def, Resource = resource });
            }
            return objects[guid];
        }
    }
}