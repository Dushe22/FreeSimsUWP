using System;
using System.Linq;
namespace FreeSims.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            bool filesPassed = FilesCompatibilityTests.Run(Console.WriteLine).All(x => x.StartsWith("PASS "));
            bool layoutPassed = ProbeLayoutTests.Run(Console.WriteLine);
            return filesPassed && layoutPassed ? 0 : 1;
        }
    }
}