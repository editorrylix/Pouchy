using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Pouchy.Interop;
using Pouchy.Services;
using Pouchy.Views;
using Symbol = Wpf.Ui.Controls.SymbolRegular;

namespace Pouchy
{
    /// <summary>Screenshots, clipboard history, auto-clear, updates and the app's palette commands.</summary>
    public partial class App
    {
        private static readonly TimeSpan ScreenshotWait = TimeSpan.FromMinutes(1);

        private ClipboardMonitor? _clipboard;
        private SoundService? _sounds;
        private DispatcherTimer? _expiryTimer;
        private DateTime _screenshotUntil;
        private bool _installingUpdate;

        // ---------------------------------------------------------------- Uninstall and restart

        /// <summary>--cleanup: remove what Pouchy added to Windows (the uninstaller runs this).</summary>
        private static void RunCleanup(string? profile)
        {
            new StartupService().SetEnabled(false);
            new ExplorerIntegrationService(profile).SetEnabled(false);
            Logger.Log("Removed startup and Explorer entries.");
        }

        /// <summary>After an update the new version waits here for the old one to exit.</summary>
        private static void WaitForProcess(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                process.WaitForExit(15000);
            }
            catch (ArgumentException)
            {
                // Already gone.
            }
        }

        // ---------------------------------------------------------------- Screenshot → Pouchy

        /// <summary>Opens Windows' screen snip; the next picture copied within a minute goes into the pouch.</summary>
        private void TakeScreenshot()
        {
            // Keep the pouch out of the picture.
            bool wasVisible = _pouchWindow?.IsVisible == true;
            if (wasVisible) _pouchWindow!.Despawn();

            _screenshotUntil = DateTime.UtcNow + ScreenshotWait;
            UpdateClipboardListening();

            var start = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(wasVisible ? 250 : 1) };
            start.Tick += (_, _) =>
            {
                start.Stop();
                try
                {
                    Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true })?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Log("Could not open screen snip: " + ex.Message);
                    _screenshotUntil = default;
                    UpdateClipboardListening();
                    PouchDialog.Alert(null, "Screen snip isn't available",
                        "Pouchy uses Windows' Snipping Tool to capture the screen. Install it from the Microsoft Store and try again.");
                }
            };
            start.Start();
        }

        // ---------------------------------------------------------------- Clipboard

        private void UpdateClipboardListening()
        {
            bool screenshotPending = DateTime.UtcNow < _screenshotUntil;
            _clipboard?.SetListening(_pouchViewModel?.ClipboardShelf != null || screenshotPending);
        }

        private async void OnClipboardChanged(object? sender, EventArgs e)
        {
            var vm = _pouchViewModel!;
            try
            {
                if (DateTime.UtcNow < _screenshotUntil && Clipboard.ContainsImage() && Clipboard.GetImage() is BitmapSource image)
                {
                    _screenshotUntil = default;
                    UpdateClipboardListening();
                    await _loadTask;
                    vm.AddImage(image);
                    _lastSpawn = DateTime.UtcNow;
                    ShowPouchAtCursor();
                    if (!vm.HasNotice) vm.ShowNotice("Screenshot added");
                    return;
                }
                await _loadTask;
                await vm.CaptureClipboardAsync();
            }
            catch (Exception ex)
            {
                Logger.Log("Clipboard change failed: " + ex.Message);
            }
        }

        // ---------------------------------------------------------------- Auto-clear

        private void StartExpiryTimer()
        {
            _expiryTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _expiryTimer.Tick += (_, _) => _pouchViewModel?.RemoveExpired(DateTime.Now);
            _expiryTimer.Start();
        }

        // ---------------------------------------------------------------- Updates

        /// <summary>Asks first, then downloads, installs and restarts.</summary>
        private async void ConfirmAndInstallUpdate()
        {
            var update = _availableUpdate;
            if (update == null || _installingUpdate) return;
            if (!PouchDialog.Confirm(null, $"Install Pouchy {update.Version.ToString(3)}?",
                    "Pouchy downloads the update, checks it, and restarts. Your shelves and settings stay as they are.",
                    "Install and restart"))
            {
                return;
            }
            try
            {
                await InstallUpdateAsync(null);
            }
            catch (Exception ex)
            {
                Logger.Log("Update failed: " + ex);
                PouchDialog.Alert(null, "The update didn't install", ex.Message);
            }
        }

        internal async Task InstallUpdateAsync(IProgress<double>? progress)
        {
            if (_installingUpdate) return;
            _installingUpdate = true;
            try
            {
                var update = _availableUpdate ?? await _updates!.CheckAsync()
                             ?? throw new InvalidOperationException("You already have the latest version.");
                string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Can't tell where Pouchy is installed.");

                if (!UpdateInstaller.CanUpdateInPlace(exe))
                {
                    OpenUpdatePage();
                    throw new InvalidOperationException("Pouchy's folder is read-only, so the download page was opened instead.");
                }

                Logger.Log($"Installing update {update.Version}...");
                string newExe = await UpdateInstaller.DownloadAsync(_updates!.Http, update, progress);
                _pouchViewModel?.Flush();
                _settings?.Flush();
                UpdateInstaller.Swap(newExe, exe);
                UpdateInstaller.Restart(exe, AppPaths.Profile);
                Logger.Log("Update installed; restarting.");
                Quit();
            }
            finally
            {
                _installingUpdate = false;
            }
        }

        // ---------------------------------------------------------------- Command palette

        private void OpenPalette()
        {
            if (_pouchWindow == null) return;
            ShowPouchAtCursor();
            _pouchWindow.OpenPalette();
        }

        private IEnumerable<PaletteCommand> AppPaletteCommands()
        {
            var s = _settings!.Current;
            yield return Command("Take a screenshot", Symbol.Screenshot24, TakeScreenshot,
                s.ScreenshotHotkey.Enabled ? s.ScreenshotHotkey.ToString() : null, "capture snip screen");
            yield return Command("Settings", Symbol.Settings24, ShowSettings, null, "preferences options");
            yield return Command("Customize theme", Symbol.PaintBrush24, ShowSettings, null, "theme editor colours");
            yield return Command(s.GesturesPaused ? "Resume gestures" : "Pause gestures", s.GesturesPaused ? Symbol.Play24 : Symbol.Pause24,
                () => _settings.Update(x => x.GesturesPaused = !x.GesturesPaused), null, "shake edge trigger");
            if (_availableUpdate != null)
            {
                yield return Command($"Install Pouchy {_availableUpdate.Version.ToString(3)}", Symbol.ArrowDownload24, ConfirmAndInstallUpdate, null, "update");
            }
            else
            {
                yield return Command("Check for updates", Symbol.ArrowSync24, async () =>
                {
                    var update = await CheckForUpdatesAsync(manual: true);
                    if (update == null) PouchDialog.Alert(null, "You're up to date", $"Pouchy {UpdateService.CurrentVersionText} is the latest version.");
                }, null, "update version");
            }
            yield return Command("Open data folder", Symbol.Folder24, () => FileActions.Open(AppPaths.DataFolder), null, "settings files backup");
            yield return Command("Hide pouch", Symbol.EyeOff24, () => _pouchWindow?.Despawn(), "Esc");
            yield return Command("Quit Pouchy", Symbol.Power24, Quit, null, "exit close");

            static PaletteCommand Command(string title, Symbol icon, Action run, string? shortcut = null, string? keywords = null) =>
                new() { Title = title, Icon = icon, Run = run, Shortcut = shortcut, Keywords = keywords };
        }
    }
}
