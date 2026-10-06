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

        /// <summary>File name of the PNG in the images folder: the picture for image items, the icon for links.</summary>
        public string? ImageFile { get; set; }

        public string DisplayName { get; set; } = "";
        public DateTime? AddedAt { get; set; }
        public bool IsPinned { get; set; }
        public ColorLabel Label { get; set; }
    }

    public sealed class PersistedShelf
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "Pouch";
        public string Color { get; set; } = Shelf.Palette[0];
        public List<PersistedItem> Items { get; set; } = new();
    }

    /// <summary>Everything in shelf_state.json.</summary>
    public sealed class PersistedState
    {
        public const int CurrentVersion = 2;

        public int Version { get; set; } = CurrentVersion;
        public Guid? ActiveShelfId { get; set; }
        public List<PersistedShelf> Shelves { get; set; } = new();
    }

    /// <summary>
    /// Saves the shelves to shelf_state.json (debounced, atomic writes) and keeps
    /// image items and link icons as PNGs in an images folder next to it.
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

        /// <summary>
        /// Reads the saved shelves. Files from before shelves existed (a plain list of items)
        /// become a single shelf.
        /// </summary>
        public PersistedState Load()
        {
            try
            {
                if (!File.Exists(_stateFile)) return new PersistedState();

                string json = File.ReadAllText(_stateFile);
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var items = document.RootElement.Deserialize<List<PersistedItem>>(SettingsService.JsonOptions) ?? new();
                    return new PersistedState { Shelves = { new PersistedShelf { Id = Guid.NewGuid(), Items = items } } };
                }
                return document.RootElement.Deserialize<PersistedState>(SettingsService.JsonOptions) ?? new PersistedState();
            }
            catch (Exception ex)
            {
                Logger.Log("Error loading pouch state: " + ex);
                return new PersistedState();
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
        public void ScheduleSave(IEnumerable<Shelf> shelves, Guid? activeShelfId)
        {
            var snapshot = Snapshot(shelves, activeShelfId);

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
        public void SaveNow(IEnumerable<Shelf> shelves, Guid? activeShelfId)
        {
            _pendingSave?.Cancel();
            Write(Snapshot(shelves, activeShelfId));
        }

        private sealed record Snapshotted(PersistedState State, List<(string File, BitmapSource Image)> Images);

        private static Snapshotted Snapshot(IEnumerable<Shelf> shelves, Guid? activeShelfId)
        {
            var state = new PersistedState { ActiveShelfId = activeShelfId };
            var images = new List<(string, BitmapSource)>();

            foreach (var shelf in shelves)
            {
                var saved = new PersistedShelf { Id = shelf.Id, Name = shelf.Name, Color = shelf.Color };
                foreach (var item in shelf.Items)
                {
                    var persisted = new PersistedItem
                    {
                        Id = item.Id,
                        Kind = item.Kind,
                        FilePath = item.FilePath,
                        StackFiles = item.StackFiles?.ToList(),
                        TextContent = item.TextContent,
                        DisplayName = item.DisplayName,
                        AddedAt = item.AddedAt,
                        IsPinned = item.IsPinned,
                        Label = item.Label,
                    };

                    BitmapSource? image = item.Kind switch
                    {
                        PouchItemKind.Image => item.ImageContent,
                        PouchItemKind.Link => item.Icon as BitmapSource,
                        _ => null,
                    };
                    if (item.Kind == PouchItemKind.Image && image == null) continue;
                    if (image != null)
                    {
                        persisted.ImageFile = $"{item.Id}.png";
                        images.Add((persisted.ImageFile, image));
                    }
                    saved.Items.Add(persisted);
                }
                state.Shelves.Add(saved);
            }
            return new Snapshotted(state, images);
        }

        private void Write(Snapshotted snapshot)
        {
            lock (_writeLock)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_stateFile)!);
                    SaveImages(snapshot.Images);

                    string json = JsonSerializer.Serialize(snapshot.State, SettingsService.JsonOptions);
                    string temp = _stateFile + ".tmp";
                    File.WriteAllText(temp, json);
                    File.Move(temp, _stateFile, overwrite: true);

                    PruneImages(snapshot.Images.Select(i => i.File));
                }
                catch (Exception ex)
                {
                    Logger.Log("Error saving pouch state: " + ex);
                }
            }
        }

        private void SaveImages(List<(string File, BitmapSource Image)> images)
        {
            foreach (var (file, image) in images)
            {
                string path = Path.Combine(_imageFolder, file);
                if (File.Exists(path)) continue;

                Directory.CreateDirectory(_imageFolder);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = File.Create(path);
                encoder.Save(stream);
            }
        }

        private void PruneImages(IEnumerable<string> keepFiles)
        {
            if (!Directory.Exists(_imageFolder)) return;

            var keep = keepFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
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
