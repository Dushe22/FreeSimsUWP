using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;

namespace FreeSims.Tests
{
    // Read-only parser diagnostic; successful inspection does not mean a lot can run.
    internal static class LotInspection
    {
        public static int Run(string gameRoot)
        {
            int inspected = 0, errors = 0;
            foreach (var path in Directory.GetFiles(Path.Combine(gameRoot, "UserData/Houses"), "House*.iff").OrderBy(x => x))
            {
                string before = Hash(path);
                try
                {
                    var iff = new IffFile(path);
                    var simi = iff.Get<SIMI>(1);
                    var objt = iff.Get<OBJT>(0);
                    var objm = iff.Get<OBJM>(1);
                    if (simi == null || objt == null || objm == null)
                        throw new InvalidDataException("Missing SIMI 1, OBJT 0 or OBJM 1.");
                    objm.ResolveTypes(objt);
                    Console.WriteLine(Path.GetFileName(path) + " size=" + simi.GlobalData[23] +
                        " types=" + objt.Entries.Count + " mapped=" + objm.ObjectData.Count +
                        " pairs=" + objm.IDToOBJT.Length / 2);
                    inspected++;
                }
                catch (Exception ex) { Console.WriteLine("ERROR " + Path.GetFileName(path) + " " + ex.GetType().Name + ": " + ex.Message); errors++; }
                if (Hash(path) != before) throw new IOException("Source changed: " + path);
            }
            Console.WriteLine("INSPECTED " + inspected + " ERRORS " + errors + "; input SHA256 hashes unchanged; gameplay not tested.");
            return inspected > 0 && errors == 0 ? 0 : 1;
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(stream));
        }
    }
}