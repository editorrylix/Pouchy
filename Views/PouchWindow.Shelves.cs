using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Pouchy.Models;
using Pouchy.ViewModels;
using Symbol = Wpf.Ui.Controls.SymbolRegular;

namespace Pouchy.Views
{
    /// <summary>Shelf tabs, search and the undo bar.</summary>
    public partial class PouchWindow
    {
        private static readonly TimeSpan UndoMessageDuration = TimeSpan.FromSeconds(6);

        private static readonly TimeSpan NoticeDuration = TimeSpan.FromSeconds(4);

        private DispatcherTimer? _undoTimer;
        private DispatcherTimer? _noticeTimer;

        /// <summary>Items currently being dragged out of the pouch, so a shelf tab can take them.</summary>
        private IReadOnlyCollection<PouchItem>? _draggedItems;

        private void InitializeShelves()
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PouchViewModel.ActiveShelf))
            {
                // Keep the active tab visible when there are more tabs than fit.
                Dispatcher.BeginInvoke(() =>
                {
                    if (ShelfTabs.ItemContainerGenerator.ContainerFromItem(_vm.ActiveShelf) is FrameworkElement tab) tab.BringIntoView();
                }, DispatcherPriority.Loaded);
            }

            if (e.PropertyName == nameof(PouchViewModel.Notice) && _vm.Notice != null)
            {
                _noticeTimer?.Stop();
                _noticeTimer = new DispatcherTimer { Interval = NoticeDuration };
                _noticeTimer.Tick += (_, _) =>
                {
                    _noticeTimer?.Stop();
                    _vm.DismissNoticeCommand.Execute(null);
                };
                _noticeTimer.Start();
            }

            if (e.PropertyName == nameof(PouchViewModel.UndoMessage) && _vm.UndoMessage != null)
            {
                // Hide the undo bar after a few seconds; Ctrl+Z keeps working after that.
                _undoTimer?.Stop();
                _undoTimer = new DispatcherTimer { Interval = UndoMessageDuration };
                _undoTimer.Tick += (_, _) =>
                {
                    _undoTimer?.Stop();
                    _vm.DismissUndoCommand.Execute(null);
                };
                _undoTimer.Start();
            }
        }

        // ---------------------------------------------------------------- Search

        private void SearchButton_Click(object sender, RoutedEventArgs e) => OpenSearch();

        private void OpenSearch()
        {
            if (!_vm.IsSearchOpen) _vm.IsSearchOpen = true;
            Dispatcher.BeginInvoke(() =>
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            }, DispatcherPriority.Input);
        }

        /// <summary>Keys while typing in the search box. Returns true if handled.</summary>
        private bool HandleSearchBoxKey(Key key)
        {
            switch (key)
            {
                case Key.Escape:
                    _vm.IsSearchOpen = false;
                    PouchItemsControl.Focus();
                    return true;
                case Key.Down:
                case Key.Enter:
                    if (_vm.DisplayedItems.Count > 0)
                    {
                        PouchItemsControl.SelectedIndex = 0;
                        (PouchItemsControl.ItemContainerGenerator.ContainerFromIndex(0) as UIElement)?.Focus();
                    }
                    return true;
                default:
                    return false;
            }
        }

        // ---------------------------------------------------------------- Shelf tabs

        private void NewShelf_Click(object sender, RoutedEventArgs e) => PromptNewShelf();

        private Shelf? PromptNewShelf(IReadOnlyCollection<PouchItem>? moveItems = null)
        {
            string? name = PouchDialog.Prompt(this, "New shelf", "Shelves keep separate sets of items, like Work or Screenshots.",
                $"Shelf {_vm.Shelves.Count + 1}", "Create",
                validate: n => string.IsNullOrWhiteSpace(n) ? "Give the shelf a name." : null);
            if (name == null) return null;

            var shelf = _vm.NewShelf(name);
            if (moveItems != null) _vm.MoveToShelf(moveItems, shelf);
            return shelf;
        }

        private void ShelfChip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: Shelf shelf })
            {
                if (_vm.IsSearchOpen) _vm.IsSearchOpen = false;
                _vm.ActivateShelf(shelf);
            }
        }

        private void ShelfTabs_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Scroll the tab strip sideways with the normal wheel.
            if (sender is ScrollViewer viewer)
            {
                viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset - e.Delta / 3.0);
                e.Handled = true;
            }
        }

        private void ShelfChip_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is Border chip) chip.SetResourceReference(Border.BorderBrushProperty, "Pouch.Accent");
            ShelfChip_DragOver(sender, e);
        }

        private void ShelfChip_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = _draggedItems != null ? DragDropEffects.Move : DragDropEffects.Copy;
            e.Handled = true;
        }

        private void ShelfChip_DragLeave(object sender, DragEventArgs e)
        {
            // Let the template trigger decide the border again.
            if (sender is Border chip) chip.ClearValue(Border.BorderBrushProperty);
        }

        /// <summary>Dropping on a tab moves pouch items there, or adds outside files/text to that shelf.</summary>
        private async void ShelfChip_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            ShelfChip_DragLeave(sender, e);
            ShowDropOverlay(false);
            _isDraggingIn = false;
            if (sender is not FrameworkElement { DataContext: Shelf shelf }) return;

            try
            {
                if (_draggedItems != null)
                {
                    _vm.MoveToShelf(_draggedItems, shelf);
                    e.Effects = DragDropEffects.None; // Nothing for the drag source to do.
                    return;
                }

                var data = e.Data;
                if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
                {
                    await _vm.AddPathsAsync(files, shelf);
                }
                else if ((data.GetData(DataFormats.UnicodeText) ?? data.GetData(DataFormats.Text)) is string text)
                {
                    _vm.AddText(text, shelf);
                }
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }
        }

        private List<List<object>> BuildShelfMenu(Shelf shelf)
        {
            var icons = Shelf.Icons.Select(icon => (
                (object)new Wpf.Ui.Controls.SymbolIcon
                {
                    Symbol = Enum.TryParse<Wpf.Ui.Controls.SymbolRegular>($"{icon}24", out var symbol) ? symbol : Wpf.Ui.Controls.SymbolRegular.Archive24,
                    Filled = true,
                    FontSize = 18,
                    Foreground = shelf.ColorBrush,
                },
                SplitWords(icon),
                shelf.Icon == icon,
                (Action)(() => _vm.SetShelfIcon(shelf, icon))));

            var colors = Shelf.Palette.Select(hex => (
                (object)new System.Windows.Shapes.Ellipse { Width = 20, Height = 20, Fill = LabelColors.BrushFromHex(hex) },
                ColorName(hex),
                shelf.Color == hex,
                (Action)(() => _vm.SetShelfColor(shelf, hex))));

            var edit = new List<object>
            {
                Item("Rename…", Symbol.Rename24, () =>
                {
                    string? name = PouchDialog.Prompt(this, "Rename shelf", null, shelf.Name, "Rename",
                        validate: n => string.IsNullOrWhiteSpace(n) ? "Give the shelf a name." : null);
                    if (name != null) _vm.RenameShelf(shelf, name);
                }),
                MenuFactory.Grid("Icon", Symbol.Emoji24, icons, columns: 8),
                MenuFactory.Grid("Colour", Symbol.Color24, colors, columns: 5),
            };

            var rules = ShelfRules.Choices
                .Select(rule => (object)Check(ShelfRules.MenuName(rule), shelf.Rule == rule,
                    () => _vm.SetShelfRule(shelf, shelf.Rule == rule ? ShelfRule.None : rule)))
                .Prepend(Separator())
                .Prepend(Check("Nothing (a normal shelf)", shelf.Rule == ShelfRule.None, () => _vm.SetShelfRule(shelf, ShelfRule.None)))
                .ToList();
            edit.Add(MenuFactory.Submenu("Auto-collect", Symbol.Sparkle24, rules));

            var create = new List<object> { Item("New shelf…", Symbol.Add24, () => PromptNewShelf()) };

            var delete = new List<object>();
            if (_vm.HasMultipleShelves)
            {
                delete.Add(Item("Delete shelf…", Symbol.Delete24, () =>
                {
                    bool empty = shelf.Items.Count == 0;
                    if (empty || PouchDialog.Confirm(this, $"Delete “{shelf.Name}”?",
                            $"Its {shelf.Items.Count} items are removed from the pouch. Files on disk aren't touched.", "Delete", destructive: true))
                    {
                        _vm.DeleteShelf(shelf);
                    }
                }, danger: true));
            }
            return new List<List<object>> { edit, create, delete };
        }

        /// <summary>"Move to shelf" entries for the item menu.</summary>
        private object BuildMoveToShelfMenu(IReadOnlyList<PouchItem> items)
        {
            var owner = items.Count > 0 ? _vm.OwnerOf(items[0]) : null;
            var entries = _vm.Shelves
                .Where(s => s != owner)
                .Select(s => (object)Item(s.Name, MenuFactory.ShelfIcon(s), () => _vm.MoveToShelf(items, s)))
                .ToList();
            if (entries.Count > 0) entries.Add(Separator());
            entries.Add(Item("New shelf…", Symbol.Add24, () => PromptNewShelf(items)));
            return MenuFactory.Submenu("Move to shelf", Symbol.Tabs24, entries);
        }

        private static string ColorName(string hex) => hex switch
        {
            "#8B7CFF" => "Violet",
            "#3B82F6" => "Blue",
            "#14B8A6" => "Teal",
            "#22C55E" => "Green",
            "#EAB308" => "Yellow",
            "#F97316" => "Orange",
            "#EF4444" => "Red",
            "#EC4899" => "Pink",
            "#94A3B8" => "Grey",
            _ => hex,
        };

        private static MenuItem Item(string header, object icon, Action action) => MenuFactory.Item(header, icon, action);

        private static MenuItem Check(string header, bool isChecked, Action action) => MenuFactory.Check(header, isChecked, action);

        /// <summary>"AnimalCat" → "Animal cat", "MusicNote2" → "Music note".</summary>
        private static string SplitWords(string name)
        {
            var words = System.Text.RegularExpressions.Regex.Replace(name.TrimEnd('0', '1', '2', '3'), "(?<!^)([A-Z])", " $1");
            return words[..1] + words[1..].ToLowerInvariant();
        }

        // ---------------------------------------------------------------- Mascot

        private DispatcherTimer? _mascotTimer;

        /// <summary>Sets the header mascot's mood; Happy wears off after a moment.</summary>
        private void SetMascotMood(MascotMood mood)
        {
            HeaderMascot.Mood = mood;
            _mascotTimer?.Stop();
            if (mood != MascotMood.Happy) return;

            _mascotTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
            _mascotTimer.Tick += (_, _) =>
            {
                _mascotTimer?.Stop();
                HeaderMascot.Mood = MascotMood.Idle;
            };
            _mascotTimer.Start();
        }
    }
}
