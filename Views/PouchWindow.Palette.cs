using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;
using Pouchy.Interop;
using Pouchy.Models;
using Pouchy.Services.Actions;
using Pouchy.ViewModels;
using Symbol = Wpf.Ui.Controls.SymbolRegular;

namespace Pouchy.Views
{
    /// <summary>The command palette's entries that belong to the pouch: items, shelves, views and the selection.</summary>
    public partial class PouchWindow
    {
        private CommandPaletteWindow? _palette;

        /// <summary>Set by the app: commands that aren't about the pouch (settings, screenshot, updates, quit...).</summary>
        public Func<IEnumerable<PaletteCommand>>? AppCommands { get; set; }

        public void OpenPalette()
        {
            if (_palette != null)
            {
                _palette.Activate();
                return;
            }

            var commands = BuildPaletteCommands();
            _palette = new CommandPaletteWindow(commands);
            _palette.Closed += (_, _) =>
            {
                _palette = null;
                if (IsVisible) Activate();
            };

            NativeMethods.POINT? anchor = null;
            if (IsVisible && NativeMethods.GetWindowRect(Handle, out var rect))
            {
                anchor = new NativeMethods.POINT { x = rect.Left + rect.Width / 2, y = rect.Top + rect.Height / 2 };
            }
            _palette.ShowNear(anchor);
        }

        private List<PaletteCommand> BuildPaletteCommands()
        {
            var list = new List<PaletteCommand>();
            var selected = IsVisible ? GetSelectedItems() : new List<PouchItem>();

            if (selected.Count > 0) list.AddRange(SelectionCommands(selected));

            list.Add(Command("New note", Symbol.NoteAdd24, () => EnsureVisible(() => NewNote()), "Ctrl+N", "write text"));
            list.Add(Command("Paste into pouch", Symbol.ClipboardPaste24, () => EnsureVisible(() => RunAsync(_vm.PasteAsync)), "Ctrl+V"));
            list.Add(Command("Search all shelves", Symbol.Search24, () => EnsureVisible(OpenSearch), "Ctrl+F", "find"));
            list.Add(Command("New shelf", Symbol.Add24, () => EnsureVisible(() => PromptNewShelf()), "Ctrl+T", "create tab"));
            if (_vm.Items.Count > 0)
            {
                list.Add(Command($"Clear “{_vm.ActiveShelf.Name}” (keeps pinned)", Symbol.Delete24, () => _vm.ClearCommand.Execute(null), null, "empty remove all"));
                list.Add(Command("Select all", Symbol.SelectAllOn24, () => EnsureVisible(() => PouchItemsControl.SelectAll()), "Ctrl+A"));
            }
            if (_vm.CanUndo) list.Add(Command("Undo remove", Symbol.ArrowUndo24, _vm.Undo, "Ctrl+Z"));
            list.Add(Command(_vm.ClipboardShelf == null ? "Turn on clipboard history" : "Turn off clipboard history",
                Symbol.ClipboardTextLtr24, () => _vm.SetClipboardHistory(_vm.ClipboardShelf == null), null, "copy history"));
            if (Actions != null) list.Add(Command("Open actions folder", Symbol.Code24, OpenActionsFolder, null, "scripts drop actions"));

            if (AppCommands != null) list.AddRange(AppCommands());

            foreach (var shelf in _vm.Shelves)
            {
                var target = shelf;
                list.Add(new PaletteCommand
                {
                    Title = shelf.Name,
                    Subtitle = $"{Helpers.Format.Plural(shelf.Items.Count, "item")}{(shelf.IsSmart ? " · " + ShelfRules.Describe(shelf.Rule) : "")}",
                    Icon = ParseSymbol(shelf.Icon + "24", Symbol.Archive24),
                    Group = "Shelf",
                    Keywords = "switch go to",
                    Run = () => EnsureVisible(() =>
                    {
                        if (_vm.IsSearchOpen) _vm.IsSearchOpen = false;
                        _vm.ActivateShelf(target);
                    }),
                });
            }

            foreach (var mode in Enum.GetValues<PouchViewMode>())
            {
                list.Add(Command($"{mode} view", mode switch
                {
                    PouchViewMode.List => Symbol.TextBulletListLtr24,
                    PouchViewMode.Compact => Symbol.GridDots24,
                    _ => Symbol.Grid24,
                }, () => _vm.SetViewMode(mode), null, "layout", group: "View"));
            }

            foreach (var (id, name) in _vm.ThemeListProvider?.Invoke() ?? Array.Empty<(string, string)>())
            {
                list.Add(Command(name, Symbol.PaintBrush24, () => _vm.SetTheme(id), id == _vm.CurrentThemeId ? "current" : null, "theme colours look", group: "Theme"));
            }

            foreach (var shelf in _vm.Shelves)
            {
                foreach (var item in shelf.Items)
                {
                    var target = item;
                    list.Add(new PaletteCommand
                    {
                        Title = item.DisplayName,
                        Subtitle = $"{shelf.Name} · {item.Metadata}",
                        Icon = item.Kind switch
                        {
                            PouchItemKind.Folder => Symbol.Folder24,
                            PouchItemKind.Text => Symbol.TextDescription24,
                            PouchItemKind.Link => Symbol.Globe24,
                            PouchItemKind.Color => Symbol.Color24,
                            PouchItemKind.Stack => Symbol.Stack24,
                            _ => Symbol.Document24,
                        },
                        Thumbnail = item.Icon,
                        Group = "Item",
                        Keywords = item.SearchText,
                        Run = () => EnsureVisible(() => RevealItem(target)),
                    });
                }
            }
            return list;
        }

