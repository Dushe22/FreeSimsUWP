using System;
using System.IO;
using System.Linq;
using FSO.Files;

namespace FreeSims.Tests
{
    public static class ThumbnailColorKeyTests
    {
        public static void Validate()
        {
            byte[] pixels = {
                255,0,255,255, 254,2,254,255, 255,1,255,255, 248,0,248,255,
                248,0,247,255, 247,0,248,255, 248,1,248,255, 248,0,248,128,
                17,34,51,255, 0,0,0,0
            };
            byte[] expected = {
                0,0,0,0, 0,0,0,0, 0,0,0,0, 0,0,0,0,
                248,0,247,255, 247,0,248,255, 248,1,248,255, 248,0,248,128,
                17,34,51,255, 0,0,0,0
            };
            if (ImageLoader.ApplyBitmapColorKey(pixels) != 4 || !pixels.SequenceEqual(expected))
                throw new InvalidDataException("BMP color-key output or neighboring colors changed.");
            if (ImageLoader.ApplyBitmapColorKey(pixels) != 0)
                throw new InvalidDataException("Color-key operation is not repeatable.");
            bool rejected = false;
            try { ImageLoader.ApplyBitmapColorKey(new byte[3]); } catch (ArgumentException) { rejected = true; }
            if (!rejected) throw new InvalidDataException("Partial RGBA pixel accepted.");
        }
    }
}