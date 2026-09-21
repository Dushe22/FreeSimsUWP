using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FSO.Files;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using Microsoft.Xna.Framework.Graphics;

namespace FreeSims.Tests
{
    // Entirely original 2x2 images and a synthetic BCON; no game assets.
    public static class FilesCompatibilityTests
    {
        private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAWSURBVBhXY/jPwPBfwSGhgQEC/v8HAC+wBT0TNBT7AAAAAElFTkSuQmCC";
        private static readonly byte[] PngRgba = {
            255,0,0,255, 32,64,96,128,
            0,0,0,0, 0,0,255,255
        };
        private static readonly byte[] BmpRgba = {
            255,0,0,255, 0,255,0,255,
            0,0,255,255, 255,255,0,255
        };

        public static List<string> Run(Action<string> log, GraphicsDevice device = null)
        {
            var results = new List<string>();
            Check(results, log, "BMP ROWS AND CHANNELS", () => Decode(Bmp(BmpRgba), BmpRgba));
            Check(results, log, "PNG STRAIGHT ALPHA", () => Decode(Convert.FromBase64String(Png), PngRgba));
            Check(results, log, "CALLER STREAM STAYS OPEN", () => {
                using (var stream = new MemoryStream(Convert.FromBase64String(Png)))
                {
                    ImageLoader.BitmapReader(stream);
                    Require(stream.CanRead, "Decoder closed caller stream.");
                    stream.Position = 0;
                    Require(stream.ReadByte() == 137, "Caller cannot reuse stream.");
                }
            });
            Check(results, log, "INVALID IMAGE REJECTED", () => {
                bool rejected = false;
                using (var stream = new MemoryStream(new byte[64]))
                {
                    try { ImageLoader.BitmapReader(stream); }
                    catch (Exception) { rejected = true; }
                }
                Require(rejected, "Invalid image accepted.");
            });
            Check(results, log, "IFF REFLECTION AND BCON", () => {
                var iff = new IffFile();
                using (var stream = new MemoryStream(IffFixture())) iff.Read(stream);
                var chunk = iff.Get<BCON>(513);
                Require(chunk != null && chunk.ChunkID == 513 && chunk.ChunkFlags == 1 &&
                    chunk.ChunkLabel == "SYNTHETIC" && chunk.Flags == 7, "IFF metadata mismatch.");
                Require(chunk.Constants.SequenceEqual(new ushort[] { 0, 0x1234, 0xffff }),
                    "BCON values/byte order mismatch.");
                Require(object.ReferenceEquals(chunk, iff.Get<BCON>(513)), "Lazy chunk cache changed.");
                Require(iff.Get<BCON>(99) == null, "Missing chunk should be null.");
            });
            Check(results, log, "INVALID IFF REJECTED", () => {
                bool rejected = false;
                using (var stream = new MemoryStream(new byte[64]))
                {
                    try { new IffFile().Read(stream); }
                    catch (Exception) { rejected = true; }
                }
                Require(rejected, "Invalid IFF accepted.");
            });
            if (device != null)
            {
                Check(results, log, "BMP THREE COLOR KEYS", () => {
                    byte[] keyed = { 255,0,255,255, 254,2,254,255, 255,1,255,255, 17,34,51,255 };
                    byte[] expected = { 0,0,0,0, 0,0,0,0, 0,0,0,0, 17,34,51,255 };
                    Texture(device, Bmp(keyed), expected, 0);
                });
                Check(results, log, "PNG TEXTURE STRAIGHT", () =>
                    Texture(device, Convert.FromBase64String(Png), PngRgba, 0));
                Check(results, log, "PNG TEXTURE PREMULTIPLY", () => {
                    var expected = (byte[])PngRgba.Clone();
                    expected[4] = 16; expected[5] = 32; expected[6] = 48;
                    Texture(device, Convert.FromBase64String(Png), expected, 1);
                });
                Check(results, log, "PNG TEXTURE UNPREMULTIPLY", () => {
                    var expected = (byte[])PngRgba.Clone();
                    expected[4] = 63; expected[5] = 127; expected[6] = 191;
                    Texture(device, Convert.FromBase64String(Png), expected, -1);
                });
            }
            log("RESULT " + results.Count(x => x.StartsWith("PASS ")) + "/" + results.Count + " PASS");
            return results;
        }

        private static void Check(List<string> results, Action<string> log, string name, Action test)
        {
            try { test(); results.Add("PASS " + name); log("PASS " + name); }
            catch (Exception ex) { results.Add("FAIL " + name); log("FAIL " + name + " " + ex); }
        }

        private static void Decode(byte[] file, byte[] expected)
        {
            using (var stream = new MemoryStream(file))
            {
                var decoded = ImageLoader.BitmapReader(stream);
                Require(decoded.Item2 == 2 && decoded.Item3 == 2, "Dimensions mismatch.");
                Bytes(decoded.Item1, expected);
            }
        }

        private static void Texture(GraphicsDevice device, byte[] file, byte[] expected, int premultiply)
        {
            int previous = ImageLoader.PremultiplyPNG;
            try
            {
                ImageLoader.PremultiplyPNG = premultiply;
                using (var stream = new MemoryStream(file))
                using (var texture = ImageLoader.FromStream(device, stream))
                {
                    Require(texture != null && texture.Width == 2 && texture.Height == 2, "Texture missing/wrong size.");
                    var actual = new byte[16];
                    texture.GetData(actual);
                    Bytes(actual, expected);
                }
            }
            finally { ImageLoader.PremultiplyPNG = previous; }
        }

        private static void Bytes(byte[] actual, byte[] expected)
        {
            Require(actual.Length == expected.Length, "Pixel byte count mismatch.");
            for (int i = 0; i < actual.Length; i++)
                Require(actual[i] == expected[i], "Pixel byte " + i + ": expected " + expected[i] + ", got " + actual[i]);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }

        // Uncompressed 24-bit BMP, bottom-up rows, two bytes of padding per row.
        private static byte[] Bmp(byte[] rgba)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)'B'); writer.Write((byte)'M'); writer.Write(70);
                writer.Write(0); writer.Write(54); writer.Write(40);
                writer.Write(2); writer.Write(2); writer.Write((ushort)1); writer.Write((ushort)24);
                writer.Write(0); writer.Write(16); writer.Write(0); writer.Write(0);
                writer.Write(0); writer.Write(0);
                for (int row = 1; row >= 0; row--)
                {
                    for (int col = 0; col < 2; col++)
                    {
                        int i = (row * 2 + col) * 4;
                        writer.Write(rgba[i + 2]); writer.Write(rgba[i + 1]); writer.Write(rgba[i]);
                    }
                    writer.Write((ushort)0);
                }
                return stream.ToArray();
            }
        }

        private static byte[] IffFixture()
        {
            var file = new byte[148]; // 64-byte file header + 76-byte chunk header + 8-byte payload.
            Encoding.ASCII.GetBytes("IFF FILE 2.5:TYPE FOLLOWED BY SIZE JAMIE DOORNBOS & MAXIS 1").CopyTo(file, 0);
            Encoding.ASCII.GetBytes("BCON").CopyTo(file, 64);
            file[71] = 84; // Chunk length, big endian.
            file[72] = 2; file[73] = 1; // ID 513.
            file[75] = 1;
            Encoding.ASCII.GetBytes("SYNTHETIC").CopyTo(file, 76);
            byte[] body = { 3, 7, 0, 0, 0x34, 0x12, 0xff, 0xff };
            body.CopyTo(file, 140);
            return file;
        }
    }
}