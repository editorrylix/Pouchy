using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using Pouchy.Helpers;
using Pouchy.Interop;
using Pouchy.Services;
using Pouchy.Services.Theming;
using Pouchy.ViewModels;
using Pouchy.Views;
using Wpf.Ui.Appearance;

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
        private PouchViewModel? _pouchViewModel;
        private PouchWindow? _pouchWindow;
        private SettingsWindow? _settingsWindow;
        private TaskbarIcon? _trayIcon;
        private DateTime _lastSpawn = DateTime.MinValue;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool isFirstInstance);
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

            // Themes first: every window binds to the theme's resources.
            _themes = new ThemeService(_settings, Resources, AppPaths.ThemesFolder, Dispatcher);
            _themes.ThemeApplied += (_, _) => SyncFluentTheme();
            _themes.Start();
            ApplyMotionSettings();
            _settings.Changed += (_, _) => ApplyMotionSettings();

            var persistence = new PersistenceService();
            _thumbnails = new ShellThumbnailProvider();
            var factory = new ItemFactory(_thumbnails);
            _pouchViewModel = new PouchViewModel(factory, persistence, _settings) { OpenSettingsAction = ShowSettings };
            _pouchWindow = new PouchWindow(_pouchViewModel);
            _ = _pouchViewModel.LoadAsync(clearInstead: _settings.Current.ClearOnStartup);

            _hotkeys = new HotkeyService();
            _hotkeys.Pressed += (_, _) => TogglePouchAtCursor();
            _hotkeys.Register(_settings.Current.Hotkey);

            _triggers = new TriggerService(_settings) { IsSuppressed = () => _pouchViewModel.IsDraggingOut };
            _triggers.Triggered += OnTriggered;
            _triggers.Start();

            SetupTrayIcon();
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
            var menu = new ContextMenu();
            menu.Items.Add(MenuItem("Show Pouch", (_, _) => TogglePouchAtCursor()));
            menu.Items.Add(MenuItem("Settings", (_, _) => ShowSettings()));
            menu.Items.Add(MenuItem("Clear Pouch", (_, _) => _pouchViewModel?.ClearCommand.Execute(null)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("Quit Pouchy", (_, _) => Quit()));

            _trayIcon = new TaskbarIcon
            {
                Icon = LoadTrayIcon(),
                ToolTipText = "Pouchy",
                ContextMenu = menu,
                NoLeftClickDelay = true,
            };
            _trayIcon.TrayLeftMouseUp += (_, _) => TogglePouchAtCursor();

            // Icons created in code (not in XAML) must be created explicitly to appear.
            _trayIcon.ForceCreate(enablesEfficiencyMode: false);

            static System.Drawing.Icon LoadTrayIcon()
            {
                try
                {
                    using var stream = GetResourceStream(new Uri(IconUri)).Stream;
                    return new System.Drawing.Icon(stream, 16, 16);
                }
                catch (Exception ex)
                {
                    Logger.Log("Could not load tray icon: " + ex.Message);
                    return System.Drawing.SystemIcons.Application;
                }
            }

            static MenuItem MenuItem(string header, RoutedEventHandler onClick)
            {
                var item = new MenuItem { Header = header };
                item.Click += onClick;
                return item;
            }
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

            var viewModel = new SettingsViewModel(_settings!, _startup!, _hotkeys!, _themes!);
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

            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();

            base.OnExit(e);
        }
    }
}
