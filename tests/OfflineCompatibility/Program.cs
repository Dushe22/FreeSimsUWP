using System;
using System.IO;
using System.Linq;
using FSO.Common.Platform;
using FSO.Files;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;

namespace FreeSims.Tests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 2) { Console.Error.WriteLine("Expected game root and scratch root."); return 2; }
            var paths = new GamePaths(Path.Combine(args[1],"Content"),args[0],Path.Combine(args[1],"UserData"));
            bool passed = OfflineTests.Run(paths,Console.WriteLine).All(x => x.StartsWith("PASS "));
            try
            {
                var house = new IffFile(paths.GetGameDataPath("UserData/Houses/House01.iff"));
                var bmp = house.SilentListAll().OfType<BMP>().Single(x => x.ChunkID == 512);
                using (var stream = new MemoryStream(bmp.ChunkData))
                {
                    var pixels = ImageLoader.BitmapReader(stream);
                    if (pixels.Item2 < 2 || pixels.Item3 < 2) throw new InvalidDataException("Empty thumbnail.");
                    Console.WriteLine("PASS THUMBNAIL CPU DECODE " + pixels.Item2 + "x" + pixels.Item3);
                }
            }
            catch (Exception ex) { Console.WriteLine("FAIL THUMBNAIL CPU DECODE " + ex); passed = false; }
            return passed ? 0 : 1;
        }
    }
}
