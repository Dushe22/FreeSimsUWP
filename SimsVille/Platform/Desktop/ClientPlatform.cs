/*
 * This Source Code Form is subject to the terms of the Mozilla Public License,
 * v. 2.0. http://mozilla.org/MPL/2.0/.
 * Desktop debugger behavior extracted from UI/Screens/CoreGameScreen.cs.
 */
namespace FSO.Client.Platform
{
    public static partial class ClientPlatform
    {
        static partial void Initialize()
        {
            ShowVmDebugger = (vm, window) => {
                var debugTools = new FSO.Debug.Simantics(vm);
                debugTools.Show();
                debugTools.Location = new System.Drawing.Point(
                    window.ClientBounds.X + window.ClientBounds.Width, window.ClientBounds.Y);
                debugTools.UpdateAQLocation();
            };
        }
    }
}
