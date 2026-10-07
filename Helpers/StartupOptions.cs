using System.IO;

namespace Pouchy.Helpers
{
    /// <summary>
    /// Command-line options.
    /// <c>--add PATH...</c> (or bare paths, e.g. files dropped on Pouchy.exe) adds those files to the pouch;
    /// it's what "Add to Pouchy" in Explorer runs.
    /// <c>--cleanup</c> removes Pouchy's registry entries and exits (the uninstaller runs it).
    /// <c>--wait-for PID</c> and <c>--updated</c> are used when Pouchy restarts itself after an update.
    /// Developer options: <c>--profile NAME</c> runs a separate copy with its own data folder,
    /// <c>--show</c> opens the pouch at startup, <c>--tray-menu</c> opens the tray menu at startup,
    /// <c>--palette</c> opens the command palette, <c>--update-now</c> installs an available update.
    /// </summary>
    public sealed record StartupOptions(string? Profile, bool ShowPouch, bool ShowTrayMenu)
    {
        /// <summary>Files and folders to add to the pouch.</summary>
        public IReadOnlyList<string> Paths { get; init; } = Array.Empty<string>();

        public bool Cleanup { get; init; }
        public int? WaitForProcess { get; init; }
        public bool Updated { get; init; }
        public bool ShowPalette { get; init; }
        public bool UpdateNow { get; init; }

        public static StartupOptions Parse(IReadOnlyList<string> args)
        {
            string? profile = null;
            bool show = false, trayMenu = false, adding = false, cleanup = false, updated = false, palette = false, updateNow = false;
            int? waitFor = null;
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
                    case "--palette":
                        palette = true;
                        break;
                    case "--cleanup":
                        cleanup = true;
                        break;
                    case "--updated":
                        updated = true;
                        break;
                    case "--update-now":
                        updateNow = true;
                        break;
                    case "--wait-for" when i + 1 < args.Count:
                        if (int.TryParse(args[++i], out int pid)) waitFor = pid;
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
            return new StartupOptions(profile, show, trayMenu)
            {
                Paths = paths,
                Cleanup = cleanup,
                WaitForProcess = waitFor,
                Updated = updated,
                ShowPalette = palette,
                UpdateNow = updateNow,
            };
        }
    }
}
