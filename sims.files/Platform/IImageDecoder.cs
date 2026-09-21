using System;
using System.IO;

namespace FSO.Files
{
    /// <summary>
    /// Decodes from the current stream position to top-down, tightly packed RGBA8
    /// with straight alpha. Does not close the caller's stream, apply color keys,
    /// premultiply alpha, rotate by EXIF, or change the embedded color space.
    /// </summary>
    public interface IImageDecoder
    {
        Tuple<byte[], int, int> Decode(Stream stream);
    }
}