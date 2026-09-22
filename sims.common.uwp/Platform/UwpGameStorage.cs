using System.IO;
using FSO.Common.Platform;
using Windows.ApplicationModel;
using Windows.Storage;

namespace FSO.Common.Platform.Uwp
{
    public static class UwpGameStorage
    {
        public static GamePaths CreatePaths()
        {
            string local = ApplicationData.Current.LocalFolder.Path;
            var paths = new GamePaths(Path.Combine(Package.Current.InstalledLocation.Path,"Content"),
                Path.Combine(local,"GameData"), Path.Combine(local,"UserData"));
            Directory.CreateDirectory(paths.GameDataRoot);
            Directory.CreateDirectory(paths.UserDataRoot);
            return paths;
        }
    }
}
