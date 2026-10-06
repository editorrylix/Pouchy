using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pouchy.Models;

namespace Pouchy.Services
{
    /// <summary>
    /// Owns <see cref="AppSettings"/>: loads it from %AppData%\Pouchy\settings.json,
    /// applies changes live and saves them (debounced, so sliders don't hammer the disk).
    /// </summary>
    public sealed class SettingsService
    {
        internal static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly string _settingsFile;
        private readonly string? _legacyBlacklistFile;
        private readonly object _saveLock = new();
        private CancellationTokenSource? _pendingSave;

        public AppSettings Current { get; }

        /// <summary>Raised on the calling thread after every <see cref="Update"/>.</summary>
        public event EventHandler? Changed;

        public SettingsService() : this(AppPaths.SettingsFile, AppPaths.LegacyBlacklistFile) { }

        public SettingsService(string settingsFile, string? legacyBlacklistFile = null)
        {
            _settingsFile = settingsFile;
            _legacyBlacklistFile = legacyBlacklistFile;
            Current = Load();
        }

        public void Update(Action<AppSettings> change)
        {
            change(Current);
            Changed?.Invoke(this, EventArgs.Empty);
            ScheduleSave();
        }

        /// <summary>Writes any pending changes immediately.</summary>
        public void Flush()
        {
            _pendingSave?.Cancel();
            Write(JsonSerializer.Serialize(Current, JsonOptions));
        }

        private AppSettings Load()
        {
            if (File.Exists(_settingsFile))
            {
                try
                {
                    var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsFile), JsonOptions);
                    if (loaded != null) return Normalize(loaded);
                }
                catch (Exception ex)
                {
                    Logger.Log("Settings file is unreadable, using defaults: " + ex.Message);
                    TryBackupCorruptFile();
                }
                return new AppSettings();
            }

            var settings = new AppSettings();
            MigrateLegacyBlacklist(settings);
            Write(JsonSerializer.Serialize(settings, JsonOptions));
            return settings;
        }

        private static AppSettings Normalize(AppSettings s)
        {
            s.Hotkey ??= new HotkeySetting();
            s.Blacklist ??= new List<string>();
            if (string.IsNullOrWhiteSpace(s.ThemeId)) s.ThemeId = "midnight";
            s.GridColumns = Math.Clamp(s.GridColumns, 2, 6);
            s.BackgroundOpacity = Math.Clamp(s.BackgroundOpacity, 0.4, 1.0);
            s.AnimationSpeed = Math.Clamp(s.AnimationSpeed, 0.5, 2.0);
            s.ShakeMinDistance = Math.Clamp(s.ShakeMinDistance, 5, 200);
            s.ShakeReversals = Math.Clamp(s.ShakeReversals, 2, 10);
            s.ShakeWindowMs = Math.Clamp(s.ShakeWindowMs, 200, 3000);
            s.EdgeBumpCount = Math.Clamp(s.EdgeBumpCount, 1, 5);
            s.EdgeBumpWindowMs = Math.Clamp(s.EdgeBumpWindowMs, 200, 3000);
            return s;
        }

        /// <summary>Earlier versions kept the blacklist in its own blacklist.json.</summary>
        private void MigrateLegacyBlacklist(AppSettings settings)
        {
            if (_legacyBlacklistFile == null || !File.Exists(_legacyBlacklistFile)) return;
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_legacyBlacklistFile));
                if (list != null)
                {
                    settings.Blacklist = list;
                    Logger.Log($"Migrated {list.Count} blacklist entries from blacklist.json.");
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Could not migrate blacklist.json: " + ex.Message);
            }
        }

        private void TryBackupCorruptFile()
        {
            try
            {
                File.Copy(_settingsFile, _settingsFile + ".corrupt", overwrite: true);
            }
            catch
            {
                // Best effort only.
            }
        }

        private void ScheduleSave()
        {
            // Snapshot on the caller's thread so later edits can't race the writer.
            string json = JsonSerializer.Serialize(Current, JsonOptions);

            _pendingSave?.Cancel();
            var cts = new CancellationTokenSource();
            _pendingSave = cts;

            Task.Delay(300, cts.Token).ContinueWith(
                _ => Write(json),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
        }

        private void Write(string json)
        {
            lock (_saveLock)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
                    string temp = _settingsFile + ".tmp";
                    File.WriteAllText(temp, json);
                    File.Move(temp, _settingsFile, overwrite: true);
                }
                catch (Exception ex)
                {
                    Logger.Log("Error saving settings: " + ex);
                }
            }
        }
    }
}
