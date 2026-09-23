using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FSO.Common.Platform;
using FSO.Files.FAR1;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;

namespace FreeSims.Tests
{
    public static class TS1ContentTests
    {
        public const int Count = 9;
        public static List<string> Run(GamePaths paths, Action<string> log)
        {
            var results = TS1ParserTests.Run(log);
            Check(results, log, "NEIGHBORHOOD CHUNKS", () => {
                var iff = new IffFile(paths.GetGameDataPath("UserData/Neighborhood.iff"));
                var neighbors = iff.List<NBRS>();
                Require(neighbors != null && neighbors.Count > 0, "No NBRS chunk.");
                Require(iff.List<NGBH>() != null && iff.List<NGBH>().Count > 0, "No NGBH chunk.");
                log("NEIGHBORS " + neighbors.Sum(x => x.Entries.Count));
            });
            Check(results, log, "HOUSE CHUNKS", () => {
                var iff = new IffFile(paths.GetGameDataPath("UserData/Houses/House01.iff"));
                Require(iff.List<HOUS>() != null && iff.List<HOUS>().Count > 0, "No HOUS chunk.");
                Require(iff.SilentListAll().Count > 0, "No recognized house chunks.");
                log("HOUSE CHUNKS " + iff.SilentListAll().Count);
            });
            Check(results, log, "OBJECT ARCHIVE AND DEFINITIONS", () => {
                var archive = new FAR1Archive(paths.GetGameDataPath("GameData/Objects/Objects.far"), false);
                try
                {
                    int files = 0, definitions = 0;
                    foreach (var entry in archive.GetAllFarEntries())
                    {
                        if (!entry.Filename.EndsWith(".iff", StringComparison.OrdinalIgnoreCase)) continue;
                        using (var stream = new MemoryStream(archive.GetEntry(entry), false))
                        {
                            var iff = new IffFile();
                            iff.Read(stream);
                            foreach (var chunk in iff.SilentListAll().Where(x => x is OBJD)) { try { iff.Get<OBJD>(chunk.ChunkID); } catch (Exception ex) { throw new InvalidDataException(entry.Filename + " OBJD " + chunk.ChunkID + " bytes=" + chunk.ChunkData.Length + " version=" + BitConverter.ToUInt32(chunk.ChunkData, 0), ex); } }
                            var objects = iff.List<OBJD>();
                            definitions += objects == null ? 0 : objects.Count;
                            files++;
                        }
                    }
                    Require(files > 0 && definitions > 0, "No object IFFs/definitions.");
                    log("OBJECT IFFS " + files + " DEFINITIONS " + definitions);
                }
                finally { archive.Close(); }
            });
            // Unique scratch user root: tests never overwrite an existing player's saves.
            var scratch = new GamePaths(paths.ContentRoot, paths.GameDataRoot,
                paths.GetUserDataPath("ContentProbe/" + Guid.NewGuid().ToString("N")));
            var store = new NeighborhoodStore(scratch, 0);
            string source = paths.GetGameDataPath("UserData/Neighborhood.iff");
            string before = null;
            Check(results, log, "ISOLATED SAVE AND RELOAD", () => {
                before = Hash(source);
                Require(store.GetReadPath("Neighborhood.iff") == source, "First read did not use installation.");
                store.Write("Neighborhood.iff", output => {
                    using (var input = File.OpenRead(source)) input.CopyTo(output);
                });
                var fresh = new NeighborhoodStore(scratch, 0);
                string saved = fresh.GetReadPath("Neighborhood.iff");
                Require(saved.StartsWith(scratch.UserDataRoot, StringComparison.OrdinalIgnoreCase), "Save escaped user root.");
                Require(Hash(saved) == before && Hash(source) == before, "Copy altered bytes.");
                var iff = new IffFile(saved);
                Require(iff.List<NGBH>() != null && iff.List<NGBH>().Count > 0, "Saved copy cannot be parsed.");
                // Exercise replacement as well as first save.
                fresh.Write("Neighborhood.iff", output => { using (var input = File.OpenRead(source)) input.CopyTo(output); });
                Require(Hash(fresh.GetReadPath("Neighborhood.iff")) == before, "Replacement changed bytes.");
            });
            Check(results, log, "FAILED SAVE PRESERVES DATA", () => {
                Require(before != null, "Initial save test failed.");
                string saved = store.GetReadPath("Neighborhood.iff");
                Require(saved != source, "No saved copy.");
                bool failed = false;
                try { store.Write("Neighborhood.iff", output => { output.WriteByte(42); throw new IOException("Intentional failure"); }); }
                catch (IOException) { failed = true; }
                Require(failed && Hash(saved) == before && Hash(source) == before, "Failed save changed data.");
            });
            Check(results, log, "SAVE PATH BOUNDARIES", () => {
                foreach (var bad in new[] { "../Neighborhood.iff", "Houses/../../file", "C:/file", "/file", "x:stream" })
                {
                    bool rejected = false;
                    try { store.GetReadPath(bad); } catch (ArgumentException) { rejected = true; }
                    Require(rejected, "Unsafe path accepted: " + bad);
                }
                var second = new NeighborhoodStore(scratch, 1);
                Require(second.GetReadPath("Neighborhood.iff") == paths.GetGameDataPath("UserData2/Neighborhood.iff"),
                    "Neighborhood selection incorrect.");
            });
            log("RESULT " + results.Count(x => x.StartsWith("PASS ")) + "/" + Count + " PASS");
            return results;
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(stream));
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
        private static void Check(List<string> results, Action<string> log, string name, Action action)
        {
            try { action(); results.Add("PASS " + name); log("PASS " + name); }
            catch (Exception ex) { results.Add("FAIL " + name); log("FAIL " + name + " " + ex); }
        }
    }
}
