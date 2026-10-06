using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pouchy.Helpers;
using Pouchy.Models;
using Pouchy.Services;

namespace Pouchy.ViewModels
{
    /// <summary>A colour dot in the search bar's label filter.</summary>
    public partial class LabelOption : ObservableObject
    {
        public LabelOption(ColorLabel label)
        {
            Label = label;
            Brush = LabelColors.BrushFor(label)!;
        }

        public ColorLabel Label { get; }
        public Brush Brush { get; }

        [ObservableProperty]
        private bool _isSelected;
    }

    public partial class PouchViewModel : ObservableObject
    {
        private const double ListWidth = 300;
        private const double CompactTileSize = 56;
        private const double TileMargin = 8;
        private const int MaxUndoSteps = 20;

        private sealed record UndoEntry(Shelf Shelf, int Index, PouchItem Item);

        private readonly ItemFactory _factory;
        private readonly PersistenceService _persistence;
        private readonly SettingsService _settings;
        private readonly LinkPreviewService? _linkPreviews;
        private readonly Stack<List<UndoEntry>> _undo = new();
        private bool _isLoading;

        public PouchViewModel(ItemFactory factory, PersistenceService persistence, SettingsService settings, LinkPreviewService? linkPreviews = null)
        {
            _factory = factory;
            _persistence = persistence;
            _settings = settings;
            _linkPreviews = linkPreviews;

            LabelOptions = LabelColors.All.Select(l => new LabelOption(l)).ToList();
            _activeShelf = AddShelf(new Shelf { Name = "Pouch" });
            _activeShelf.IsActive = true;

            _settings.Changed += (_, _) => OnPropertyChanged(string.Empty); // Refresh every appearance binding.
        }

        // ================================================================ Shelves

        public ObservableCollection<Shelf> Shelves { get; } = new();

        [ObservableProperty]
        private Shelf _activeShelf;

        /// <summary>The active shelf's items.</summary>
        public ObservableCollection<PouchItem> Items => ActiveShelf.Items;

        public bool HasMultipleShelves => Shelves.Count > 1;

        public void ActivateShelf(Shelf shelf)
        {
            if (Shelves.Contains(shelf)) ActiveShelf = shelf;
        }

        /// <summary>Moves to the next (+1) or previous (-1) shelf, wrapping around.</summary>
        public void CycleShelf(int direction)
        {
            int index = Shelves.IndexOf(ActiveShelf);
            ActiveShelf = Shelves[(index + direction + Shelves.Count) % Shelves.Count];
        }

        public Shelf NewShelf(string name)
        {
            // Each new shelf gets the next colour and a different icon.
            var shelf = AddShelf(new Shelf
            {
                Name = name.Trim(),
                Color = Shelf.Palette[Shelves.Count % Shelf.Palette.Length],
                Icon = Shelf.Icons[(Shelves.Count * 5) % Shelf.Icons.Length],
            });
            ActiveShelf = shelf;
            Save();
            return shelf;
        }

        public void RenameShelf(Shelf shelf, string name)
        {
            shelf.Name = name.Trim();
            Save();
        }

        public void SetShelfColor(Shelf shelf, string color)
        {
            shelf.Color = color;
            Save();
        }

        public void SetShelfIcon(Shelf shelf, string icon)
        {
            shelf.Icon = icon;
            Save();
        }

        /// <summary>Deletes a shelf and its items. The last shelf can't be deleted.</summary>
        public void DeleteShelf(Shelf shelf)
        {
            if (Shelves.Count <= 1 || !Shelves.Contains(shelf)) return;

            int index = Shelves.IndexOf(shelf);
            shelf.Items.CollectionChanged -= OnShelfItemsChanged;
            Shelves.Remove(shelf);
            if (ActiveShelf == shelf) ActiveShelf = Shelves[Math.Min(index, Shelves.Count - 1)];
            OnPropertyChanged(nameof(HasMultipleShelves));
            RefreshSearch();
            Save();
        }

