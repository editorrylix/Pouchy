using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using H.NotifyIcon;
using Pouchy.Helpers;
using Pouchy.Interop;
using Pouchy.Services;
using Pouchy.Services.Theming;
using Pouchy.ViewModels;
using Pouchy.Views;
using Wpf.Ui.Appearance;
using Symbol = Wpf.Ui.Controls.SymbolRegular;

namespace Pouchy
{
    /// <summary>Composition root: creates the services and wires them together.</summary>
    public partial class App : Application
    {
        private const string SingleInstanceMutexName = @"Local\Pouchy.SingleInstance";
        public const string IconUri = "pack://application:,,,/Pouchy;component/Assets/pouchy.ico";
        private static readonly TimeSpan SpawnCooldown = TimeSpan.FromMilliseconds(500);

        private Mutex? _singleInstanceMutex;
        private SettingsService? _settings;
        private StartupService? _startup;
        private HotkeyService? _hotkeys;
        private TriggerService? _triggers;
        private ThemeService? _themes;
        private ShellThumbnailProvider? _thumbnails;
        private LinkPreviewService? _linkPreviews;
        private UpdateService? _updates;
        private UpdateInfo? _availableUpdate;
        private System.Windows.Threading.DispatcherTimer? _updateTimer;
        private PouchViewModel? _pouchViewModel;
        private PouchWindow? _pouchWindow;
        private SettingsWindow? _settingsWindow;
        private TaskbarIcon? _trayIcon;
        private ContextMenu? _trayMenu;
        private DateTime _lastSpawn = DateTime.MinValue;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var options = StartupOptions.Parse(e.Args);
            if (options.Profile != null) AppPaths.UseProfile(options.Profile);

            string mutexName = options.Profile == null ? SingleInstanceMutexName : $"{SingleInstanceMutexName}.{options.Profile}";
            _singleInstanceMutex = new Mutex(true, mutexName, out bool isFirstInstance);
            if (!isFirstInstance)
            {
                // A second hook and a second writer to the same state file would fight.
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                Shutdown();
                return;
            }

            RegisterExceptionHandlers();
            Logger.Log("App starting up...");

            _settings = new SettingsService();
            _startup = new StartupService();
            _startup.RefreshPathIfEnabled();

            // WPF-UI applies its Mica backdrop to Application.MainWindow whenever the theme changes.
            // On the transparent pouch window that draws a solid rectangle, so give it a hidden
            // stand-in that never shows.
            MainWindow = new Window { Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false };

            // Themes first: every window binds to the theme's resources.
            _themes = new ThemeService(_settings, Resources, AppPaths.ThemesFolder, Dispatcher);
            _themes.ThemeApplied += (_, _) => SyncFluentTheme();
            _themes.Start();
            ApplyMotionSettings();
            _settings.Changed += (_, _) => ApplyMotionSettings();

            var persistence = new PersistenceService();
            _thumbnails = new ShellThumbnailProvider();
            var factory = new ItemFactory(_thumbnails);
            _linkPreviews = new LinkPreviewService();
            _pouchViewModel = new PouchViewModel(factory, persistence, _settings, _linkPreviews)
            {
                OpenSettingsAction = ShowSettings,
                ThemeListProvider = () => _themes.Themes.Select(t => (t.Id, t.Name)).ToList(),
            };
            _pouchWindow = new PouchWindow(_pouchViewModel);
            _ = _pouchViewModel.LoadAsync(clearUnpinned: _settings.Current.ClearOnStartup);
            MenuFactory.ErrorHandler = ex => PouchDialog.Alert(_pouchWindow, "That didn't work", ex.Message);

            _hotkeys = new HotkeyService();
            _hotkeys.Pressed += (_, _) => TogglePouchAtCursor();
            _hotkeys.Register(_settings.Current.Hotkey);

            _triggers = new TriggerService(_settings) { IsSuppressed = () => _pouchViewModel.IsDraggingOut };
            _triggers.Triggered += OnTriggered;
            _triggers.Start();

