using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pouchy.Models;
using Pouchy.Services;

namespace Pouchy.ViewModels
{
    /// <summary>Edits <see cref="AppSettings"/>; every change applies and saves immediately.</summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settings;
        private readonly StartupService _startup;
        private readonly HotkeyService _hotkeys;
        private readonly bool _initialized;

        public IReadOnlyList<DragModifier> ModifierOptions { get; } = Enum.GetValues<DragModifier>();
        public IReadOnlyList<DragDetectionMode> DragDetectionOptions { get; } = Enum.GetValues<DragDetectionMode>();

        [ObservableProperty] private bool _runAtStartup;
        [ObservableProperty] private bool _clearOnStartup;

        [ObservableProperty] private bool _shakeEnabled;
        [ObservableProperty] private int _shakeMinDistance;
        [ObservableProperty] private int _shakeReversals;
        [ObservableProperty] private bool _edgeBumpEnabled;
        [ObservableProperty] private bool _modifierDragEnabled;
        [ObservableProperty] private DragModifier _modifierDragKey;
        [ObservableProperty] private DragDetectionMode _dragDetection;

        [ObservableProperty] private bool _hotkeyEnabled;
        [ObservableProperty] private string _hotkeyText = "";
        [ObservableProperty] private string? _hotkeyError;

        [ObservableProperty] private bool _suppressInFullscreen;
        [ObservableProperty] private string _blacklistText = "";

        public string DragDetectionDescription => DragDetection switch
        {
            DragDetectionMode.Off => "Any left-button drag can open the pouch, including moving windows and selecting text.",
            DragDetectionMode.Strict => "Only real drag-and-drop operations (files, links, images) open the pouch.",
            _ => "Ignores window moves, resizes, menus and text selection.",
        };

        public SettingsViewModel(SettingsService settings, StartupService startup, HotkeyService hotkeys)
        {
            _settings = settings;
            _startup = startup;
            _hotkeys = hotkeys;

            var s = settings.Current;
            RunAtStartup = startup.IsEnabled;
            ClearOnStartup = s.ClearOnStartup;
            ShakeEnabled = s.ShakeEnabled;
            ShakeMinDistance = s.ShakeMinDistance;
            ShakeReversals = s.ShakeReversals;
            EdgeBumpEnabled = s.EdgeBumpEnabled;
            ModifierDragEnabled = s.ModifierDragEnabled;
            ModifierDragKey = s.ModifierDragKey;
            DragDetection = s.DragDetection;
            HotkeyEnabled = s.Hotkey.Enabled;
            HotkeyText = s.Hotkey.ToString();
            SuppressInFullscreen = s.SuppressInFullscreen;
            BlacklistText = string.Join(Environment.NewLine, s.Blacklist);

            _initialized = true;
        }

        /// <summary>Called by the hotkey recorder box.</summary>
        public void SetHotkey(ModifierKeys modifiers, Key key)
        {
            if (modifiers == ModifierKeys.None)
            {
                HotkeyError = "Use at least one modifier: Ctrl, Alt, Shift or Win.";
                return;
            }

            var previous = new HotkeySetting
            {
                Enabled = _settings.Current.Hotkey.Enabled,
                Modifiers = _settings.Current.Hotkey.Modifiers,
                Key = _settings.Current.Hotkey.Key,
            };
            var candidate = new HotkeySetting { Enabled = HotkeyEnabled, Modifiers = modifiers, Key = key };

            if (!_hotkeys.Register(candidate))
            {
                HotkeyError = $"{candidate} is already used by another app.";
                _hotkeys.Register(previous);
                return;
            }

            HotkeyError = null;
            HotkeyText = candidate.ToString();
            _settings.Update(s => s.Hotkey = candidate);
        }

        [RelayCommand]
        private void OpenDataFolder() => OpenInShell(AppPaths.DataFolder);

        [RelayCommand]
        private void OpenLog() => OpenInShell(AppPaths.LogFile);

        private static void OpenInShell(string path)
        {
            try
            {
                if (!File.Exists(path)) Directory.CreateDirectory(AppPaths.DataFolder);
                Process.Start(new ProcessStartInfo(File.Exists(path) ? path : AppPaths.DataFolder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Log($"Could not open {path}: {ex.Message}");
            }
        }

        private void Apply(Action<AppSettings> change)
        {
            if (_initialized) _settings.Update(change);
        }

        partial void OnRunAtStartupChanged(bool value)
        {
            if (_initialized) _startup.SetEnabled(value);
        }

        partial void OnClearOnStartupChanged(bool value) => Apply(s => s.ClearOnStartup = value);
        partial void OnShakeEnabledChanged(bool value) => Apply(s => s.ShakeEnabled = value);
        partial void OnShakeMinDistanceChanged(int value) => Apply(s => s.ShakeMinDistance = value);
        partial void OnShakeReversalsChanged(int value) => Apply(s => s.ShakeReversals = value);
        partial void OnEdgeBumpEnabledChanged(bool value) => Apply(s => s.EdgeBumpEnabled = value);
        partial void OnModifierDragEnabledChanged(bool value) => Apply(s => s.ModifierDragEnabled = value);
        partial void OnModifierDragKeyChanged(DragModifier value) => Apply(s => s.ModifierDragKey = value);
        partial void OnSuppressInFullscreenChanged(bool value) => Apply(s => s.SuppressInFullscreen = value);

        partial void OnDragDetectionChanged(DragDetectionMode value)
        {
            OnPropertyChanged(nameof(DragDetectionDescription));
            Apply(s => s.DragDetection = value);
        }

        partial void OnHotkeyEnabledChanged(bool value)
        {
            if (!_initialized) return;
            _settings.Update(s => s.Hotkey.Enabled = value);
            HotkeyError = _hotkeys.Register(_settings.Current.Hotkey)
                ? null
                : $"{_settings.Current.Hotkey} is already used by another app.";
        }

        partial void OnBlacklistTextChanged(string value)
        {
            var entries = value
                .Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            Apply(s => s.Blacklist = entries);
        }
    }
}
