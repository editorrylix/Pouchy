using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pouchy.Helpers;
using Pouchy.Models;
using Pouchy.Services;

namespace Pouchy.ViewModels
{
    public partial class PouchViewModel : ObservableObject
    {
        private const double ListWidth = 300;
        private const double CompactTileSize = 56;
        private const double TileMargin = 8;

        private readonly ItemFactory _factory;
        private readonly PersistenceService _persistence;
        private readonly SettingsService _settings;
        private bool _isLoading;

        public ObservableCollection<PouchItem> Items { get; } = new();

        public bool IsEmpty => Items.Count == 0;
        public bool HasMultipleItems => Items.Count > 1;
        public string DragAllText => $"Drag all {Items.Count} items";
        public string ItemCountText => Items.Count.ToString();

        // Appearance, mirrored from settings so the view can bind to it.
        public PouchViewMode ViewMode => _settings.Current.ViewMode;
        public bool IsListMode => ViewMode == PouchViewMode.List;

        public double TileSize => ViewMode == PouchViewMode.Compact
            ? CompactTileSize
            : _settings.Current.TileSize switch
            {
                Models.TileSize.Small => 76,
                Models.TileSize.Large => 120,
                _ => 96,
            };

        public int Columns => ViewMode == PouchViewMode.Compact ? _settings.Current.GridColumns + 2 : _settings.Current.GridColumns;

        /// <summary>Width of the item area; the window sizes itself around it.</summary>
        public double ContentWidth => IsListMode ? ListWidth : Columns * (TileSize + TileMargin);

        /// <summary>Item list width: content plus the tile margins it overhangs on both sides.</summary>
        public double ItemsAreaWidth => ContentWidth + TileMargin;

        public bool ShowNames => ViewMode == PouchViewMode.Grid && _settings.Current.ShowItemNames;
        public bool ShowDetails => ViewMode == PouchViewMode.Grid && _settings.Current.ShowItemDetails;
        public bool ShowTileActions => ViewMode == PouchViewMode.Grid;
        public SpawnAnimation SpawnAnimation => _settings.Current.ReduceMotion ? SpawnAnimation.None : _settings.Current.SpawnAnimation;

        /// <summary>Set by the app to open the settings window.</summary>
        public Action? OpenSettingsAction { get; set; }

        /// <summary>Set by the app so the background menu can list themes.</summary>
        public Func<IReadOnlyList<(string Id, string Name)>>? ThemeListProvider { get; set; }

        public string CurrentThemeId => _settings.Current.ThemeId;

        public void SetTheme(string id) => _settings.Update(s => s.ThemeId = id);

        public void SetViewMode(PouchViewMode mode) => _settings.Update(s => s.ViewMode = mode);

        /// <summary>True while items are being dragged out of the pouch. Read from the trigger thread.</summary>
        [ObservableProperty]
        private bool _isDraggingOut;

        public PouchViewModel(ItemFactory factory, PersistenceService persistence, SettingsService settings)
        {
            _factory = factory;
            _persistence = persistence;
            _settings = settings;
            Items.CollectionChanged += OnItemsChanged;
            _settings.Changed += (_, _) => OnPropertyChanged(string.Empty); // Refresh every appearance binding.
        }

        public async Task LoadAsync(bool clearInstead)
        {
            if (clearInstead)
            {
                _persistence.SaveNow(Items);
                return;
            }

            _isLoading = true;
            try
            {
                var restored = await Task.Run(() => _persistence.Load()
                    .Select(saved => _factory.Restore(saved, saved.ImageFile != null ? _persistence.LoadImage(saved.ImageFile) : null))
                    .OfType<PouchItem>()
                    .ToList());

                foreach (var item in restored)
                {
                    Items.Add(item);
                }
                Logger.Log($"Restored {restored.Count} pouch items.");
            }
            catch (Exception ex)
            {
                Logger.Log("Error restoring pouch: " + ex);
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>Re-checks every item's files, e.g. when the pouch is shown.</summary>
        public void RefreshMissingState()
        {
            foreach (var item in Items)
            {
                _factory.Refresh(item);
            }
        }

        public async Task AddPathsAsync(IReadOnlyList<string> paths)
        {
            if (paths.Count == 0) return;

            if (paths.Count == 1)
            {
                string path = paths[0];
                if (Items.Any(i => string.Equals(i.FilePath, path, StringComparison.OrdinalIgnoreCase))) return;
                Items.Add(await Task.Run(() => _factory.CreateFromPath(path)));
            }
            else
            {
                bool alreadyStacked = Items.Any(i => i.IsStack &&
                    i.StackFiles!.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                        .SequenceEqual(paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase));
                if (alreadyStacked) return;
                Items.Add(await Task.Run(() => _factory.CreateStack(paths)));
            }
        }

        public PouchItem? AddText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (Items.FirstOrDefault(i => i.Kind == PouchItemKind.Text && i.TextContent == text) is { } existing) return existing;
            var item = _factory.CreateText(text);
            Items.Add(item);
            return item;
        }

        public void AddImage(BitmapSource image)
        {
            Items.Add(_factory.CreateImage(image));
        }

        /// <summary>Removes everything except pinned items.</summary>
        [RelayCommand]
        private void Clear()
        {
            foreach (var item in Items.Where(i => !i.IsPinned).ToList())
            {
                Items.Remove(item);
            }
        }

        [RelayCommand]
        private void CycleViewMode()
        {
            var next = ViewMode switch
            {
                PouchViewMode.Grid => PouchViewMode.List,
                PouchViewMode.List => PouchViewMode.Compact,
                _ => PouchViewMode.Grid,
            };
            _settings.Update(s => s.ViewMode = next);
        }

        [RelayCommand]
        private void OpenSettings() => OpenSettingsAction?.Invoke();

        [RelayCommand]
        private void Remove(PouchItem? item)
        {
            if (item != null) Items.Remove(item);
        }

        public void RemoveItems(IEnumerable<PouchItem> items)
        {
            foreach (var item in items.ToList())
            {
                Items.Remove(item);
            }
        }

        [RelayCommand]
        private void Copy(PouchItem? item)
        {
            if (item != null) CopyItems(new[] { item });
        }

        /// <summary>Puts files, text and images on the clipboard, like Ctrl+C in Explorer.</summary>
        public static void CopyItems(IReadOnlyCollection<PouchItem> items)
        {
            var data = DataObjectBuilder.Build(items);
            if (data != null) SetClipboard(() => Clipboard.SetDataObject(data, copy: true));
        }

        public static void CopyText(string text) => SetClipboard(() => Clipboard.SetText(text));

        public static void CopyImage(BitmapSource image) => SetClipboard(() => Clipboard.SetImage(image));

        private static void SetClipboard(Action set)
        {
            try
            {
                set();
            }
            catch (Exception ex)
            {
                // The clipboard can be locked by another app.
                Logger.Log("Copy to clipboard failed: " + ex.Message);
            }
        }

        /// <summary>Adds whatever is on the clipboard: files, an image or text.</summary>
        public async Task PasteAsync()
        {
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList().Cast<string>().ToList();
                    await AddPathsAsync(files);
                }
                else if (Clipboard.ContainsImage() && Clipboard.GetImage() is BitmapSource image)
                {
                    AddImage(image);
                }
                else if (Clipboard.ContainsText())
                {
                    AddText(Clipboard.GetText());
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Paste failed: " + ex.Message);
            }
        }

