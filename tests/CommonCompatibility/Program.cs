using System;
using System.IO;
using System.Linq;
using FSO.Common.Platform;

namespace FreeSims.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            string root = Path.Combine(Path.GetTempPath(),"FreeSimsCommonCompatibility");
            var paths = new GamePaths(Path.Combine(root,"Content"),Path.Combine(root,"GameData"),Path.Combine(root,"UserData"));
            var results = CommonCompatibilityTests.Run(paths,Console.WriteLine);
            results.AddRange(ClientSettingsTests.Run(paths.GetUserDataPath("ClientSettings"), Console.WriteLine));
            Console.WriteLine("RESULT " + results.Count(x => x.StartsWith("PASS ")) + "/" + results.Count + " PASS");
            return results.All(x => x.StartsWith("PASS ")) ? 0 : 1;
        }
    }
}
