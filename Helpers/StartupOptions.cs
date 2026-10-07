using System.IO;

namespace Pouchy.Helpers
{
    /// <summary>
    /// Command-line options.
    /// <c>--add PATH...</c> (or bare paths, e.g. files dropped on Pouchy.exe) adds those files to the pouch;
    /// it's what "Add to Pouchy" in Explorer runs.
    /// Developer options: <c>--profile NAME</c> runs a separate copy with its own data folder,
    /// <c>--show</c> opens the pouch at startup, <c>--tray-menu</c> opens the tray menu at startup.
    /// </summary>
    public sealed record StartupOptions(string? Profile, bool ShowPouch, bool ShowTrayMenu)
    {
        /// <summary>Files and folders to add to the pouch.</summary>
        public IReadOnlyList<string> Paths { get; init; } = Array.Empty<string>();

        public static StartupOptions Parse(IReadOnlyList<string> args)
        {
            string? profile = null;
            bool show = false, trayMenu = false, adding = false;
            var paths = new List<string>();
            for (int i = 0; i < args.Count; i++)
            {
                // After --add everything is a path, even if it looks like an option.
                if (adding)
                {
                    if (args[i].Length > 0) paths.Add(args[i]);
                    continue;
                }

                switch (args[i].ToLowerInvariant())
                {
                    case "--profile" when i + 1 < args.Count:
                        string name = new string(args[++i].Where(char.IsLetterOrDigit).ToArray());
                        if (name.Length > 0) profile = name;
                        break;
                    case "--show":
                        show = true;
                        break;
                    case "--tray-menu":
                        trayMenu = true;
                        break;
                    case "--add":
                        adding = true;
                        break;
                    default:
                        // A bare argument counts only if it exists, e.g. files dropped on Pouchy.exe. Unknown options are ignored.
                        string arg = args[i];
                        if (!arg.StartsWith('-') && (File.Exists(arg) || Directory.Exists(arg))) paths.Add(arg);
                        break;
                }
            }
            return new StartupOptions(profile, show, trayMenu) { Paths = paths };
        }
    }
}
