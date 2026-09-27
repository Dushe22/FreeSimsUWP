using System;
using MonoGame.Framework;
using Windows.ApplicationModel.Core;

namespace FreeSims.Xbox.Proof
{
    public static class Program
    {
        private static int pauseRequested;
        internal static void RequestPause() { System.Threading.Interlocked.Exchange(ref pauseRequested, 1); }
        internal static bool ConsumePause() { return System.Threading.Interlocked.Exchange(ref pauseRequested, 0) != 0; }
        private static void Main()
        {
            ProofLog.Write("START OFFLINE PROBE commit=" + BuildInfo.Commit + " platform=x64 configuration=Release");
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                ProofLog.Write("UNHANDLED " + args.ExceptionObject);
            CoreApplication.Suspending += (sender, args) => { RequestPause(); ProofLog.Write("SUSPENDING - LIVE SESSION NOT SAVED"); };
            CoreApplication.Resuming += (sender, args) => ProofLog.Write("RESUMING");
            try
            {
                CoreApplication.Run(new GameFrameworkViewSource<OfflineProbeGame>());
            }
            catch (Exception ex)
            {
                ProofLog.Write("FATAL " + ex);
                throw;
            }
        }
    }
}
