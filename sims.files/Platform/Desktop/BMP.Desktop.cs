/*
 * This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
 * If a copy of the MPL was not distributed with this file, You can obtain one at
 * http://mozilla.org/MPL/2.0/. 
 */

using System.Drawing;
using System.IO;

namespace FSO.Files.Formats.IFF.Chunks
{
    // Keep the desktop debugger's API out of the UWP assembly.
    public partial class BMP
    {
        public Image GetBitmap()
        {
            return Bitmap.FromStream(new MemoryStream(data));
        }
    }
}