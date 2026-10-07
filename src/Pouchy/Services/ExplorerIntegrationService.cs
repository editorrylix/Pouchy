using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Pouchy.Services
{
    /// <summary>
    /// Adds "Add to Pouchy" to the right-click menu of files and folders in Explorer, and a Pouchy entry to "Send to".
    /// Everything is per user (HKCU and the user's SendTo folder), so no admin rights are needed.
    /// On Windows 11 both live under "Show more options", because the new menu only takes packaged apps.
    /// </summary>
    public sealed class ExplorerIntegrationService
    {
        private static readonly string[] Targets = { "*", "Directory" };

        private readonly string _classesRoot;
        private readonly string _sendToFolder;
        private readonly string _exePath;
        private readonly string _iconPath;
        private readonly string? _profile;

        /// <param name="classesRoot">Registry path under HKCU; tests point this somewhere harmless.</param>
        public ExplorerIntegrationService(string? profile = null, string classesRoot = @"Software\Classes",
            string? sendToFolder = null, string? exePath = null)
        {
            _profile = profile;
            _classesRoot = classesRoot;
            _sendToFolder = sendToFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
            // The Store version is started through its app alias, which survives updates; its icon is
            // read from the installed exe (refreshed on start when an update moves it).
            _exePath = exePath ?? PackageInfo.LaunchPath;
            _iconPath = exePath ?? Environment.ProcessPath ?? _exePath;
        }

        private string VerbName => _profile == null ? "Pouchy" : "Pouchy." + _profile;
        private string MenuText => _profile == null ? "Add to Pouchy" : $"Add to Pouchy ({_profile})";
        private string ShortcutPath => Path.Combine(_sendToFolder, (_profile == null ? "Pouchy" : $"Pouchy ({_profile})") + ".lnk");
        private string ProfileArgs => _profile == null ? "" : $"--profile {_profile} ";
        internal string MenuCommand => $"\"{_exePath}\" {ProfileArgs}--add \"%1\"";
        private string IconValue => $"\"{_iconPath}\",0";

        private string VerbKey(string target) => $@"{_classesRoot}\{target}\shell\{VerbName}";

        public bool IsEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(VerbKey("*") + @"\command");
                    return key != null;
                }
                catch (Exception ex)
                {
                    Logger.Log("Could not read the Explorer menu entry: " + ex.Message);
                    return false;
                }
            }
        }

        public void SetEnabled(bool enabled)
        {
            try
            {
                foreach (string target in Targets)
                {
                    if (enabled)
                    {
                        using var verb = Registry.CurrentUser.CreateSubKey(VerbKey(target));
                        verb.SetValue("", MenuText);
                        verb.SetValue("Icon", IconValue);
                        verb.SetValue("MultiSelectModel", "Player"); // Otherwise the entry disappears when more than 15 items are selected.
                        using var command = verb.CreateSubKey("command");
                        command.SetValue("", MenuCommand);
                    }
                    else
                    {
                        Registry.CurrentUser.DeleteSubKeyTree(VerbKey(target), throwOnMissingSubKey: false);
                    }
                }

                if (enabled) CreateSendToShortcut();
                else if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);

                Logger.Log($"Explorer integration set to {enabled}.");
            }
            catch (Exception ex)
            {
                Logger.Log("Could not update the Explorer integration: " + ex.Message);
            }
        }

        /// <summary>If the entries exist but the exe has moved, point them at the current exe.</summary>
        public void RefreshPathIfEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(VerbKey("*") + @"\command");
                using var verb = Registry.CurrentUser.OpenSubKey(VerbKey("*"));
                if (key?.GetValue("") is string value &&
                    (!string.Equals(value, MenuCommand, StringComparison.OrdinalIgnoreCase) || !File.Exists(ShortcutPath) ||
                     !string.Equals(verb?.GetValue("Icon") as string, IconValue, StringComparison.OrdinalIgnoreCase)))
                {
                    SetEnabled(true);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Could not refresh the Explorer integration: " + ex.Message);
            }
        }

        /// <summary>Send to passes every selected file to one launch, unlike the right-click entry.</summary>
        private void CreateSendToShortcut()
        {
            var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell isn't available.");
            object shell = Activator.CreateInstance(type)!;
            object? link = null;
            try
            {
                Directory.CreateDirectory(_sendToFolder);
                link = ((dynamic)shell).CreateShortcut(ShortcutPath);
                dynamic d = link!;
                d.TargetPath = _exePath;
                d.Arguments = (ProfileArgs + "--add").Trim();
                d.IconLocation = _iconPath + ",0";
                d.Description = "Put the selected items in your pouch";
                d.Save();
            }
            finally
            {
                if (link != null) Marshal.FinalReleaseComObject(link);
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
