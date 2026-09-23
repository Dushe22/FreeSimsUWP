using System;
using MonoGame.Framework;
using Windows.ApplicationModel.Core;

namespace FreeSims.Xbox.Proof
{
    public static class Program
    {
        private static void Main()
        {
            ProofLog.Write("START CONTENT PROBE commit=" + BuildInfo.Commit + " platform=x64 configuration=Release");
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                ProofLog.Write("UNHANDLED " + args.ExceptionObject);
            CoreApplication.Suspending += (sender, args) => ProofLog.Write("SUSPENDING");
            CoreApplication.Resuming += (sender, args) => ProofLog.Write("RESUMING");
            try
            {
                CoreApplication.Run(new GameFrameworkViewSource<ContentProbeGame>());
            }
            catch (Exception ex)
            {
                ProofLog.Write("FATAL " + ex);
                throw;
            }
        }
    }
}
