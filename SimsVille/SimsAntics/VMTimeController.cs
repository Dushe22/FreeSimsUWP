using System;

namespace FSO.SimAntics
{
    public enum VMTimeSpeed { Paused, Normal, Fast, Ultra }

    /// <summary>
    /// Shared fixed-tick pacing for local time. Callers consume the returned ticks
    /// with a preview clock or, when live simulation is enabled, the offline VM.
    /// No VM, game content, callbacks or graphics resources are retained.
    /// </summary>
    public sealed class VMTimeController
    {
        public const int TicksPerSecond = 30;
        public const int TS1TicksPerMinute = 30;
        public const double MaxFrameSeconds = 0.25;
        private double fraction;
        private bool skipNext;
        private VMTimeSpeed resumeSpeed = VMTimeSpeed.Normal;
        public VMTimeSpeed Speed { get; private set; }
        public int Multiplier {
            get {
                switch (Speed) {
                    case VMTimeSpeed.Normal: return 1;
                    case VMTimeSpeed.Fast: return 3;
                    case VMTimeSpeed.Ultra: return 10;
                    default: return 0;
                }
            }
        }

        public void SetSpeed(VMTimeSpeed speed)
        {
            if (speed < VMTimeSpeed.Paused || speed > VMTimeSpeed.Ultra)
                throw new ArgumentOutOfRangeException(nameof(speed));
            Speed = speed;
            if (speed != VMTimeSpeed.Paused) resumeSpeed = speed;
        }

        public void ChangeSpeed(int direction)
        {
            SetSpeed((VMTimeSpeed)Math.Max(0, Math.Min(3, (int)Speed + Math.Sign(direction))));
        }

        public void TogglePause()
        {
            SetSpeed(Speed == VMTimeSpeed.Paused ? resumeSpeed : VMTimeSpeed.Paused);
        }

        /// <summary>Discard elapsed time crossing a load, test, suspension or disconnect.</summary>
        public void Suspend()
        {
            skipNext = true;
        }

        public int Advance(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (skipNext) { skipNext = false; return 0; }
            if (Speed == VMTimeSpeed.Paused) return 0;
            // Bound catch-up work on a slow frame; never accumulate a backlog.
            double ticks = fraction + Math.Min(elapsedSeconds, MaxFrameSeconds) * TicksPerSecond * Multiplier;
            int count = (int)Math.Floor(ticks + 1e-9);
            fraction = Math.Max(0, ticks - count);
            return count;
        }
    }
}
