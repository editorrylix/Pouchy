using System.IO;

namespace Pouchy.Services
{
    public static class AppPaths
    {
        public static readonly string DataFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pouchy");

        public static string SettingsFile => Path.Combine(DataFolder, "settings.json");
        public static string LegacyBlacklistFile => Path.Combine(DataFolder, "blacklist.json");
        public static string LogFile => Path.Combine(DataFolder, "debug.log");
    }
}
