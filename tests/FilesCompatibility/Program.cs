using System;
using System.Linq;
namespace FreeSims.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            return FilesCompatibilityTests.Run(Console.WriteLine).All(x => x.StartsWith("PASS ")) ? 0 : 1;
        }
    }
}