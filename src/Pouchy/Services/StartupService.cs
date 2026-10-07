using Microsoft.Win32;

namespace Pouchy.Services
{
    /// <summary>
    /// Starts Pouchy with Windows: the HKCU Run key for the plain .exe, or the package's startup task
    /// for the Microsoft Store version (its install folder changes with every update, and the startup
    /// task also shows up in Task Manager → Startup apps).
    /// </summary>
    public sealed class StartupService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Pouchy";

        private static string Command => $"\"{Environment.ProcessPath}\"";

        public bool IsEnabled
        {
            get
            {
                if (PackageInfo.IsPackaged) return PackagedStartup.IsEnabled();
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
            if (PackageInfo.IsPackaged)
            {
                PackagedStartup.SetEnabled(enabled);
                return;
            }
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
            if (PackageInfo.IsPackaged) return; // The startup task always starts the installed version.
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

        /// <summary>The package's startup task (Windows.ApplicationModel.StartupTask). Only valid inside a package.</summary>
        private static class PackagedStartup
        {
            public static bool IsEnabled()
            {
                try
                {
                    var task = Windows.ApplicationModel.StartupTask.GetAsync(PackageInfo.StartupTaskId).AsTask().GetAwaiter().GetResult();
                    return task.State is Windows.ApplicationModel.StartupTaskState.Enabled
                        or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
                }
                catch (Exception ex)
                {
                    Logger.Log("Could not read the startup task: " + ex.Message);
                    return false;
                }
            }

            public static void SetEnabled(bool enabled)
            {
                try
                {
                    var task = Windows.ApplicationModel.StartupTask.GetAsync(PackageInfo.StartupTaskId).AsTask().GetAwaiter().GetResult();
                    if (enabled)
                    {
                        var state = task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
                        Logger.Log($"Startup task: {state}.");
                        // Turned off in Task Manager or Windows Settings: only the user can turn it back on there.
                        if (state == Windows.ApplicationModel.StartupTaskState.DisabledByUser)
                        {
                            Logger.Log("Startup was turned off in Windows Settings → Apps → Startup; it has to be turned on there.");
                        }
                    }
                    else
                    {
                        task.Disable();
                        Logger.Log("Startup task disabled.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log("Could not change the startup task: " + ex.Message);
                }
            }
        }
    }
}
