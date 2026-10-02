using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Common.Platform;
using FSO.Files.FAR1;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using Microsoft.Xna.Framework;

namespace FSO.Content.TS1
{
    // Read-only, lazy TS1 architecture catalog. No GPU textures borrowed from IFFs.
    public sealed class TS1MaterialProvider
    {
        public sealed class Material
        {
            public int Width, Height;
            public Color[] Pixels;
            public string Name;
        }
        private sealed class Source
        {
            public string Path, Entry;
            public IffFile Read()
            {
                if (Entry == null) return new IffFile(Path);
                var archive = new FAR1Archive(Path, false);
                try {
                    var bytes = archive.GetEntry(new KeyValuePair<string, byte[]>(Entry, null));
                    if (bytes == null) throw new InvalidDataException("Missing material entry: " + Entry);
                    var iff = new IffFile();
                    using (var stream = new MemoryStream(bytes, false)) iff.Read(stream);
                    return iff;
                } finally { archive.Close(); }
            }
        }
        private readonly Dictionary<string, Source> sources = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Material> cache = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<ushort, string> floors, walls;
        private readonly IffFile floorGlobals, wallGlobals;
        private readonly GamePaths paths;
        private readonly int[] wallInset = new int[2], wallSpan = new int[2];
        public TS1MaterialProvider(GamePaths paths, IffFile lot)
        {
            this.paths = paths;
            floors = Map(lot.Get<FLRm>(1) ?? lot.Get<FLRm>(0));
            walls = Map(lot.Get<WALm>(1) ?? lot.Get<WALm>(0));
            floorGlobals = new IffFile(paths.GetGameDataPath("GameData/floors.iff"));
            wallGlobals = new IffFile(paths.GetGameDataPath("GameData/walls.iff"));
            // Crop the full-wall mask's common top padding, never the bounds of a
            // fence/window itself: their authored height and holes must stay intact.
            var fullWall = wallGlobals.Get<SPR>(1026);
            if (fullWall == null || fullWall.Frames.Count < 2) throw new InvalidDataException("Missing full wall masks.");
            for (int i = 0; i < 2; i++) {
                var frame = fullWall.Frames[i]; frame.DecodeIfRequired(); int x = frame.Width / 2, first = -1, last = -1;
                for (int y = 0; y < frame.Height; y++) if (frame.GetPixel(x,y).A >= 128) {if(first < 0) first = y; last = y;}
                if (first < 0 || last <= first) throw new InvalidDataException("Invalid full wall mask.");
                wallInset[i] = first - (i == 0 ? (frame.Width - 1 - x) / 2 : x / 2);
                wallSpan[i] = last - first + 1;
            }
            foreach (var root in new[] { "GameData/Floors", "GameData/Walls", "Deluxe", "ExpansionShared", "ExpansionPack", "ExpansionPack2", "ExpansionPack3", "ExpansionPack4", "ExpansionPack5", "ExpansionPack6", "ExpansionPack7", "Downloads" }) {
                var files = Files(paths.GetGameDataPath(root)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ThenBy(x => x, StringComparer.Ordinal).ToArray();
                foreach (var file in files.Where(x => string.Equals(Path.GetExtension(x), ".far", StringComparison.OrdinalIgnoreCase))) {
                    var archive = new FAR1Archive(file, false);
                    try {
                        foreach (var entry in archive.GetAllFarEntries().Where(x => IsMaterial(x.Filename)).OrderBy(x => x.Filename, StringComparer.Ordinal))
                            sources[Name(entry.Filename)] = new Source { Path = file, Entry = entry.Filename };
                    } finally { archive.Close(); }
                }
                foreach (var file in files.Where(IsMaterial)) sources[Name(file)] = new Source { Path = file };
            }
        }
        private static bool IsMaterial(string path) { var ext = Path.GetExtension(path); return ext.Equals(".flr", StringComparison.OrdinalIgnoreCase) || ext.Equals(".wll", StringComparison.OrdinalIgnoreCase); }
        private static string Name(string path) { return Path.GetFileName(path.Replace('\\', '/')); }
        private static IEnumerable<string> Files(string path)
        {
            if (!Directory.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) yield break;
            foreach (var file in Directory.GetFiles(path)) if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) yield return file;
            foreach (var child in Directory.GetDirectories(path)) foreach (var file in Files(child)) yield return file;
        }
        private static Dictionary<ushort, string> Map(WALm map)
        {
            var result = new Dictionary<ushort, string>();
            if (map != null) foreach (var entry in map.Entries) {
                if (result.ContainsKey(entry.ID)) throw new InvalidDataException("Duplicate lot material ID.");
                result.Add(entry.ID, Name(entry.Name));
            }
            return result;
        }
        public Material Floor(ushort id, bool global)
        {
            if (id == 0 || id >= 65534) return null;
            return Get(id, true, global);
        }
        public Material Wall(ushort id, ushort style = 1)
        {
            var source = Get(id, false, false);
            if (style == 0 || style == 1 || style == 255) return source;
            string key = source.Name + ":style:" + style;
            Material result;
            if (cache.TryGetValue(key, out result)) return result;
            var mask = wallGlobals.Get<SPR>((ushort)(1024 + style * 2));
            if (mask == null || mask.Frames.Count == 0) throw new InvalidDataException("Missing wall style: " + style);
            var frame = mask.Frames[0]; frame.DecodeIfRequired();
            result = new Material {Name = source.Name, Width = source.Width, Height = source.Height, Pixels = (Color[])source.Pixels.Clone()};
            for (int y = 0; y < result.Height; y++) for (int x = 0; x < result.Width; x++) {
                int sx = x * frame.Width / result.Width;
                int sy = (frame.Width - 1 - sx) / 2 + wallInset[0] + y * (wallSpan[0] - 1) / (result.Height - 1);
                var pixel = result.Pixels[y * result.Width + x];
                pixel.A = frame.GetPixel(sx, Math.Min(frame.Height - 1, sy)).A;
                result.Pixels[y * result.Width + x] = pixel;
            }
            cache.Add(key, result); return result;
        }
        public SPR OpeningMask(ushort style)
        {
            return wallGlobals.Get<SPR>((ushort)(1024 + style * 2));
        }
        public Material WithOpening(Material source, SPR mask, int frameIndex, bool mirror, string identity)
        {
            string key = source.Name + ":opening:" + identity + ":" + frameIndex + ":" + mirror;
            Material result;
            if (cache.TryGetValue(key, out result)) return result;
            if (frameIndex < 0 || frameIndex > 1 || mask == null || mask.Frames.Count <= frameIndex) throw new InvalidDataException("Missing opening mask: " + identity);
            var frame = mask.Frames[frameIndex]; frame.DecodeIfRequired();
            int span = frame.Height - frame.Width / 2;
            if (frame.Width < 2 || span < 2) throw new InvalidDataException("Invalid opening mask: " + identity);
            result = new Material {Name = key, Width = source.Width, Height = source.Height, Pixels = (Color[])source.Pixels.Clone()};
            for (int y = 0; y < result.Height; y++) for (int x = 0; x < result.Width; x++) {
                int sx = Math.Min(frame.Width - 1, x * frame.Width / result.Width);
                if (mirror) sx = frame.Width - 1 - sx;
                int top = frameIndex == 0 ? (frame.Width - 1 - sx) / 2 : sx / 2;
                int sy = top + wallInset[frameIndex] + y * (wallSpan[frameIndex] - 1) / (result.Height - 1);
                var pixel = result.Pixels[y * result.Width + x];
                pixel.A = Math.Min(pixel.A, frame.GetPixel(sx, Math.Min(frame.Height - 1, sy)).A);
                result.Pixels[y * result.Width + x] = pixel;
            }
            cache.Add(key, result); return result;
        }
        public Material Roof(string name)
        {
            // HOUS stores the texture basename. Never accept a path from lot data.
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(new[] {'/', '\\', ':'}) >= 0 || name == "." || name == "..")
                throw new InvalidDataException("Invalid saved roof name.");
            string key = "roof:" + name.ToLowerInvariant();
            Material result;
            if (cache.TryGetValue(key, out result)) return result;
            string directory = paths.GetGameDataPath("GameData/Roofs");
            string file = Files(directory).FirstOrDefault(p => string.Equals(Path.GetFileName(p), name + ".bmp", StringComparison.OrdinalIgnoreCase));
            if (file == null) throw new FileNotFoundException("Upload GameData/Roofs/" + name + ".bmp from your TS1 installation.");
            using(var stream = File.OpenRead(file)) result = DecodeRoofBitmap(stream, key);
            cache.Add(key, result); return result;
        }
        public static Material DecodeRoofBitmap(Stream stream, string name)
        {
            Material result;
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true)) {
                if (stream.Length < 54 || reader.ReadUInt16() != 0x4d42) throw new InvalidDataException("Invalid roof BMP.");
                reader.ReadUInt32(); reader.ReadUInt32(); uint offset = reader.ReadUInt32();
                uint header = reader.ReadUInt32(); int width = reader.ReadInt32(), signedHeight = reader.ReadInt32();
                ushort planes = reader.ReadUInt16(), bits = reader.ReadUInt16(); uint compression = reader.ReadUInt32();
                if (header < 40 || width < 1 || width > 1024 || signedHeight == 0 || Math.Abs((long)signedHeight) > 1024 || planes != 1 || (compression != 0 && (compression != 1 || bits != 8 || signedHeight < 0)) || (bits != 8 && bits != 24 && bits != 32))
                    throw new InvalidDataException("Unsupported roof BMP encoding.");
                int height = Math.Abs(signedHeight), stride = ((width * bits + 31) / 32) * 4;
                if (offset < 14L + header || offset >= stream.Length || stream.Length > 8 * 1024 * 1024 || (compression == 0 && (long)offset + (long)stride * height > stream.Length)) throw new InvalidDataException("Truncated roof BMP.");
                Color[] palette = null;
                if (bits == 8) {
                    stream.Position = 46; uint used = reader.ReadUInt32(); int colors = used == 0 ? 256 : (int)used;
                    if (colors < 1 || colors > 256 || 14L + header + colors * 4 > offset) throw new InvalidDataException("Invalid roof palette.");
                    palette = new Color[colors]; stream.Position = 14 + header;
                    for (int i = 0; i < colors; i++) { byte b = reader.ReadByte(), g = reader.ReadByte(), r = reader.ReadByte(); reader.ReadByte(); palette[i] = new Color(r, g, b); }
                }
                result = new Material {Name = name, Width = width, Height = height, Pixels = new Color[width * height]};
                if (compression == 1) {
                    for (int i = 0; i < result.Pixels.Length; i++) result.Pixels[i] = palette[0];
                    stream.Position = offset; int x = 0, y = 0, commands = 0; bool ended = false;
                    Action<int> put = index => {
                        if (x >= width || y >= height || index >= palette.Length) throw new InvalidDataException("Roof RLE8 pixel outside bitmap.");
                        result.Pixels[(height - 1 - y) * width + x++] = palette[index];
                    };
                    while (stream.Position + 2 <= stream.Length && commands++ < width * height * 4 + height) {
                        int count = reader.ReadByte(), value = reader.ReadByte();
                        if (count != 0) { for (int i = 0; i < count; i++) put(value); }
                        else if (value == 0) { x = 0; y++; if (y > height) throw new InvalidDataException("Roof RLE8 row overflow."); }
                        else if (value == 1) { ended = true; break; }
                        else if (value == 2) { x += reader.ReadByte(); y += reader.ReadByte(); if (x > width || y >= height) throw new InvalidDataException("Roof RLE8 delta overflow."); }
                        else { for (int i = 0; i < value; i++) put(reader.ReadByte()); if ((value & 1) != 0) reader.ReadByte(); }
                    }
                    if (!ended) throw new InvalidDataException("Truncated/unbounded roof RLE8.");
                }
                else for (int y = 0; y < height; y++) {
                    stream.Position = offset + (long)y * stride; int targetY = signedHeight > 0 ? height - 1 - y : y;
                    for (int x = 0; x < width; x++) {
                        Color color;
                        if (bits == 8) { int index = reader.ReadByte(); if (index >= palette.Length) throw new InvalidDataException("Invalid roof palette index."); color = palette[index]; }
                        else { byte b = reader.ReadByte(), g = reader.ReadByte(), r = reader.ReadByte(); if (bits == 32) reader.ReadByte(); color = new Color(r, g, b); }
                        result.Pixels[targetY * width + x] = color;
                    }
                }
            }
            return result;
        }
        private Material Get(ushort id, bool floor, bool global)
        {
            string name = null;
            bool mapped = !global && (floor ? floors : walls).TryGetValue((byte)id, out name);
            if (!mapped) name = (floor ? "floor:" : "wall:") + id;
            Material value;
            if (cache.TryGetValue(name, out value)) return value;
            IffFile iff;
            if (mapped) {
                Source source;
                if (!sources.TryGetValue(name, out source)) throw new FileNotFoundException("Missing TS1 material: " + name);
                iff = source.Read();
            } else iff = floor ? floorGlobals : wallGlobals;
            if (floor) {
                var sprite = iff.Get<SPR2>((ushort)(512 + (mapped ? 1 : id)));
                if (sprite == null || sprite.Frames.Length == 0) throw new InvalidDataException("Missing floor sprite: " + name);
                var frame = sprite.Frames[0]; frame.DecodeIfRequired();
                if (frame.Width < 2 || frame.Height < 2 || frame.PixelData == null) throw new InvalidDataException("Invalid floor pixels: " + name);
                // Unproject the authored isometric diamond into tile UVs once. World
                // geometry supplies rotation and depth, including diagonal half tiles.
                value = new Material { Name = name, Width = 64, Height = 64, Pixels = new Color[4096] };
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) {
                    float u = (x + .5f) / 64, v = (y + .5f) / 64;
                    int sx = Math.Min(frame.Width - 1, Math.Max(0, (int)((u - v + 1) * .5f * frame.Width)));
                    int sy = Math.Min(frame.Height - 1, Math.Max(0, (int)((u + v) * .5f * frame.Height)));
                    var p = frame.PixelData[sy * frame.Width + sx]; p.A = 255;
                    value.Pixels[y * 64 + x] = p;
                }
            } else {
                var sprite = iff.Get<SPR>((ushort)(mapped ? 2049 : 2048 + id));
                if (sprite == null || sprite.Frames.Count == 0) throw new InvalidDataException("Missing wall sprite: " + name);
                var frame = sprite.Frames[0]; frame.DecodeIfRequired();
                if (frame.Width < 2 || frame.Height < 2) throw new InvalidDataException("Invalid wall pixels: " + name);
                value = new Material { Name = name, Width = frame.Width, Height = 240, Pixels = new Color[frame.Width * 240] };
                for (int x = 0; x < frame.Width; x++) {
                    // Frame zero slopes up to the right. Keep the full authored
                    // wall height: scanning opaque bounds would stretch low fences.
                    int top = (frame.Width - 1 - x) / 2;
                    int span = frame.Height - frame.Width / 2;
                    for (int y = 0; y < 240; y++) {
                        int sy = Math.Min(frame.Height - 1, top + wallInset[0] + y * (wallSpan[0] - 1) / 239);
                        value.Pixels[y * frame.Width + x] = frame.GetPixel(x, sy);
                    }
                }
            }
            cache.Add(name, value); return value;
        }
    }
}
