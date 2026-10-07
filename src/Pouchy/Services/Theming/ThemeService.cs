using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Pouchy.Models;

namespace Pouchy.Services.Theming
{
    /// <summary>
    /// Loads built-in and user themes (JSON files in the themes folder), applies the
    /// selected one to a resource dictionary, and re-applies it when the theme file,
    /// the settings, or Windows' colours change.
    /// </summary>
    public sealed class ThemeService : IDisposable
    {
        private readonly SettingsService _settings;
        private readonly ResourceDictionary _target;
        private readonly string _themesFolder;
        private readonly Dispatcher? _dispatcher;
        private readonly Func<ThemeContext>? _contextOverride;
        private FileSystemWatcher? _watcher;
        private CancellationTokenSource? _pendingReload;
        private List<ThemeDefinition> _themes = new();
        private string? _appliedId;
        private double _appliedOpacity = double.NaN;

        public IReadOnlyList<ThemeDefinition> Themes => _themes;
        public ThemeDefinition Current { get; private set; } = BuiltInThemes.All()[0];
        public bool IsLight { get; private set; }
        public System.Windows.Media.Color AccentColor { get; private set; }

        /// <summary>The list of available themes changed (a file was added, edited or removed).</summary>
        public event EventHandler? ThemesChanged;

        /// <summary>A theme was applied to the resource dictionary.</summary>
        public event EventHandler? ThemeApplied;

        public ThemeService(
            SettingsService settings,
            ResourceDictionary target,
            string themesFolder,
            Dispatcher? dispatcher = null,
            Func<ThemeContext>? contextOverride = null)
        {
            _settings = settings;
            _target = target;
            _themesFolder = themesFolder;
            _dispatcher = dispatcher;
            _contextOverride = contextOverride;
        }

        public void Start()
        {
            Reload();
            Apply();

            _settings.Changed += OnSettingsChanged;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            StartWatching();
        }

        public void Dispose()
        {
            _settings.Changed -= OnSettingsChanged;
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _watcher?.Dispose();
        }

        public ThemeDefinition? Find(string id) =>
            _themes.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

        public ThemeContext GetContext() =>
            _contextOverride?.Invoke() ?? SystemTheme.CurrentContext(_settings.Current.BackgroundOpacity);

        /// <summary>Re-reads the themes folder.</summary>
        public void Reload()
        {
            _themes = BuiltInThemes.All().Concat(LoadUserThemes()).ToList();
            ThemesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Applies the theme selected in settings (falls back to the default theme).</summary>
        public void Apply()
        {
            var theme = Find(_settings.Current.ThemeId) ?? Find(BuiltInThemes.DefaultId)!;
            var context = GetContext();

            Dictionary<string, object> resources;
            try
            {
                resources = ThemeResourceBuilder.Build(theme, context);
            }
            catch (FormatException ex)
            {
                Logger.Log($"Theme '{theme.Name}' is invalid ({ex.Message}); using the default theme.");
                theme = Find(BuiltInThemes.DefaultId)!;
                resources = ThemeResourceBuilder.Build(theme, context);
            }

            foreach (var (key, value) in resources)
            {
                _target[key] = value;
            }

            Current = theme;
            IsLight = ThemeResourceBuilder.IsLight(theme, context);
            AccentColor = (System.Windows.Media.Color)resources[ThemeKeys.AccentColor];
            _appliedId = _settings.Current.ThemeId;
            _appliedOpacity = _settings.Current.BackgroundOpacity;
            ThemeApplied?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Saves an editable copy of a theme into the themes folder.
        /// </summary>
        /// <returns>The new theme file's path.</returns>
        public string CreateCustomCopy(ThemeDefinition source)
        {
            Directory.CreateDirectory(_themesFolder);

            string baseId = "custom-" + source.Id.Replace("custom-", "", StringComparison.OrdinalIgnoreCase);
            string id = baseId;
            for (int i = 2; Find(id) != null || File.Exists(Path.Combine(_themesFolder, id + ".json")); i++)
            {
                id = $"{baseId}-{i}";
            }

            var json = JsonSerializer.Serialize(source, SettingsService.JsonOptions);
            var copy = JsonSerializer.Deserialize<ThemeDefinition>(json, SettingsService.JsonOptions)!;
            copy.Id = id;
            copy.Name = source.Name.EndsWith("(custom)", StringComparison.Ordinal) ? source.Name : $"{source.Name} (custom)";
            copy.Author = Environment.UserName;

            string path = Path.Combine(_themesFolder, id + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(copy, SettingsService.JsonOptions));
            Reload();
            return path;
        }

        internal List<ThemeDefinition> LoadUserThemes()
        {
            var result = new List<ThemeDefinition>();
            if (!Directory.Exists(_themesFolder)) return result;

            var builtInIds = BuiltInThemes.All().Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var context = GetContext();

            foreach (var file in Directory.EnumerateFiles(_themesFolder, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var theme = JsonSerializer.Deserialize<ThemeDefinition>(File.ReadAllText(file), SettingsService.JsonOptions);
                    if (theme == null) continue;

                    string fileId = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrWhiteSpace(theme.Id)) theme.Id = fileId;
                    if (builtInIds.Contains(theme.Id) || result.Any(t => t.Id.Equals(theme.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        theme.Id = "custom-" + fileId;
                    }
                    if (string.IsNullOrWhiteSpace(theme.Name)) theme.Name = fileId;
                    theme.FilePath = file;
                    theme.IsBuiltIn = false;

                    ThemeResourceBuilder.Build(theme, context); // Validate now, not when it's selected.
                    result.Add(theme);
                }
                catch (Exception ex) when (ex is JsonException or FormatException or IOException)
                {
                    Logger.Log($"Skipping theme {Path.GetFileName(file)}: {ex.Message}");
                }
            }
            return result;
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            var s = _settings.Current;
            if (s.ThemeId != _appliedId || s.BackgroundOpacity != _appliedOpacity) Apply();
        }

        private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            // Accent or light/dark changed in Windows; only "system" themes care.
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
            {
                RunOnUi(Apply);
            }
        }

        private void StartWatching()
        {
            try
            {
                Directory.CreateDirectory(_themesFolder);
                _watcher = new FileSystemWatcher(_themesFolder, "*.json")
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true,
                };
                _watcher.Changed += OnThemeFileChanged;
                _watcher.Created += OnThemeFileChanged;
                _watcher.Deleted += OnThemeFileChanged;
                _watcher.Renamed += OnThemeFileChanged;
            }
            catch (Exception ex)
            {
                Logger.Log("Could not watch the themes folder: " + ex.Message);
            }
        }

        private void OnThemeFileChanged(object sender, FileSystemEventArgs e)
        {
            // Editors fire several events per save; wait for them to settle.
            _pendingReload?.Cancel();
            var cts = new CancellationTokenSource();
            _pendingReload = cts;
            Task.Delay(250, cts.Token).ContinueWith(
                _ => RunOnUi(() =>
                {
                    Logger.Log($"Theme file changed: {e.Name}. Reloading themes.");
                    Reload();
                    Apply();
                }),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
        }

        private void RunOnUi(Action action)
        {
            if (_dispatcher == null || _dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.BeginInvoke(action);
            }
        }
    }
}