        public void MoveToShelf(IReadOnlyCollection<PouchItem> items, Shelf target)
        {
            foreach (var item in items.ToList())
            {
                var owner = OwnerOf(item);
                if (owner == null || owner == target) continue;
                owner.Items.Remove(item);
                target.Items.Add(item);
            }
        }

        public Shelf? OwnerOf(PouchItem item) => Shelves.FirstOrDefault(s => s.Items.Contains(item));

        partial void OnActiveShelfChanged(Shelf? oldValue, Shelf newValue)
        {
            if (oldValue != null) oldValue.IsActive = false;
            newValue.IsActive = true;
            NotifyItemsChanged();
            OnPropertyChanged(nameof(Items));
            Save();
        }

        private Shelf AddShelf(Shelf shelf)
        {
            shelf.Items.CollectionChanged += OnShelfItemsChanged;
            Shelves.Add(shelf);
            OnPropertyChanged(nameof(HasMultipleShelves));
            return shelf;
        }

        // ================================================================ Search

        public IReadOnlyList<LabelOption> LabelOptions { get; }

        public ObservableCollection<PouchItem> SearchResults { get; } = new();

        [ObservableProperty]
        private bool _isSearchOpen;

        [ObservableProperty]
        private string _searchText = "";

        [ObservableProperty]
        private ColorLabel _labelFilter;

        public bool IsFiltering => IsSearchOpen && (SearchText.Trim().Length > 0 || LabelFilter != ColorLabel.None);

        /// <summary>What the pouch shows: the active shelf, or search results from every shelf.</summary>
        public ObservableCollection<PouchItem> DisplayedItems => IsFiltering ? SearchResults : Items;

        [RelayCommand]
        private void ToggleSearch()
        {
            IsSearchOpen = !IsSearchOpen;
        }

        [RelayCommand]
        private void ToggleLabelFilter(LabelOption? option)
        {
            if (option != null) LabelFilter = LabelFilter == option.Label ? ColorLabel.None : option.Label;
        }

        partial void OnIsSearchOpenChanged(bool value)
        {
            if (!value)
            {
                SearchText = "";
                LabelFilter = ColorLabel.None;
            }
            RefreshSearch();
        }

        partial void OnSearchTextChanged(string value) => RefreshSearch();

        partial void OnLabelFilterChanged(ColorLabel value)
        {
            foreach (var option in LabelOptions) option.IsSelected = option.Label == value;
            RefreshSearch();
        }

        private void RefreshSearch()
        {
            SearchResults.Clear();
            if (IsFiltering)
            {
                var words = SearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var item in Shelves.SelectMany(s => s.Items))
                {
                    if (LabelFilter != ColorLabel.None && item.Label != LabelFilter) continue;
                    string haystack = item.SearchText;
                    if (words.All(w => haystack.Contains(w, StringComparison.CurrentCultureIgnoreCase))) SearchResults.Add(item);
                }
            }
            NotifyItemsChanged();
        }

        // ================================================================ Display state

        public bool IsEmpty => DisplayedItems.Count == 0;
        public bool HasMultipleItems => DisplayedItems.Count > 1;
        public string DragAllText => $"Drag all {DisplayedItems.Count} items";
        public string ItemCountText => Items.Count.ToString();
        public string EmptyTitle => IsFiltering ? "Hmm, nothing here" : "Drop anything on me!";
        public string EmptySubtitle => IsFiltering ? "Try other words, or another colour" : "Files, folders, links, text and images";

        /// <summary>One line for the tray menu header.</summary>
        public string StatusText
        {
            get
            {
                int total = Shelves.Sum(s => s.Items.Count);
                string items = Format.Plural(total, "item");
                return HasMultipleShelves ? $"{items} on {Shelves.Count} shelves" : items;
            }
        }

