using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using Pouchy.Models;

namespace Pouchy.Services
{
    /// <summary>On-disk shape of a pouch item.</summary>
    public sealed class PersistedItem
    {
        public Guid Id { get; set; }

        /// <summary>Null in state files written before item kinds existed.</summary>
        public PouchItemKind? Kind { get; set; }

        public string? FilePath { get; set; }
        public List<string>? StackFiles { get; set; }
        public string? TextContent { get; set; }

        /// <summary>File name of the PNG in the images folder, for image items.</summary>
        public string? ImageFile { get; set; }

        public string DisplayName { get; set; } = "";
        public DateTime? AddedAt { get; set; }
    }

    /// <summary>
    /// Saves the pouch to shelf_state.json (debounced, atomic writes) and
    /// keeps image items as PNGs in an images folder next to it.
    /// </summary>
    public sealed class PersistenceService
    {
        private const int SaveDelayMs = 400;

        private readonly string _stateFile;
        private readonly string _imageFolder;
        private readonly object _writeLock = new();
        private CancellationTokenSource? _pendingSave;

        public PersistenceService() : this(AppPaths.DataFolder) { }

        public PersistenceService(string dataFolder)
        {
            _stateFile = Path.Combine(dataFolder, "shelf_state.json");
            _imageFolder = Path.Combine(dataFolder, "images");
        }

        public List<PersistedItem> Load()
        {
            try
            {
                if (!File.Exists(_stateFile)) return new List<PersistedItem>();
                return JsonSerializer.Deserialize<List<PersistedItem>>(File.ReadAllText(_stateFile), SettingsService.JsonOptions)
                       ?? new List<PersistedItem>();
            }
            catch (Exception ex)
            {
                Logger.Log("Error loading pouch state: " + ex);
                return new List<PersistedItem>();
            }
        }

        /// <summary>Loads a saved image item's bitmap. Safe to call off the UI thread.</summary>
        public BitmapSource? LoadImage(string imageFile)
        {
            string path = Path.Combine(_imageFolder, imageFile);
            if (!File.Exists(path)) return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error loading saved image {imageFile}: {ex.Message}");
                return null;
            }
        }

        /// <summary>Saves shortly after the last call, so bursts of changes cause one write.</summary>
        public void ScheduleSave(IEnumerable<PouchItem> items)
        {
            var snapshot = Snapshot(items);

            _pendingSave?.Cancel();
            var cts = new CancellationTokenSource();
            _pendingSave = cts;

            Task.Delay(SaveDelayMs, cts.Token).ContinueWith(
                _ => Write(snapshot),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
        }

        /// <summary>Saves synchronously, cancelling any pending save. Use on exit.</summary>
        public void SaveNow(IEnumerable<PouchItem> items)
        {
            _pendingSave?.Cancel();
            Write(Snapshot(items));
        }

        private static List<(PersistedItem Item, BitmapSource? Image)> Snapshot(IEnumerable<PouchItem> items)
        {
            var result = new List<(PersistedItem, BitmapSource?)>();
            foreach (var item in items)
            {
                var saved = new PersistedItem
                {
                    Id = item.Id,
                    Kind = item.Kind,
                    FilePath = item.FilePath,
                    StackFiles = item.StackFiles?.ToList(),
                    TextContent = item.TextContent,
                    DisplayName = item.DisplayName,
                    AddedAt = item.AddedAt,
                };
                if (item.Kind == PouchItemKind.Image)
                {
                    if (item.ImageContent == null) continue;
                    saved.ImageFile = $"{item.Id}.png";
                }
                result.Add((saved, item.ImageContent));
            }
            return result;
        }

        private void Write(List<(PersistedItem Item, BitmapSource? Image)> snapshot)
        {
            lock (_writeLock)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_stateFile)!);
                    SaveImages(snapshot);

                    string json = JsonSerializer.Serialize(snapshot.Select(s => s.Item).ToList(), SettingsService.JsonOptions);
                    string temp = _stateFile + ".tmp";
                    File.WriteAllText(temp, json);
                    File.Move(temp, _stateFile, overwrite: true);

                    PruneImages(snapshot);
                }
                catch (Exception ex)
                {
                    Logger.Log("Error saving pouch state: " + ex);
                }
            }
        }

        private void SaveImages(List<(PersistedItem Item, BitmapSource? Image)> snapshot)
        {
            foreach (var (item, image) in snapshot)
            {
                if (item.ImageFile == null || image == null) continue;

                string path = Path.Combine(_imageFolder, item.ImageFile);
                if (File.Exists(path)) continue;

                Directory.CreateDirectory(_imageFolder);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = File.Create(path);
                encoder.Save(stream);
            }
        }

        private void PruneImages(List<(PersistedItem Item, BitmapSource? Image)> snapshot)
        {
            if (!Directory.Exists(_imageFolder)) return;

            var keep = snapshot
                .Where(s => s.Item.ImageFile != null)
                .Select(s => s.Item.ImageFile!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.EnumerateFiles(_imageFolder, "*.png"))
            {
                if (keep.Contains(Path.GetFileName(file))) continue;
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Could not delete unused image {file}: {ex.Message}");
                }
            }
        }
    }
}
