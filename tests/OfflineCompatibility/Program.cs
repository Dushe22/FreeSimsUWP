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
            if (args.Length == 2 && args[0] == "--inspect-lots") return LotInspection.Run(args[1]);
            if (args.Length != 2) { Console.Error.WriteLine("Expected game root and scratch root."); return 2; }
            var paths = new GamePaths(Path.Combine(args[1],"Content"),args[0],Path.Combine(args[1],"UserData"));
            bool passed = ObjectTypeMappingTests.Run();
            passed &= LotPlacementTests.Run(paths, Console.WriteLine).All(x => x.StartsWith("PASS "));
            passed &= OBJMPlacementTests.Run(Console.WriteLine).All(x => x.StartsWith("PASS "));
            passed &= OfflineTests.Run(paths,Console.WriteLine).All(x => x.StartsWith("PASS "));
            try
            {
                ThumbnailColorKeyTests.Validate();
                Console.WriteLine("PASS EXACT THUMBNAIL KEYS AND NEIGHBORING COLORS");
                foreach (int id in new[] { 1, 2, 28 })
                {
                    var house = new IffFile(paths.GetGameDataPath("UserData/Houses/House" + id.ToString("00") + ".iff"));
                    var bmp = house.SilentListAll().OfType<BMP>().Single(x => x.ChunkID == 512);
                    using (var stream = new MemoryStream(bmp.ChunkData))
                    {
                        var pixels = ImageLoader.BitmapReader(stream);
                        int cleared = ImageLoader.ApplyBitmapColorKey(pixels.Item1);
                        int opaque = Enumerable.Range(0, pixels.Item1.Length / 4).Count(i => pixels.Item1[i * 4 + 3] == 255);
                        if (cleared == 0 || opaque == 0) throw new InvalidDataException("Expected transparent background and retained image.");
                        Console.WriteLine("PASS THUMBNAIL " + id + " " + pixels.Item2 + "x" + pixels.Item3 + " CLEARED=" + cleared + " OPAQUE=" + opaque);
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("FAIL THUMBNAIL CPU DECODE " + ex); passed = false; }
            return passed ? 0 : 1;
        }
    }
}
