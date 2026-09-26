using System;
using GonzoNet;
namespace FSO.SimAntics.NetPlay
{
    public abstract class VMNetworkDriver : VMNetDriver
    {
        public abstract void OnPacket(NetworkClient client, ProcessedPacket packet);
        public static void Dispatch(VM vm, NetworkClient client, ProcessedPacket packet)
        {
            var network = vm.Driver as VMNetworkDriver;
            if (network == null) throw new InvalidOperationException("This VM has no network transport.");
            network.OnPacket(client, packet);
        }
    }
}