        private void NotifyItemsChanged()
        {
            OnPropertyChanged(nameof(DisplayedItems));
            OnPropertyChanged(nameof(IsFiltering));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasMultipleItems));
            OnPropertyChanged(nameof(DragAllText));
            OnPropertyChanged(nameof(ItemCountText));
            OnPropertyChanged(nameof(EmptyTitle));
            OnPropertyChanged(nameof(EmptySubtitle));
            OnPropertyChanged(nameof(StatusText));
        }

        // ================================================================ Appearance (mirrored from settings)

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
        public bool ShowMascot => _settings.Current.ShowMascot;
        public bool CompactShelfTabs => _settings.Current.CompactShelfTabs;
        public DragOutAction DragOutAction => _settings.Current.DragOutAction;

        /// <summary>
        /// After items were dropped somewhere: drop the ones whose files were moved away (they'd only
        /// show as missing), and all of them if the user wants delivered items removed.
        /// </summary>
        public void AfterDragOut(IReadOnlyCollection<PouchItem> items, bool dropped)
        {
            foreach (var item in items) _factory.Refresh(item);
            var gone = items.Where(i => i.IsFileSystemItem && i.IsMissing).ToList();
            var remove = dropped && _settings.Current.RemoveAfterDragOut ? items.ToList() : gone;
            if (remove.Count > 0) RemoveItems(remove, undoable: false);
        }
        public SpawnAnimation SpawnAnimation => _settings.Current.ReduceMotion ? SpawnAnimation.None : _settings.Current.SpawnAnimation;

        /// <summary>Set by the app to open the settings window.</summary>
        public Action? OpenSettingsAction { get; set; }

        /// <summary>Set by the app so menus can list themes.</summary>
        public Func<IReadOnlyList<(string Id, string Name)>>? ThemeListProvider { get; set; }

        public string CurrentThemeId => _settings.Current.ThemeId;

        public void SetTheme(string id) => _settings.Update(s => s.ThemeId = id);

        public void SetViewMode(PouchViewMode mode) => _settings.Update(s => s.ViewMode = mode);

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

        /// <summary>True while items are being dragged out of the pouch. Read from the trigger thread.</summary>
        [ObservableProperty]
        private bool _isDraggingOut;

        // ================================================================ Loading and saving

        public async Task LoadAsync(bool clearUnpinned)
        {
            _isLoading = true;
            try
            {
                var state = await Task.Run(() => _persistence.Load());
                var restored = await Task.Run(() => state.Shelves.Select(saved => (
                        Shelf: saved,
                        Items: saved.Items
                            .Select(i => _factory.Restore(i, i.ImageFile != null ? _persistence.LoadImage(i.ImageFile) : null))
                            .OfType<PouchItem>()
                            .ToList()))
                    .ToList());

                if (restored.Count > 0)
                {
                    foreach (var shelf in Shelves.ToList()) shelf.Items.CollectionChanged -= OnShelfItemsChanged;
                    var shelves = restored.Select(r =>
                    {
                        var shelf = new Shelf
                        {
                            Id = r.Shelf.Id == Guid.Empty ? Guid.NewGuid() : r.Shelf.Id,
                            Name = string.IsNullOrWhiteSpace(r.Shelf.Name) ? "Pouch" : r.Shelf.Name,
                            Color = r.Shelf.Color,
                            Icon = Shelf.Icons.Contains(r.Shelf.Icon) ? r.Shelf.Icon : Shelf.Icons[0],
                        };
                        foreach (var item in r.Items)
                        {
                            if (!clearUnpinned || item.IsPinned) shelf.Items.Add(item);
                        }
                        return shelf;
                    }).ToList();

                    Shelves.Clear();
                    foreach (var shelf in shelves) AddShelf(shelf);
                    ActiveShelf = Shelves.FirstOrDefault(s => s.Id == state.ActiveShelfId) ?? Shelves[0];
                    ActiveShelf.IsActive = true;
                }
                Logger.Log($"Restored {Shelves.Sum(s => s.Items.Count)} items on {Shelves.Count} shelves.");
            }
            catch (Exception ex)
            {
                Logger.Log("Error restoring pouch: " + ex);
            }
            finally
            {
                _isLoading = false;
                NotifyItemsChanged();
            }

            if (clearUnpinned) Save();
            foreach (var link in AllItems.Where(i => i.Kind == PouchItemKind.Link && i.Icon == null).ToList())
            {
                _ = EnrichLinkAsync(link);
            }
        }

        /// <summary>Writes everything to disk immediately. Call on exit.</summary>
        public void Flush() => _persistence.SaveNow(Shelves, ActiveShelf.Id);

        private IEnumerable<PouchItem> AllItems => Shelves.SelectMany(s => s.Items);

        private void Save()
        {
            if (!_isLoading) _persistence.ScheduleSave(Shelves, ActiveShelf.Id);
        }

        private void OnShelfItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (IsFiltering && e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Reset)
            {
                RefreshSearch();
            }
            NotifyItemsChanged();
            Save();
        }

        // ================================================================ Adding

        /// <summary>Re-checks every item's files, e.g. when the pouch is shown.</summary>
        public void RefreshMissingState()
        {
            foreach (var item in AllItems) _factory.Refresh(item);
        }

        public async Task AddPathsAsync(IReadOnlyList<string> paths, Shelf? shelf = null)
        {
            if (paths.Count == 0) return;
            var target = (shelf ?? ActiveShelf).Items;

            if (paths.Count == 1)
            {
                string path = paths[0];
                if (target.Any(i => string.Equals(i.FilePath, path, StringComparison.OrdinalIgnoreCase))) return;
                target.Add(await Task.Run(() => _factory.CreateFromPath(path)));
            }
            else
            {
                bool alreadyStacked = target.Any(i => i.IsStack &&
                    i.StackFiles!.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                        .SequenceEqual(paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase));
                if (alreadyStacked) return;
                target.Add(await Task.Run(() => _factory.CreateStack(paths)));
            }
        }

        /// <summary>Adds text; URLs become links and colour values become swatches.</summary>
        public PouchItem? AddText(string text, Shelf? shelf = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var target = (shelf ?? ActiveShelf).Items;

            var item = _factory.CreateFromText(text);
            if (target.FirstOrDefault(i => i.Kind == item.Kind && i.TextContent == item.TextContent) is { } existing) return existing;

            target.Add(item);
            if (item.Kind == PouchItemKind.Link) _ = EnrichLinkAsync(item);
            return item;
        }

        public void AddImage(BitmapSource image, Shelf? shelf = null)
        {
            (shelf ?? ActiveShelf).Items.Add(_factory.CreateImage(image));
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

        /// <summary>Fills in a link's page title and icon.</summary>
        private async Task EnrichLinkAsync(PouchItem link)
        {
            if (_linkPreviews == null || !_settings.Current.FetchLinkPreviews || link.TextContent == null) return;

            var preview = await _linkPreviews.FetchAsync(link.TextContent);
            if (preview == null) return;

            // Don't overwrite a name the user chose.
            if (preview.Title != null && link.DisplayName == LinkPreviewParser.HostName(link.TextContent))
            {
                link.DisplayName = preview.Title;
            }
            if (preview.Icon != null) link.Icon = preview.Icon;
            Save();
        }

        // ================================================================ Removing and undo

        /// <summary>The "Removed 3 items · Undo" message shown at the bottom of the pouch.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasUndoMessage))]
        private string? _undoMessage;

        public bool HasUndoMessage => UndoMessage != null;
        public bool CanUndo => _undo.Count > 0;

        [RelayCommand]
        private void Remove(PouchItem? item)
        {
            if (item != null) RemoveItems(new[] { item });
        }

        /// <param name="undoable">False when the files themselves are gone (deleted to the Recycle Bin).</param>
        public void RemoveItems(IEnumerable<PouchItem> items, bool undoable = true, string? message = null)
        {
            var entries = items
                .Select(i => (Item: i, Shelf: OwnerOf(i)))
                .Where(e => e.Shelf != null)
                .Select(e => new UndoEntry(e.Shelf!, e.Shelf!.Items.IndexOf(e.Item), e.Item))
                .OrderBy(e => e.Index)
                .ToList();
            if (entries.Count == 0) return;

            foreach (var entry in entries) entry.Shelf.Items.Remove(entry.Item);

            if (!undoable) return;
            _undo.Push(entries);
            while (_undo.Count > MaxUndoSteps)
            {
                var kept = _undo.Reverse().Skip(1).ToList();
                _undo.Clear();
                foreach (var batch in kept) _undo.Push(batch);
            }
            UndoMessage = message ?? (entries.Count == 1 ? $"Removed “{entries[0].Item.DisplayName}”" : $"Removed {entries.Count} items");
            OnPropertyChanged(nameof(CanUndo));
        }

        /// <summary>Removes everything except pinned items from the active shelf.</summary>
        [RelayCommand]
        private void Clear()
        {
            var unpinned = Items.Where(i => !i.IsPinned).ToList();
            RemoveItems(unpinned, message: $"Cleared {Format.Plural(unpinned.Count, "item")}");
        }

        /// <summary>Puts the most recently removed items back where they were.</summary>
        [RelayCommand]
        public void Undo()
        {
            if (_undo.Count == 0) return;
            foreach (var entry in _undo.Pop())
            {
                if (!Shelves.Contains(entry.Shelf) || entry.Shelf.Items.Contains(entry.Item)) continue;
                entry.Shelf.Items.Insert(Math.Min(entry.Index, entry.Shelf.Items.Count), entry.Item);
            }
            UndoMessage = null;
            OnPropertyChanged(nameof(CanUndo));
        }

        [RelayCommand]
        private void DismissUndo() => UndoMessage = null;

        // ================================================================ Clipboard

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

        // ================================================================ Editing

        /// <summary>Pins every item, or unpins them all if they're all pinned already.</summary>
        public void TogglePin(IReadOnlyCollection<PouchItem> items)
        {
            bool pin = items.Any(i => !i.IsPinned);
            foreach (var item in items) item.IsPinned = pin;
            Save();
        }

        public void SetLabel(IReadOnlyCollection<PouchItem> items, ColorLabel label)
        {
            foreach (var item in items) item.Label = label;
            if (IsFiltering) RefreshSearch();
            Save();
        }

        public void Rename(PouchItem item, string displayName)
        {
            item.DisplayName = displayName;
            Save();
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
            var target = (OwnerOf(anchor) ?? ActiveShelf).Items;
            int index = target.IndexOf(anchor);
            target.Insert(index < 0 ? target.Count : index + 1, item);
        }

        /// <summary>Combines the files of several items into one stack, placed where the first one was.</summary>
        public async Task GroupAsync(IReadOnlyList<PouchItem> items)
        {
            var paths = items.SelectMany(i => i.FilePaths).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (paths.Count < 2) return;

            var stack = await Task.Run(() => _factory.CreateStack(paths));
            stack.IsPinned = items.Any(i => i.IsPinned);
            var target = (OwnerOf(items[0]) ?? ActiveShelf).Items;
            int index = items.Select(i => target.IndexOf(i)).Where(i => i >= 0).DefaultIfEmpty(target.Count).Min();
            RemoveItems(items, undoable: false);
            target.Insert(Math.Min(index, target.Count), stack);
        }

        /// <summary>Splits a stack back into one item per file.</summary>
        public async Task UngroupAsync(PouchItem stack)
        {
            if (!stack.IsStack || stack.StackFiles == null) return;

            var files = stack.StackFiles.ToList();
            var created = await Task.Run(() => files.Select(f => _factory.CreateFromPath(f)).ToList());
            var target = OwnerOf(stack)?.Items;
            if (target == null) return;

            int index = target.IndexOf(stack);
            target.RemoveAt(index);
            foreach (var item in created)
            {
                if (target.Any(i => string.Equals(i.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase))) continue;
                item.IsPinned = stack.IsPinned;
                item.Label = stack.Label;
                target.Insert(index++, item);
            }
        }

        private void Replace(PouchItem old, PouchItem replacement)
        {
            var target = OwnerOf(old)?.Items;
            if (target == null) return;
            replacement.IsPinned = old.IsPinned;
            replacement.Label = old.Label;
            target[target.IndexOf(old)] = replacement;
        }
    }
}