        /// <summary>Commands for the selected items, listed first.</summary>
        private IEnumerable<PaletteCommand> SelectionCommands(List<PouchItem> selected)
        {
            string what = selected.Count == 1 ? $"“{selected[0].DisplayName}”" : $"{selected.Count} selected items";
            var existing = selected.SelectMany(i => i.FilePaths).Where(p => File.Exists(p) || Directory.Exists(p))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            yield return Command($"Copy {what}", Symbol.Copy24, () => PouchViewModel.CopyItems(selected), "Ctrl+C", group: "Selection");
            yield return Command($"Pin or unpin {what}", Symbol.Pin24, () => _vm.TogglePin(selected), "Ctrl+P", group: "Selection");
            yield return Command($"Remove {what}", Symbol.Dismiss24, () => _vm.RemoveItems(selected), "Del", "delete", group: "Selection");
            if (selected.Count == 1 && selected[0].IsQuickLookSupported)
            {
                yield return Command($"Quick Look {what}", Symbol.Eye24, () => OpenQuickLook(selected[0]), "Space", "preview", group: "Selection");
            }

            if (existing.Count == 0 || !selected.All(i => i.IsFileSystemItem)) yield break;

            yield return Command($"Show {what} in folder", Symbol.FolderArrowRight24, () => Services.FileActions.ShowInExplorer(existing), null, "explorer reveal", group: "Selection");
            foreach (var folder in _vm.RecentDestinations)
            {
                string name = ActionRegistry.FolderName(folder);
                yield return Command($"Copy {what} to {name}", Symbol.FolderArrowRight24, () => MoveOrCopyTo(selected, existing, move: false, folder), null, folder, group: "Selection");
                yield return Command($"Move {what} to {name}", Symbol.ArrowMove24, () => MoveOrCopyTo(selected, existing, move: true, folder), null, folder, group: "Selection");
            }
            if (Actions != null)
            {
                foreach (var action in Actions.For(existing, Array.Empty<string>()))
                {
                    var run = action;
                    yield return Command($"{action.Name}: {what}", ParseSymbol(action.Icon), () => RunAsync(() => RunActionAsync(run, existing, selected[^1])),
                        null, "action", group: "Selection");
                }
            }
        }

        /// <summary>Switches to the item's shelf, selects it and scrolls to it.</summary>
        private void RevealItem(PouchItem item)
        {
            if (_vm.IsSearchOpen) _vm.IsSearchOpen = false;
            if (_vm.OwnerOf(item) is { } shelf) _vm.ActivateShelf(shelf);
            Dispatcher.BeginInvoke(() =>
            {
                PouchItemsControl.SelectedItems.Clear();
                PouchItemsControl.SelectedItem = item;
                PouchItemsControl.ScrollIntoView(item);
                (PouchItemsControl.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem)?.Focus();
            }, DispatcherPriority.Loaded);
        }

        /// <summary>Shows the pouch (at the cursor) before running something that needs it.</summary>
        private void EnsureVisible(Action action)
        {
            if (!IsVisible && NativeMethods.GetCursorPos(out var point)) SpawnAt(point.x, point.y);
            Activate();
            action();
        }

        private static PaletteCommand Command(string title, Symbol icon, Action run, string? shortcut = null, string? keywords = null, string group = "Command") =>
            new() { Title = title, Icon = icon, Run = run, Shortcut = shortcut, Keywords = keywords, Group = group };
    }
}
