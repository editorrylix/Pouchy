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

        public void AddText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (Items.Any(i => i.Kind == PouchItemKind.Text && i.TextContent == text)) return;
            Items.Add(_factory.CreateText(text));
        }

        public void AddImage(BitmapSource image)
        {
            Items.Add(_factory.CreateImage(image));
        }

        [RelayCommand]
        private void Clear() => Items.Clear();

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

        [RelayCommand]
        private void Copy(PouchItem? item)
        {
            if (item == null) return;
            var data = DataObjectBuilder.Build(new[] { item });
            if (data == null) return;

            try
            {
                Clipboard.SetDataObject(data, copy: true);
            }
            catch (Exception ex)
            {
                // The clipboard can be locked by another app.
                Logger.Log("Copy to clipboard failed: " + ex.Message);
            }
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