            SetupTrayIcon();
            _pouchViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(PouchViewModel.StatusText) or "") UpdateTrayTooltip();
            };
            _settings.Changed += (_, _) => UpdateTrayTooltip();

            StartUpdateChecks();

            if (options.ShowPouch) Dispatcher.BeginInvoke(ShowPouchAtScreenCenter, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            if (options.ShowTrayMenu) Dispatcher.BeginInvoke(() => ShowTrayMenu(atScreenCenter: true), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        // ---------------------------------------------------------------- Updates

        private void StartUpdateChecks()
        {
            _updates = new UpdateService();
            _trayIcon!.TrayBalloonTipClicked += (_, _) => OpenUpdatePage();

            // First check shortly after startup, then every few hours (each run skips if checked in the last day).
            _updateTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
            _updateTimer.Tick += async (_, _) =>
            {
                _updateTimer.Interval = TimeSpan.FromHours(6);
                await CheckForUpdatesAsync(manual: false);
            };
            _updateTimer.Start();
        }

        /// <returns>The available update, or null if up to date (or checks are off).</returns>
        internal async Task<UpdateInfo?> CheckForUpdatesAsync(bool manual)
        {
            var s = _settings!.Current;
            if (_updates == null) return null;
            if (!manual && (!s.CheckForUpdates || s.LastUpdateCheck is DateTime last && DateTime.Now - last < TimeSpan.FromHours(23)))
            {
                return _availableUpdate;
            }

            var update = await _updates.CheckAsync();
            _settings.Update(x => x.LastUpdateCheck = DateTime.Now);
            _availableUpdate = update;

            if (update != null && (manual || s.DismissedUpdateVersion != update.Version.ToString()))
            {
                Logger.Log($"Update available: {update.Tag}");
                _trayIcon?.ShowNotification($"Pouchy {update.Version} is available",
                    "Click to see what's new and download it.", H.NotifyIcon.Core.NotificationIcon.Info);
                _settings.Update(x => x.DismissedUpdateVersion = update.Version.ToString()); // Notify once per version.
            }
            return update;
        }

        private void OpenUpdatePage()
        {
            try
            {
                FileActions.Open(_availableUpdate?.Url ?? UpdateService.ReleasesUrl);
            }
            catch (Exception ex)
            {
                Logger.Log("Could not open the release page: " + ex.Message);
            }
        }

        private void ShowPouchAtScreenCenter()
        {
            int x = NativeMethods.GetSystemMetrics(0) / 2;
            int y = NativeMethods.GetSystemMetrics(1) / 2;
            _pouchWindow?.SpawnAt(x - 200, y);
        }

        private void RegisterExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Logger.Log("Unhandled: " + args.ExceptionObject);

            DispatcherUnhandledException += (_, args) =>
            {
                Logger.Log("Dispatcher unhandled: " + args.Exception);
                args.Handled = true; // Keep the tray app alive; the error is in the log.
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Logger.Log("Unobserved task exception: " + args.Exception);
                args.SetObserved();
            };
        }

        /// <summary>Keeps WPF-UI windows (settings, Quick Look) in step with the pouch theme.</summary>
        private void SyncFluentTheme()
        {
            if (_themes == null) return;
            var theme = _themes.IsLight ? ApplicationTheme.Light : ApplicationTheme.Dark;
            ApplicationThemeManager.Apply(theme, Wpf.Ui.Controls.WindowBackdropType.Mica, updateAccent: false);
            ApplicationAccentColorManager.Apply(_themes.AccentColor, theme);
        }

        private void ApplyMotionSettings()
        {
            var s = _settings!.Current;
            Motion.Enabled = !s.ReduceMotion && s.SpawnAnimation != Models.SpawnAnimation.None;
            Motion.Speed = s.AnimationSpeed;
        }

        private void SetupTrayIcon()
        {
            // The menu is opened by us rather than by the tray library, so it is styled and
            // positioned exactly like the pouch's own menus.
            _trayMenu = MenuFactory.Create(Array.Empty<IEnumerable<object>>(), null);

            _trayIcon = new TaskbarIcon
            {
                Icon = LoadTrayIcon(),
                ToolTipText = "Pouchy",
                NoLeftClickDelay = true,
            };
            _trayIcon.TrayLeftMouseUp += (_, _) => TogglePouchAtCursor();
            _trayIcon.TrayRightMouseUp += (_, _) => ShowTrayMenu(atScreenCenter: false);
            UpdateTrayTooltip();

            // Icons created in code (not in XAML) must be created explicitly to appear.
            _trayIcon.ForceCreate(enablesEfficiencyMode: false);

            static System.Drawing.Icon LoadTrayIcon()
            {
                try
                {
                    // Ask for the size the tray actually draws at this DPI.
                    int size = Math.Max(16, NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON));
                    using var stream = GetResourceStream(new Uri(IconUri)).Stream;
                    return new System.Drawing.Icon(stream, size, size);
                }
                catch (Exception ex)
                {
                    Logger.Log("Could not load tray icon: " + ex.Message);
                    return System.Drawing.SystemIcons.Application;
                }
            }
        }

        private void ShowTrayMenu(bool atScreenCenter)
        {
            if (_trayMenu == null) return;

            // Rebuilt every time so counts, checks and shelves are current.
            MenuFactory.Fill(_trayMenu, BuildTrayMenu());
            if (atScreenCenter)
            {
                _trayMenu.Placement = PlacementMode.AbsolutePoint;
                _trayMenu.HorizontalOffset = SystemParameters.PrimaryScreenWidth / 2 + 100;
                _trayMenu.VerticalOffset = SystemParameters.PrimaryScreenHeight / 2 - 200;
            }
            else
            {
                _trayMenu.Placement = PlacementMode.MousePoint;
            }
            // The developer flag opens it without a click, so Windows won't give it focus; keep it open anyway.
            _trayMenu.StaysOpen = atScreenCenter;
            _trayMenu.IsOpen = true;

            // Without this the menu doesn't close when you click somewhere else.
            if (PresentationSource.FromVisual(_trayMenu) is HwndSource source)
            {
                NativeMethods.SetForegroundWindow(source.Handle);
            }
        }

        private List<List<object>> BuildTrayMenu()
        {
            var vm = _pouchViewModel!;
            var s = _settings!.Current;
            string hotkey = s.Hotkey.Enabled ? s.Hotkey.ToString() : "";
            string status = s.GesturesPaused ? $"{vm.StatusText} · gestures paused" : vm.StatusText;
            if (AppPaths.Profile != null) status += $" · profile {AppPaths.Profile}";

            var header = new List<object> { MenuFactory.Header(status) };
            if (_availableUpdate != null)
            {
                header.Add(MenuFactory.Item($"Update available: {_availableUpdate.Version}", Symbol.ArrowDownload24, OpenUpdatePage));
            }

            var main = new List<object>
            {
                MenuFactory.Item("Show pouch", Symbol.PanelLeft24, TogglePouchAtCursor, hotkey),
                MenuFactory.Item("New note…", Symbol.NoteAdd24, () =>
                {
                    if (_pouchWindow!.NewNote()) ShowPouchAtCursor();
                }),
                MenuFactory.Item("Paste into pouch", Symbol.ClipboardPaste24, async () =>
                {
                    await vm.PasteAsync();
                    ShowPouchAtCursor();
                }),
            };

            var shelves = vm.Shelves
                .Select(shelf => (object)MenuFactory.Check($"{shelf.Name}  ({shelf.Items.Count})", shelf.IsActive, () =>
                {
                    vm.ActivateShelf(shelf);
                    ShowPouchAtCursor();
                }, MenuFactory.ShelfIcon(shelf)))
                .ToList();
            var themes = _themes!.Themes
                .Select(t => (object)MenuFactory.Check(t.Name, t.Id == s.ThemeId, () => vm.SetTheme(t.Id)))
                .ToList();
            var views = Enum.GetValues<Models.PouchViewMode>()
                .Select(mode => (object)MenuFactory.Check(mode.ToString(), s.ViewMode == mode, () => vm.SetViewMode(mode)))
                .ToList();

            var look = new List<object>
            {
                MenuFactory.Submenu("Shelf", Symbol.Tabs24, shelves),
                MenuFactory.Submenu("Theme", Symbol.PaintBrush24, themes),
                MenuFactory.Submenu("View", Symbol.Grid24, views),
            };

            var behaviour = new List<object>
            {
                MenuFactory.Item(s.GesturesPaused ? "Resume gestures" : "Pause gestures",
                    s.GesturesPaused ? Symbol.Play24 : Symbol.Pause24,
                    () => _settings.Update(x => x.GesturesPaused = !x.GesturesPaused)),
                MenuFactory.Item("Settings…", Symbol.Settings24, ShowSettings),
            };
            if (vm.Items.Count > 0)
            {
                behaviour.Add(MenuFactory.Item("Clear shelf (keeps pinned)", Symbol.Delete24, () => vm.ClearCommand.Execute(null)));
            }

            var quit = new List<object> { MenuFactory.Item("Quit Pouchy", Symbol.Power24, Quit) };

            return new List<List<object>> { header, main, look, behaviour, quit };
        }

        private void UpdateTrayTooltip()
        {
            if (_trayIcon == null || _pouchViewModel == null) return;
            string paused = _settings!.Current.GesturesPaused ? " (gestures paused)" : "";
            _trayIcon.ToolTipText = $"Pouchy · {_pouchViewModel.StatusText}{paused}";
        }

        private void ShowPouchAtCursor()
        {
            if (_pouchWindow == null || _pouchWindow.IsVisible) return;
            if (NativeMethods.GetCursorPos(out var point)) _pouchWindow.SpawnAt(point.x, point.y);
        }

        private void OnTriggered(object? sender, TriggerEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_pouchWindow == null || _pouchWindow.IsVisible) return;
                if (DateTime.UtcNow - _lastSpawn < SpawnCooldown) return;

                _lastSpawn = DateTime.UtcNow;
                _pouchWindow.SpawnAt(e.X, e.Y);
            });
        }

        private void TogglePouchAtCursor()
        {
            if (_pouchWindow == null) return;
            if (!NativeMethods.GetCursorPos(out var point)) return;

            _lastSpawn = DateTime.UtcNow;
            _pouchWindow.Toggle(point.x, point.y);
        }

        private void ShowSettings()
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }

            var viewModel = new SettingsViewModel(_settings!, _startup!, _hotkeys!, _themes!, () => CheckForUpdatesAsync(manual: true));
            _settingsWindow = new SettingsWindow(viewModel);
            _settingsWindow.Closed += (_, _) =>
            {
                viewModel.Detach();
                _settingsWindow = null;
            };
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        private void Quit()
        {
            if (_pouchWindow != null) _pouchWindow.AllowClose = true;
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Log("App shutting down.");

            _pouchViewModel?.Flush();
            _settings?.Flush();

            _trayIcon?.Dispose();
            _hotkeys?.Dispose();
            _triggers?.Dispose();
            _themes?.Dispose();
            _thumbnails?.Dispose();
            _linkPreviews?.Dispose();
            _updates?.Dispose();

            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();

            base.OnExit(e);
        }
    }
}
