using System;
using Microsoft.Xna.Framework;
using FSO.SimAntics;

namespace FSO.Client.Platform
{
    // Optional desktop tooling. A host with no debugger keeps the action absent.
    public static partial class ClientPlatform
    {
        public static Action<VM, GameWindow> ShowVmDebugger;
        static ClientPlatform() { Initialize(); }
        static partial void Initialize();
    }
}
