using System;
using System.Linq;
using FSO.Content;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;

namespace FSO.SimAntics
{
    /// <summary>Bounded headless exercise of selected TS1 mains, not full gameplay.
    /// Call on the simulation thread. A successful reload starts a fresh paused session.
    /// No background catch-up, save serialization or automatic resume.</summary>
    public sealed class TS1SimulationController : IDisposable
    {
        public const int TickLimit = 6000;
        public const int TicksPerSecond = 30;
        // Compatibility gate shared by headless trials and the live renderer.
        // Resource families are validated by TS1BehaviorTests, never house IDs.
        private static readonly System.Collections.Generic.HashSet<string> controlledResources=
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "FlowersOutdoor.iff","Shrubs.iff","ChairsHD.iff","lampceiling.iff","WallLite.iff",
                "TablesHD.iff","Sinks.iff","PlantsHangingHD.iff","FishTankBig.iff"
            };
        public static bool SupportsControlledBehavior(string resource){return controlledResources.Contains(resource);}
        private TS1LotObjectSession session;
        private long accumulated;
        private bool disposed;
        public VM VM { get { return session == null ? null : session.VM; } }
        public int House { get; private set; }
        public int ActiveObjects { get; private set; }
        public int HeldObjects { get; private set; }
        public int CompletedTicks { get; private set; }
        public bool Running { get; private set; }
        public Exception Fault { get; private set; }
        public bool LimitReached { get { return CompletedTicks >= TickLimit; } }

        public void Load(IffFile lot, TS1ObjectProvider content, int house)
        {
            EnsureAlive();
            if (house != 2 && house != 28) throw new ArgumentOutOfRangeException("house");
            // Construct before replacing: failure leaves the current VM and controls intact.
            var candidate = TS1LotObjectSession.Load(lot, content);
            try {
                var selected = candidate.VM.Entities.Where(e => SupportsControlledBehavior(e.Object.Resource.Name)).ToArray();
                if (selected.Length == 0) throw new InvalidOperationException("No supported objects in this lot.");
                candidate.ConfigureLiveClock();
                candidate.VM.Context.RandomSeed = 12345;
                foreach (var entity in selected) candidate.RestartMain(entity.ObjectID);
                var previous = session;
                session = candidate;
                House = house; ActiveObjects = selected.Length;
                HeldObjects = candidate.VM.Entities.Count - selected.Length;
                CompletedTicks = 0; Fault = null; Running = false; accumulated = 0;
                if (previous != null) previous.Dispose();
            } catch { candidate.Dispose(); throw; }
        }
        public void Pause() { EnsureAlive(); Running = false; accumulated = 0; }
        public void Run()
        {
            EnsureReady();
            if (Fault != null || VM.ScriptExecutionStopped || LimitReached)
                throw new InvalidOperationException("Reload the lot before running again.", Fault ?? VM.ScriptFault);
            Running = true;
        }
        public void Step()
        {
            EnsureReady();
            if (Running) throw new InvalidOperationException("Pause before stepping.");
            TickOnce();
        }
        public void Advance(TimeSpan elapsed, bool active)
        {
            EnsureReady();
            if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException("elapsed");
            if (!active) { Pause(); return; }
            if (!Running) return;
            // At most five ticks per update. Discard time spent stalled/backgrounded.
            accumulated += Math.Min(elapsed.Ticks, TimeSpan.TicksPerSecond / 6) * TicksPerSecond;
            int count = Math.Min(5, (int)(accumulated / TimeSpan.TicksPerSecond));
            accumulated %= TimeSpan.TicksPerSecond;
            for (int i = 0; i < count && Running; i++) TickOnce();
        }
        private void TickOnce()
        {
            if (Fault != null || VM.ScriptExecutionStopped || LimitReached)
                throw new InvalidOperationException("Reload the stopped lot before stepping.", Fault ?? VM.ScriptFault);
            try {
                session.Tick();
                CompletedTicks++;
                if (LimitReached) Pause();
            } catch (Exception error) { Fault = error; Pause(); throw; }
        }
        internal void CheckpointReady() { EnsureReady(); if (Fault != null || VM.ScriptExecutionStopped) throw new InvalidOperationException("Cannot checkpoint a faulted session."); }
        private void EnsureAlive() { if (disposed) throw new ObjectDisposedException("TS1SimulationController"); }
        private void EnsureReady() { EnsureAlive(); if (session == null) throw new InvalidOperationException("Load a lot first."); }
        public void Dispose() { if (!disposed) { if (session != null) session.Dispose(); Running = false; disposed = true; } }
    }
}
