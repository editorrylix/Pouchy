namespace Pouchy.Helpers
{
    /// <summary>
    /// Developer command-line options:
    /// <c>--profile NAME</c> runs a separate copy with its own data folder,
    /// <c>--show</c> opens the pouch at startup, <c>--tray-menu</c> opens the tray menu at startup.
    /// </summary>
    public sealed record StartupOptions(string? Profile, bool ShowPouch, bool ShowTrayMenu)
    {
        public static StartupOptions Parse(IReadOnlyList<string> args)
        {
            string? profile = null;
            bool show = false, trayMenu = false;
            for (int i = 0; i < args.Count; i++)
            {
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
                }
            }
            return new StartupOptions(profile, show, trayMenu);
        }
    }
}
