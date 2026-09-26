using System;
using System.Collections.Generic;
using FSO.SimAntics.NetPlay.Model;
using FSO.SimAntics.Engine.TSOTransaction;
namespace FSO.SimAntics.NetPlay.Drivers
{
    /// <summary>Local authoritative queue, without sockets or command serialization.</summary>
    public sealed class VMOfflineDriver : VMNetDriver
    {
        private readonly object gate = new object();
        private readonly List<VMNetCommandBodyAbstract> queued = new List<VMNetCommandBodyAbstract>();
        private bool closed;
        public override bool IsAuthoritative { get { return true; } }
        public VMOfflineDriver() { GlobalLink = new VMTSOGlobalLinkStub(); }
        public override void SendCommand(VMNetCommandBodyAbstract command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (command.FromNet) throw new InvalidOperationException("Offline commands must originate locally.");
            lock (gate)
            {
                if (closed) throw new ObjectDisposedException(nameof(VMOfflineDriver));
                queued.Add(command);
            }
        }
        public override bool Tick(VM vm)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));
            VMNetCommandBodyAbstract[] batch;
            lock (gate)
            {
                if (closed) return false;
                batch = queued.ToArray();
                queued.Clear();
            }
            // Reentrant commands from Verify/Execute belong to the next tick.
            var verified = new List<VMNetCommandBodyAbstract>();
            foreach (var command in batch)
                if (command.Verify(vm, vm.GetObjectByPersist(command.ActorUID) as VMAvatar)) verified.Add(command);
            foreach (var command in verified)
            {
                lock (gate) { if (closed) return false; }
                command.Execute(vm, vm.GetObjectByPersist(command.ActorUID) as VMAvatar);
            }
            lock (gate) { if (closed) return false; }
            if (vm.Context.Ready) vm.InternalTick();
            return true;
        }
        public override string GetUserIP(uint uid) { return "local"; }
        public override void CloseNet() { lock (gate) { closed = true; queued.Clear(); } }
    }
}
