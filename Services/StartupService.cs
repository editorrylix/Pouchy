using Microsoft.Win32;

namespace Pouchy.Services
{
    /// <summary>Manages the HKCU Run key that starts Pouchy with Windows.</summary>
    public sealed class StartupService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Pouchy";

        private static string Command => $"\"{Environment.ProcessPath}\"";

        public bool IsEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                    return key?.GetValue(ValueName) is string;
                }
                catch (Exception ex)
                {
                    Logger.Log("Could not read startup registry key: " + ex.Message);
                    return false;
                }
            }
        }

        public void SetEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
                if (enabled)
                {
                    key.SetValue(ValueName, Command);
                }
                else
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                }
                Logger.Log($"Run at startup set to {enabled}.");
            }
            catch (Exception ex)
            {
                Logger.Log("Could not update startup registry key: " + ex.Message);
            }
        }

        /// <summary>If startup is enabled but the exe has moved, point the entry at the current exe.</summary>
        public void RefreshPathIfEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                if (key?.GetValue(ValueName) is string value &&
                    !string.Equals(value, Command, StringComparison.OrdinalIgnoreCase))
                {
                    SetEnabled(true);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Could not refresh startup registry key: " + ex.Message);
            }
        }
    }
}
