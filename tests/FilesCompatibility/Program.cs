using System;
using System.Linq;
namespace FreeSims.Tests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 2) return TS1ContentTests.Run(new FSO.Common.Platform.GamePaths(System.IO.Path.Combine(args[1], "Content"), args[0], System.IO.Path.Combine(args[1], "UserData")), Console.WriteLine).All(x => x.StartsWith("PASS ")) ? 0 : 1;
            bool filesPassed = FilesCompatibilityTests.Run(Console.WriteLine).All(x => x.StartsWith("PASS "));
            bool layoutPassed = ProbeLayoutTests.Run(Console.WriteLine);
            bool parserPassed = TS1ParserTests.Run(Console.WriteLine).All(x => x.StartsWith("PASS "));
            return filesPassed && layoutPassed && parserPassed ? 0 : 1;
        }
    }
}