using System.IO;

namespace Pouchy.Services
{
    public static class AppPaths
    {
        private static readonly string Root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public static string DataFolder { get; private set; } = Path.Combine(Root, "Pouchy");

        /// <summary>Name of the developer profile in use, or null for the normal one.</summary>
        public static string? Profile { get; private set; }

        /// <summary>
        /// Developer option (--profile NAME): keeps settings and items in %AppData%\Pouchy-NAME so a
        /// test copy can run next to the normal one. Call before any service is created.
        /// </summary>
        public static void UseProfile(string name)
        {
            Profile = name;
            DataFolder = Path.Combine(Root, "Pouchy-" + name);
        }

        public static string SettingsFile => Path.Combine(DataFolder, "settings.json");
        public static string LegacyBlacklistFile => Path.Combine(DataFolder, "blacklist.json");
        public static string ThemesFolder => Path.Combine(DataFolder, "themes");
        public static string LogFile => Path.Combine(DataFolder, "debug.log");
        /// <summary>Scripts that show up as drop actions.</summary>
        public static string ActionsFolder => Path.Combine(DataFolder, "actions");
        /// <summary>WAV files for the Custom sound pack.</summary>
        public static string SoundsFolder => Path.Combine(DataFolder, "sounds");
    }
}
