using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pouchy.Models;
using Pouchy.Services;
using Pouchy.Services.Theming;

namespace Pouchy.ViewModels
{
    /// <summary>A theme in the settings gallery, with brushes for its preview swatch.</summary>
    public sealed class ThemeOption
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string Subtitle { get; init; }
        public required Brush Background { get; init; }
        public required Brush Border { get; init; }
        public required Brush Accent { get; init; }
        public required Brush Text { get; init; }
        public required Brush TextSecondary { get; init; }
        public required Brush Tile { get; init; }
        public required CornerRadius CornerRadius { get; init; }
        public required FontFamily HeaderFont { get; init; }
    }

    /// <summary>Edits <see cref="AppSettings"/>; every change applies and saves immediately.</summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settings;
        private readonly StartupService _startup;
        private readonly HotkeyService _hotkeys;
        private readonly ThemeService _themes;
        private readonly bool _initialized;

        public IReadOnlyList<DragModifier> ModifierOptions { get; } = Enum.GetValues<DragModifier>();
        public IReadOnlyList<DragDetectionMode> DragDetectionOptions { get; } = Enum.GetValues<DragDetectionMode>();
        public IReadOnlyList<PouchViewMode> ViewModeOptions { get; } = Enum.GetValues<PouchViewMode>();
        public IReadOnlyList<TileSize> TileSizeOptions { get; } = Enum.GetValues<TileSize>();
        public IReadOnlyList<SpawnAnimation> SpawnAnimationOptions { get; } = Enum.GetValues<SpawnAnimation>();
        public IReadOnlyList<DragOutAction> DragOutOptions { get; } = Enum.GetValues<DragOutAction>();

        public ObservableCollection<ThemeOption> Themes { get; } = new();

        [ObservableProperty] private string _selectedThemeId = "";
        [ObservableProperty] private PouchViewMode _viewMode;
        [ObservableProperty] private TileSize _tileSize;
        [ObservableProperty] private int _gridColumns;
        [ObservableProperty] private bool _showItemNames;
        [ObservableProperty] private bool _showItemDetails;
        [ObservableProperty] private double _backgroundOpacity;
        [ObservableProperty] private SpawnAnimation _spawnAnimation;
        [ObservableProperty] private double _animationSpeed;
        [ObservableProperty] private bool _reduceMotion;
        [ObservableProperty] private bool _showMascot;
        [ObservableProperty] private bool _compactShelfTabs;
        [ObservableProperty] private DragOutAction _dragOutAction;
        [ObservableProperty] private bool _removeAfterDragOut;

        [ObservableProperty] private bool _runAtStartup;
        [ObservableProperty] private bool _clearOnStartup;
        [ObservableProperty] private bool _fetchLinkPreviews;

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

        public SettingsViewModel(SettingsService settings, StartupService startup, HotkeyService hotkeys, ThemeService themes)
        {
            _settings = settings;
            _startup = startup;
            _hotkeys = hotkeys;
            _themes = themes;

            var s = settings.Current;
            RebuildThemeOptions();
            SelectedThemeId = s.ThemeId;
            ViewMode = s.ViewMode;
            TileSize = s.TileSize;
            GridColumns = s.GridColumns;
            ShowItemNames = s.ShowItemNames;
            ShowItemDetails = s.ShowItemDetails;
            BackgroundOpacity = s.BackgroundOpacity;
            SpawnAnimation = s.SpawnAnimation;
            AnimationSpeed = s.AnimationSpeed;
            ReduceMotion = s.ReduceMotion;
            ShowMascot = s.ShowMascot;
            CompactShelfTabs = s.CompactShelfTabs;
            DragOutAction = s.DragOutAction;
            RemoveAfterDragOut = s.RemoveAfterDragOut;

            RunAtStartup = startup.IsEnabled;
            ClearOnStartup = s.ClearOnStartup;
            FetchLinkPreviews = s.FetchLinkPreviews;
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
            _themes.ThemesChanged += OnThemesChanged;
        }

        /// <summary>Unhooks from the theme service; call when the settings window closes.</summary>
        public void Detach() => _themes.ThemesChanged -= OnThemesChanged;

        private void OnThemesChanged(object? sender, EventArgs e)
        {
            string selected = SelectedThemeId;
            RebuildThemeOptions();
            SelectedThemeId = selected;
        }

        private void RebuildThemeOptions()
        {
            Themes.Clear();
            var context = _themes.GetContext() with { BackgroundOpacity = 1 };
            foreach (var theme in _themes.Themes)
            {
                try
                {
                    var r = ThemeResourceBuilder.Build(theme, context);
                    Themes.Add(new ThemeOption
                    {
                        Id = theme.Id,
                        Name = theme.Name,
                        Subtitle = theme.IsBuiltIn ? "Built-in" : "Custom",
                        Background = (Brush)r[ThemeKeys.Background],
                        Border = (Brush)r[ThemeKeys.Border],
                        Accent = (Brush)r[ThemeKeys.Accent],
                        Text = (Brush)r[ThemeKeys.Text],
                        TextSecondary = (Brush)r[ThemeKeys.TextSecondary],
                        Tile = (Brush)r[ThemeKeys.Tile],
                        CornerRadius = (CornerRadius)r[ThemeKeys.TileCornerRadius],
                        HeaderFont = (FontFamily)r[ThemeKeys.HeaderFontFamily],
                    });
                }
                catch (FormatException)
                {
                    // Invalid themes are already filtered out when loaded.
                }
            }
        }

        [RelayCommand]
        private void OpenThemesFolder()
        {
            Directory.CreateDirectory(AppPaths.ThemesFolder);
            OpenInShell(AppPaths.ThemesFolder);
        }

        /// <summary>Copies the selected theme to an editable JSON file, selects it and opens it.</summary>
        [RelayCommand]
        private void CustomizeTheme()
        {
            var source = _themes.Find(SelectedThemeId) ?? _themes.Current;
            try
            {
                string path = _themes.CreateCustomCopy(source);
                SelectedThemeId = Path.GetFileNameWithoutExtension(path);
                OpenInShell(path);
            }
            catch (Exception ex)
            {
                Logger.Log("Could not create custom theme: " + ex.Message);
            }
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
                Directory.CreateDirectory(AppPaths.DataFolder);
                bool exists = File.Exists(path) || Directory.Exists(path);
                Process.Start(new ProcessStartInfo(exists ? path : AppPaths.DataFolder) { UseShellExecute = true });
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

        partial void OnSelectedThemeIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value)) Apply(s => s.ThemeId = value);
        }

        partial void OnViewModeChanged(PouchViewMode value) => Apply(s => s.ViewMode = value);
        partial void OnTileSizeChanged(TileSize value) => Apply(s => s.TileSize = value);
        partial void OnGridColumnsChanged(int value) => Apply(s => s.GridColumns = value);
        partial void OnShowItemNamesChanged(bool value) => Apply(s => s.ShowItemNames = value);
        partial void OnShowItemDetailsChanged(bool value) => Apply(s => s.ShowItemDetails = value);
        partial void OnBackgroundOpacityChanged(double value) => Apply(s => s.BackgroundOpacity = Math.Round(value, 2));
        partial void OnSpawnAnimationChanged(SpawnAnimation value) => Apply(s => s.SpawnAnimation = value);
        partial void OnAnimationSpeedChanged(double value) => Apply(s => s.AnimationSpeed = Math.Round(value, 2));
        partial void OnReduceMotionChanged(bool value) => Apply(s => s.ReduceMotion = value);
        partial void OnShowMascotChanged(bool value) => Apply(s => s.ShowMascot = value);
        partial void OnCompactShelfTabsChanged(bool value) => Apply(s => s.CompactShelfTabs = value);
        partial void OnDragOutActionChanged(DragOutAction value) => Apply(s => s.DragOutAction = value);
        partial void OnRemoveAfterDragOutChanged(bool value) => Apply(s => s.RemoveAfterDragOut = value);

        partial void OnClearOnStartupChanged(bool value) => Apply(s => s.ClearOnStartup = value);
        partial void OnFetchLinkPreviewsChanged(bool value) => Apply(s => s.FetchLinkPreviews = value);
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
