using System.IO;
using System.Runtime.InteropServices;

namespace Pouchy.Services
{
    /// <summary>
    /// Whether Pouchy runs as an MSIX package (the Microsoft Store version) or as the plain .exe
    /// (installer or portable zip). A few things work differently in a package:
    /// the install folder changes with every update, so Windows-facing paths use the app alias,
    /// starting with Windows goes through the package's startup task, and updates come from the Store.
    /// </summary>
    public static class PackageInfo
    {
        /// <summary>The startup task declared in packaging/Package.appxmanifest.</summary>
        public const string StartupTaskId = "PouchyStartup";

        /// <summary>The app execution alias declared in the manifest.</summary>
        public const string AliasName = "pouchy.exe";

        private const int AppModelErrorNoPackage = 15700;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

        private static readonly Lazy<bool> Packaged = new(() =>
        {
            try
            {
                int length = 0;
                return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        });

        public static bool IsPackaged => Packaged.Value;

        /// <summary>
        /// The path other programs (Explorer, Send to) should start Pouchy with. In a package this is the
        /// app alias in %LocalAppData%\Microsoft\WindowsApps, which keeps working across Store updates.
        /// </summary>
        public static string LaunchPath => IsPackaged
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", AliasName)
            : Environment.ProcessPath ?? "Pouchy.exe";
    }
}