        /// <summary>Pins every item, or unpins them all if they're all pinned already.</summary>
        public void TogglePin(IReadOnlyCollection<PouchItem> items)
        {
            bool pin = items.Any(i => !i.IsPinned);
            foreach (var item in items) item.IsPinned = pin;
            _persistence.ScheduleSave(Items);
        }

        public void Rename(PouchItem item, string displayName)
        {
            item.DisplayName = displayName;
            _persistence.ScheduleSave(Items);
        }

        public void UpdateText(PouchItem item, string text)
        {
            if (item.Kind != PouchItemKind.Text || string.IsNullOrEmpty(text)) return;
            Replace(item, _factory.CreateText(text, item.Id, item.AddedAt));
        }

        /// <summary>Recreates an item after its files moved or were renamed.</summary>
        public async Task UpdatePathsAsync(PouchItem item, IReadOnlyList<string> newPaths)
        {
            PouchItem replacement = item.Kind == PouchItemKind.Stack
                ? await Task.Run(() => _factory.CreateStack(newPaths, item.Id, item.AddedAt))
                : await Task.Run(() => _factory.CreateFromPath(newPaths[0], item.Id, item.AddedAt));
            if (item.Kind == PouchItemKind.Stack) replacement.DisplayName = item.DisplayName;
            Replace(item, replacement);
        }

        /// <summary>Adds a file created from an item (a zip, a converted image...) right after it.</summary>
        public async Task InsertAfterAsync(PouchItem anchor, string path)
        {
            var item = await Task.Run(() => _factory.CreateFromPath(path));
            int index = Items.IndexOf(anchor);
            Items.Insert(index < 0 ? Items.Count : index + 1, item);
        }

        /// <summary>Combines the files of several items into one stack, placed where the first one was.</summary>
        public async Task GroupAsync(IReadOnlyList<PouchItem> items)
        {
            var paths = items.SelectMany(i => i.FilePaths).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (paths.Count < 2) return;

            var stack = await Task.Run(() => _factory.CreateStack(paths));
            stack.IsPinned = items.Any(i => i.IsPinned);
            int index = items.Select(i => Items.IndexOf(i)).Where(i => i >= 0).DefaultIfEmpty(Items.Count).Min();
            RemoveItems(items);
            Items.Insert(Math.Min(index, Items.Count), stack);
        }

        /// <summary>Splits a stack back into one item per file.</summary>
        public async Task UngroupAsync(PouchItem stack)
        {
            if (!stack.IsStack || stack.StackFiles == null) return;

            var files = stack.StackFiles.ToList();
            var created = await Task.Run(() => files.Select(f => _factory.CreateFromPath(f)).ToList());
            int index = Items.IndexOf(stack);
            if (index < 0) return;

            Items.RemoveAt(index);
            foreach (var item in created)
            {
                if (Items.Any(i => string.Equals(i.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase))) continue;
                item.IsPinned = stack.IsPinned;
                Items.Insert(index++, item);
            }
        }

        private void Replace(PouchItem old, PouchItem replacement)
        {
            int index = Items.IndexOf(old);
            if (index < 0) return;
            replacement.IsPinned = old.IsPinned;
            Items[index] = replacement;
        }

        /// <summary>Writes the pouch to disk immediately. Call on exit.</summary>
        public void Flush() => _persistence.SaveNow(Items);

        private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasMultipleItems));
            OnPropertyChanged(nameof(DragAllText));
            OnPropertyChanged(nameof(ItemCountText));

            if (!_isLoading) _persistence.ScheduleSave(Items);
        }
    }
}
