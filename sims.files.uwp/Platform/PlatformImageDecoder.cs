using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace FSO.Files
{
    internal sealed class PlatformImageDecoder : IImageDecoder
    {
        public Tuple<byte[], int, int> Decode(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            // Existing asset loaders are synchronous. Every await below avoids
            // capturing the game/UI context so this bridge cannot wait on itself.
            return DecodeAsync(stream).GetAwaiter().GetResult();
        }

        private static async Task<Tuple<byte[], int, int>> DecodeAsync(Stream stream)
        {
            using (var input = new InMemoryRandomAccessStream())
            using (var writer = input.AsStreamForWrite())
            {
                await stream.CopyToAsync(writer).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
                input.Seek(0);
                var decoder = await BitmapDecoder.CreateAsync(input).AsTask().ConfigureAwait(false);
                var pixels = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight,
                    new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage).AsTask().ConfigureAwait(false);
                return Tuple.Create(pixels.DetachPixelData(),
                    checked((int)decoder.PixelWidth), checked((int)decoder.PixelHeight));
            }
        }
    }
}