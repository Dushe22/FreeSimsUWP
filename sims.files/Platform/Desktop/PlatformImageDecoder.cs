/*
 * This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
 * If a copy of the MPL was not distributed with this file, You can obtain one at
 * http://mozilla.org/MPL/2.0/. 
 */

using System;
using System.IO;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace FSO.Files
{
    internal sealed class PlatformImageDecoder : IImageDecoder
    {
        public Tuple<byte[], int, int> Decode(Stream str)
        {
            System.Drawing.Bitmap image = (System.Drawing.Bitmap)System.Drawing.Bitmap.FromStream(str);
            try
            {
                // Fix up the Image to match the expected format
                //image = (Bitmap)image.RGBToBGR();

                var data = new byte[image.Width * image.Height * 4];

                BitmapData bitmapData = image.LockBits(new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                    ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                if (bitmapData.Stride != image.Width * 4)
                    throw new NotImplementedException();
                Marshal.Copy(bitmapData.Scan0, data, 0, data.Length);
                image.UnlockBits(bitmapData);

                for (int i = 0; i < data.Length; i += 4)
                {
                    var temp = data[i];
                    data[i] = data[i + 2];
                    data[i + 2] = temp;
                }

                return new Tuple<byte[], int, int>(data, image.Width, image.Height);
            }
            finally
            {
                image.Dispose();
            }
        }

    }
}
