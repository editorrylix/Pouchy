using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Pouchy.Interop;
using Pouchy.Models;
using Pouchy.Services;
using Pouchy.ViewModels;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;
using Symbol = Wpf.Ui.Controls.SymbolRegular;

namespace Pouchy.Views
{
    /// <summary>Right-click menus and the actions behind them.</summary>
    public partial class PouchWindow
    {
        private const int MaxItemsToOpenAtOnce = 15;

        /// <summary>Physical-pixel point where the last menu opened; the Explorer menu opens there too.</summary>
        private NativeMethods.POINT _menuPoint;

        // ---------------------------------------------------------------- Opening menus

        private void Window_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            NativeMethods.GetCursorPos(out _menuPoint);

            if (FindShelfChip(e.OriginalSource as DependencyObject) is { DataContext: Shelf shelf })
            {
                NewMenu(BuildShelfMenu(shelf)).IsOpen = true;
            }
            else if (ItemsControl.ContainerFromElement(PouchItemsControl, (DependencyObject)e.OriginalSource) is ListBoxItem container)
            {
                if (!container.IsSelected)
                {
                    PouchItemsControl.SelectedItems.Clear();
                    container.IsSelected = true;
                }
                container.Focus();
                ShowItemMenu(GetSelectedItems(), placeAtMouse: true);
            }
            else
            {
                ShowBackgroundMenu();
            }
            e.Handled = true;
        }

        /// <summary>Menu key or Shift+F10: open the menu next to the focused item.</summary>
        private void ShowItemMenuFromKeyboard()
        {
            var items = GetSelectedItems();
            if (items.Count == 0)
            {
                NativeMethods.GetCursorPos(out _menuPoint);
                ShowBackgroundMenu();
                return;
            }

            if (PouchItemsControl.ItemContainerGenerator.ContainerFromItem(items[0]) is FrameworkElement container)
            {
                var screen = container.PointToScreen(new Point(container.ActualWidth / 2, container.ActualHeight / 2));
                _menuPoint = new NativeMethods.POINT { x = (int)screen.X, y = (int)screen.Y };
                ShowItemMenu(items, placeAtMouse: false, container);
            }
        }

        private List<PouchItem> GetSelectedItems() =>
            PouchItemsControl.SelectedItems.Cast<PouchItem>().OrderBy(i => _vm.DisplayedItems.IndexOf(i)).ToList();

        private static FrameworkElement? FindShelfChip(DependencyObject? element)
        {
            for (var current = element; current != null;
                 current = current is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
            {
                if (current is FrameworkElement { Tag: "ShelfChip" } chip) return chip;
            }
            return null;
        }

        private void ShowItemMenu(IReadOnlyList<PouchItem> items, bool placeAtMouse, UIElement? anchor = null)
        {
            if (items.Count == 0) return;
            var menu = NewMenu(BuildItemMenu(items));
            if (!placeAtMouse && anchor != null)
            {
                menu.PlacementTarget = anchor;
                menu.Placement = PlacementMode.Center;
            }
            menu.IsOpen = true;
        }

        private void ShowBackgroundMenu() => NewMenu(BuildBackgroundMenu()).IsOpen = true;

        // ---------------------------------------------------------------- Item menu

        private List<List<object>> BuildItemMenu(IReadOnlyList<PouchItem> items)
        {
            var item = items[0];
            bool single = items.Count == 1;
            bool allFileSystem = items.All(i => i.IsFileSystemItem);
            var paths = items.SelectMany(i => i.FilePaths).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var existing = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
            bool singleFile = single && item.Kind == PouchItemKind.File && existing.Count == 1;

            var open = new List<object>();
            if (single)
            {
                switch (item.Kind)
                {
                    case PouchItemKind.File when existing.Count > 0:
                        open.Add(Item("Open", Symbol.Open24, () => OpenItem(item), "Enter"));
                        open.Add(Item("Open with…", Symbol.AppsList24, () => FileActions.OpenWith(item.FilePath!)));
                        break;
                    case PouchItemKind.Folder when existing.Count > 0:
                        open.Add(Item("Open", Symbol.FolderOpen24, () => OpenItem(item), "Enter"));
                        break;
                    case PouchItemKind.Stack when existing.Count > 0:
                        open.Add(Item($"Open all {existing.Count} items", Symbol.Open24, () => OpenPaths(existing)));
                        break;
                    case PouchItemKind.Text when TextTools.IsUrl(item.TextContent!):
                    case PouchItemKind.Link:
                        open.Add(Item("Open link", Symbol.Globe24, () => FileActions.Open(item.TextContent!.Trim()), "Enter"));
                        break;
                }
                if (item.IsQuickLookSupported) open.Add(Item("Quick Look", Symbol.Eye24, () => OpenQuickLook(item), "Space"));
            }
            else if (existing.Count > 0)
            {
                open.Add(Item($"Open all {existing.Count} items", Symbol.Open24, () => OpenPaths(existing)));
            }
            if (existing.Count > 0) open.Add(Item("Show in folder", Symbol.FolderArrowRight24, () => FileActions.ShowInExplorer(existing)));

            var clipboard = new List<object>
            {
                Item("Copy", Symbol.Copy24, () => PouchViewModel.CopyItems(items), "Ctrl+C"),
            };
            if (paths.Count > 0)
            {
                clipboard.Add(Item(paths.Count == 1 ? "Copy path" : "Copy paths", Symbol.DocumentCopy24,
                    () => PouchViewModel.CopyText(string.Join(Environment.NewLine, paths)), "Ctrl+Shift+C"));
                clipboard.Add(Item("Copy as \"path\"", Symbol.DocumentCopy24,
                    () => PouchViewModel.CopyText(string.Join(" ", paths.Select(p => $"\"{p}\"")))));
                if (single && item.Kind != PouchItemKind.Stack)
                {
                    clipboard.Add(Item("Copy name", Symbol.TextT24, () => PouchViewModel.CopyText(Path.GetFileName(item.FilePath!.TrimEnd('\\')))));
                }
            }
            if (single && item.Kind == PouchItemKind.Color && item.ColorValue is System.Windows.Media.Color color)
            {
                clipboard.Add(Item($"Copy {TextTools.ToHex(color)}", Symbol.Color24, () => PouchViewModel.CopyText(TextTools.ToHex(color))));
                clipboard.Add(Item($"Copy {TextTools.ToRgb(color)}", Symbol.Color24, () => PouchViewModel.CopyText(TextTools.ToRgb(color))));
                clipboard.Add(Item($"Copy {TextTools.ToHsl(color)}", Symbol.Color24, () => PouchViewModel.CopyText(TextTools.ToHsl(color))));
            }
            if (single && item.Kind == PouchItemKind.Link)
            {
                clipboard.Add(Item("Copy title", Symbol.TextT24, () => PouchViewModel.CopyText(item.DisplayName)));
                clipboard.Add(Item("Copy as Markdown link", Symbol.Link24, () => PouchViewModel.CopyText($"[{item.DisplayName}]({item.TextContent})")));
            }
            if (singleFile && PreviewSupport.IsText(item.FilePath!))
            {
                clipboard.Add(Item("Copy contents", Symbol.DocumentText24, () => PouchViewModel.CopyText(FileActions.ReadText(item.FilePath!))));
            }
            if (singleFile && PreviewSupport.IsImage(item.FilePath!))
            {
                clipboard.Add(Item("Copy image", Symbol.Image24, () => PouchViewModel.CopyImage(FileActions.LoadImage(item.FilePath!))));
            }

            var organise = new List<object>();
            if (single)
            {
                if (item.Kind == PouchItemKind.Text)
                {
                    organise.Add(Item("Edit…", Symbol.Edit24, () => EditText(item), "F2"));
                    organise.Add(Submenu("Transform", Symbol.TextCaseTitle24,
                        Item("UPPERCASE", Symbol.TextCaseUppercase24, () => _vm.UpdateText(item, item.TextContent!.ToUpper())),
                        Item("lowercase", Symbol.TextCaseLowercase24, () => _vm.UpdateText(item, item.TextContent!.ToLower())),
                        Item("Title Case", Symbol.TextCaseTitle24, () => _vm.UpdateText(item, TextTools.ToTitleCase(item.TextContent!))),
                        Item("Tidy whitespace", Symbol.TextGrammarWand24, () => _vm.UpdateText(item, TextTools.TidyWhitespace(item.TextContent!)))));
                    organise.Add(Item("Save as text file…", Symbol.Save24, () => SaveTextAs(item)));
                }
                else
                {
                    organise.Add(Item("Rename…", Symbol.Rename24, () => Rename(item), "F2"));
                }
            }
            var currentLabel = items.Select(i => i.Label).Distinct().Count() == 1 ? items[0].Label : (ColorLabel?)null;
            var labels = LabelColors.All
                .Select(l => (object)Check(l.ToString(), currentLabel == l, () => _vm.SetLabel(items, currentLabel == l ? ColorLabel.None : l),
                    MenuFactory.Dot(LabelColors.BrushFor(l)!, ring: currentLabel == l)))
                .Append(Separator())
                .Append(Item("No label", Symbol.Circle24, () => _vm.SetLabel(items, ColorLabel.None)))
                .ToArray();
            organise.Add(Submenu("Label", Symbol.Tag24, labels));
            organise.Add(BuildMoveToShelfMenu(items));

            bool allPinned = items.All(i => i.IsPinned);
            organise.Add(Item(allPinned ? "Unpin" : "Pin", allPinned ? Symbol.PinOff24 : Symbol.Pin24, () => _vm.TogglePin(items), "Ctrl+P"));
            if (items.Count > 1 && allFileSystem)
            {
                organise.Add(Item("Group into stack", Symbol.Stack24, () => RunAsync(() => _vm.GroupAsync(items)), "Ctrl+G"));
            }
            if (single && item.IsStack)
            {
                organise.Add(Item("Ungroup", Symbol.ArrowSplit24, () => RunAsync(() => _vm.UngroupAsync(item))));
            }

            var files = new List<object>();
            if (allFileSystem && existing.Count > 0)
            {
                files.Add(Item("Move to…", Symbol.ArrowMove24, () => MoveOrCopyTo(items, existing, move: true)));
                files.Add(Item("Copy to…", Symbol.FolderArrowRight24, () => MoveOrCopyTo(items, existing, move: false)));
                files.Add(Item("Compress to ZIP", Symbol.FolderZip24, () => RunAsync(async () =>
                {
                    string zip = await Task.Run(() => FileActions.CompressToZip(existing));
                    await _vm.InsertAfterAsync(items[^1], zip);
                })));
            }
            if (singleFile && FileActions.IsZip(item.FilePath!))
            {
                files.Add(Item("Extract here", Symbol.FolderArrowUp24, () => RunAsync(async () =>
                {
                    string folder = await Task.Run(() => FileActions.ExtractZip(item.FilePath!));
                    await _vm.InsertAfterAsync(item, folder);
                })));
            }
            if (singleFile && PreviewSupport.IsImage(item.FilePath!))
            {
                string path = item.FilePath!;
                files.Add(Submenu("Image", Symbol.ImageEdit24,
                    Item("Convert to PNG", Symbol.Image24, () => RunAsync(() => CreateFromFile(item, () => FileActions.ConvertImage(path, ImageFileFormat.Png)))),
                    Item("Convert to JPG", Symbol.Image24, () => RunAsync(() => CreateFromFile(item, () => FileActions.ConvertImage(path, ImageFileFormat.Jpeg)))),
                    Item("Resize to 50%", Symbol.ResizeImage24, () => RunAsync(() => CreateFromFile(item, () => FileActions.ResizeImage(path, 0.5)))),
                    Item("Resize to 25%", Symbol.ResizeImage24, () => RunAsync(() => CreateFromFile(item, () => FileActions.ResizeImage(path, 0.25)))),
                    Separator(),
                    Item("Extract text (OCR)", Symbol.ScanText24, () => RunAsync(() => ExtractText(() => OcrService.RecognizeFileAsync(path)))),
                    Item("Set as wallpaper", Symbol.Desktop24, () => FileActions.SetWallpaper(path))));
            }
            if (single && item.Kind == PouchItemKind.Image && item.ImageContent != null)
            {
                var image = item.ImageContent;
                files.Add(Item("Extract text (OCR)", Symbol.ScanText24, () => RunAsync(() => ExtractText(() => OcrService.RecognizeImageAsync(image)))));
                files.Add(Item("Save as image file…", Symbol.Save24, () => SaveImageAs(item)));
            }

            var share = new List<object>();
            if (existing.Count > 0 || items.Any(i => i.IsTextLike || i.Kind == PouchItemKind.Image))
            {
                share.Add(Item("Share…", Symbol.Share24, () => Share(items, existing)));
            }
            if (ShellContextMenu.CanShow(existing))
            {
                share.Add(Item("More options…", Symbol.MoreHorizontal24, () => ShowExplorerMenu(existing)));
            }

            var remove = new List<object>
            {
                Item(items.Count == 1 ? "Remove from pouch" : $"Remove {items.Count} items from pouch", Symbol.Dismiss24, () => _vm.RemoveItems(items), "Del"),
            };
            if (allFileSystem && existing.Count > 0)
            {
                remove.Add(Item("Delete from disk…", Symbol.Delete24, () => DeleteFromDisk(items, existing), "Shift+Del", danger: true));
            }

            return new List<List<object>> { open, clipboard, organise, files, share, remove };
        }

        // ---------------------------------------------------------------- Background menu

        private List<List<object>> BuildBackgroundMenu()
        {
            bool canPaste = false;
            try
            {
                canPaste = Clipboard.ContainsFileDropList() || Clipboard.ContainsText() || Clipboard.ContainsImage();
            }
            catch (Exception)
            {
                // Clipboard busy; leave Paste disabled.
            }

            var paste = Item("Paste", Symbol.ClipboardPaste24, () => RunAsync(_vm.PasteAsync), "Ctrl+V");
            paste.IsEnabled = canPaste;

            var add = new List<object>
            {
                paste,
                Item("New note…", Symbol.NoteAdd24, () => NewNote(), "Ctrl+N"),
            };
            if (!_vm.IsEmpty) add.Add(Item("Select all", Symbol.SelectAllOn24, () => PouchItemsControl.SelectAll(), "Ctrl+A"));

            var shelves = _vm.Shelves
                .Select(s => (object)Check(s.Name, s.IsActive, () => _vm.ActivateShelf(s), MenuFactory.Dot(s.ColorBrush, ring: s.IsActive)))
                .Append(Separator())
                .Append(Item("New shelf…", Symbol.Add24, () => PromptNewShelf(), "Ctrl+T"))
                .ToArray();
            add.Add(Item("Search…", Symbol.Search24, OpenSearch, "Ctrl+F"));

            var views = Enum.GetValues<PouchViewMode>()
                .Select(mode => (object)Check(mode.ToString(), _vm.ViewMode == mode, () => _vm.SetViewMode(mode)))
                .ToArray();
            var themes = (_vm.ThemeListProvider?.Invoke() ?? Array.Empty<(string Id, string Name)>())
                .Select(t => (object)Check(t.Name, t.Id == _vm.CurrentThemeId, () => _vm.SetTheme(t.Id)))
                .ToArray();

            var look = new List<object> { Submenu("Shelf", Symbol.Tabs24, shelves), Submenu("View", Symbol.Grid24, views) };
            if (themes.Length > 0) look.Add(Submenu("Theme", Symbol.PaintBrush24, themes));

            var manage = new List<object>();
            if (_vm.CanUndo) manage.Add(Item("Undo remove", Symbol.ArrowUndo24, _vm.Undo, "Ctrl+Z"));
            if (_vm.Items.Count > 0) manage.Add(Item("Clear (keeps pinned)", Symbol.Delete24, () => _vm.ClearCommand.Execute(null)));
            manage.Add(Item("Settings…", Symbol.Settings24, () => _vm.OpenSettingsCommand.Execute(null)));

            return new List<List<object>> { add, look, manage };
        }

        // ---------------------------------------------------------------- Actions

        private void OpenItem(PouchItem item)
        {
            switch (item.Kind)
            {
                case PouchItemKind.File or PouchItemKind.Folder when !item.IsMissing:
                    FileActions.Open(item.FilePath!);
                    break;
                case PouchItemKind.Text when TextTools.IsUrl(item.TextContent!):
                case PouchItemKind.Link:
                    FileActions.Open(item.TextContent!.Trim());
                    break;
                case PouchItemKind.Color:
                    PouchViewModel.CopyText(item.DisplayName);
                    break;
                case PouchItemKind.File or PouchItemKind.Folder:
                    PouchDialog.Alert(this, "File not found", $"\"{item.DisplayName}\" was moved or deleted.");
                    break;
                default:
                    OpenQuickLook(item);
                    break;
            }
        }

        private void OpenPaths(IReadOnlyList<string> paths)
        {
            if (paths.Count > MaxItemsToOpenAtOnce &&
                !PouchDialog.Confirm(this, $"Open {paths.Count} items?", "Each one opens in its own window.", "Open all"))
            {
                return;
            }
            foreach (var path in paths) FileActions.Open(path);
        }

        private void Rename(PouchItem item)
        {
            if (item.Kind is PouchItemKind.File or PouchItemKind.Folder && !item.IsMissing)
            {
                string currentName = Path.GetFileName(item.FilePath!.TrimEnd('\\'));
                string? newName = PouchDialog.Prompt(this, "Rename", null, currentName, "Rename",
                    validate: name =>
                    {
                        try
                        {
                            FileActions.ValidateFileName(name);
                            return null;
                        }
                        catch (ArgumentException ex)
                        {
                            return ex.Message;
                        }
                    },
                    selectNameOnly: item.Kind == PouchItemKind.File);
                if (newName == null || newName == currentName) return;

                RunAsync(async () =>
                {
                    string newPath = FileActions.Rename(item.FilePath!, newName);
                    await _vm.UpdatePathsAsync(item, new[] { newPath });
                });
            }
            else if (item.Kind == PouchItemKind.Text)
            {
                EditText(item);
            }
            else
            {
                // Stacks, images and missing files: rename the label only.
                string? label = PouchDialog.Prompt(this, "Rename", "This only changes the name shown in the pouch.", item.DisplayName, "Rename",
                    validate: name => string.IsNullOrWhiteSpace(name) ? "The name can't be empty." : null);
                if (label != null) _vm.Rename(item, label.Trim());
            }
        }

        private void EditText(PouchItem item)
        {
            string? text = PouchDialog.Prompt(this, "Edit note", "Ctrl+Enter to save.", item.TextContent ?? "", "Save", multiline: true,
                validate: t => string.IsNullOrWhiteSpace(t) ? "The note can't be empty." : null);
            if (text != null) _vm.UpdateText(item, text);
        }

        /// <returns>True if a note was added.</returns>
        internal bool NewNote()
        {
            string? text = PouchDialog.Prompt(this, "New note", "Ctrl+Enter to save.", "", "Add", multiline: true,
                validate: t => string.IsNullOrWhiteSpace(t) ? "Write something first." : null);
            if (text == null) return false;
            _vm.AddText(text);
            return true;
        }

        private void MoveOrCopyTo(IReadOnlyList<PouchItem> items, IReadOnlyList<string> paths, bool move)
        {
            var dialog = new OpenFolderDialog { Title = move ? "Move to" : "Copy to" };
            if (dialog.ShowDialog(this) != true) return;
            string folder = dialog.FolderName;

            RunAsync(async () =>
            {
                if (!move)
                {
                    await Task.Run(() => FileActions.CopyTo(paths, folder));
                    return;
                }

                var moved = await Task.Run(() => FileActions.MoveTo(paths, folder));
                var map = paths.Zip(moved).ToDictionary(p => p.First, p => p.Second, StringComparer.OrdinalIgnoreCase);
                foreach (var item in items)
                {
                    var newPaths = item.FilePaths.Select(p => map.TryGetValue(p, out var n) ? n : p).ToList();
                    if (newPaths.Count > 0) await _vm.UpdatePathsAsync(item, newPaths);
                }
            });
        }

        private async Task CreateFromFile(PouchItem source, Func<string> create)
        {
            string path = await Task.Run(create);
            await _vm.InsertAfterAsync(source, path);
        }

        private async Task ExtractText(Func<Task<string?>> recognize)
        {
            string? text = await recognize();
            if (text == null)
            {
                PouchDialog.Alert(this, "Text recognition unavailable",
                    "Windows has no OCR language installed. Add one in Settings → Time & language → Language & region.");
            }
            else if (string.IsNullOrWhiteSpace(text))
            {
                PouchDialog.Alert(this, "No text found", "Pouchy couldn't find any text in this image.");
            }
            else
            {
                PouchViewModel.CopyText(text);
                _vm.AddText(text);
            }
        }

        private void SaveTextAs(PouchItem item)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save note",
                FileName = SafeFileName(item.DisplayName.TrimEnd('.')) + ".txt",
                Filter = "Text file|*.txt|All files|*.*",
            };
            if (dialog.ShowDialog(this) == true) File.WriteAllText(dialog.FileName, item.TextContent);
        }

        private void SaveImageAs(PouchItem item)
        {
            var dialog = new SaveFileDialog { Title = "Save image", FileName = "Image.png", Filter = "PNG image|*.png|JPEG image|*.jpg" };
            if (dialog.ShowDialog(this) != true) return;
            var format = dialog.FilterIndex == 2 ? ImageFileFormat.Jpeg : ImageFileFormat.Png;
            FileActions.SaveImage(item.ImageContent!, dialog.FileName, format);
        }

        private void Share(IReadOnlyList<PouchItem> items, IReadOnlyList<string> existing)
        {
            var paths = existing.ToList();
            foreach (var image in items.Where(i => i.Kind == PouchItemKind.Image && i.ImageContent != null))
            {
                // The share sheet needs real files.
                string temp = Path.Combine(Path.GetTempPath(), "Pouchy", $"Image {image.Id.ToString()[..8]}.png");
                Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
                if (!File.Exists(temp)) FileActions.SaveImage(image.ImageContent!, temp, ImageFileFormat.Png);
                paths.Add(temp);
            }
            string? text = string.Join(Environment.NewLine,
                items.Where(i => i.IsTextLike).Select(i => i.TextContent));
            string title = items.Count == 1 ? items[0].DisplayName : $"{items.Count} items from Pouchy";
            ShareSheet.Show(Handle, paths, text.Length > 0 ? text : null, title);
        }

        private void ShowExplorerMenu(IReadOnlyList<string> paths)
        {
            // Let the WPF menu finish closing before the native one takes over.
            var point = _menuPoint;
            bool extended = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            Dispatcher.BeginInvoke(() =>
            {
                if (ShellContextMenu.Show(Handle, paths, point.x, point.y, extended))
                {
                    _vm.RefreshMissingState();
                }
            });
        }

        private void DeleteFromDisk(IReadOnlyList<PouchItem> items, IReadOnlyList<string> paths)
        {
            string what = paths.Count == 1 ? $"\"{Path.GetFileName(paths[0].TrimEnd('\\'))}\"" : $"these {paths.Count} items";
            if (!PouchDialog.Confirm(this, "Move to Recycle Bin?", $"This moves {what} to the Recycle Bin and removes them from the pouch.",
                    "Delete", destructive: true))
            {
                return;
            }

            if (FileActions.DeleteToRecycleBin(paths, Handle))
            {
                _vm.RemoveItems(items, undoable: false);
            }
            else
            {
                _vm.RefreshMissingState();
            }
        }

        private static string SafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return cleaned.Length > 0 ? cleaned : "Note";
        }

        private void RunAsync(Func<Task> action)
        {
            _ = Execute();

            async Task Execute()
            {
                try
                {
                    await action();
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
                finally
                {
                    _vm.RefreshMissingState();
                }
            }
        }

        private void ReportError(Exception ex)
        {
            Logger.Log("Action failed: " + ex);
            PouchDialog.Alert(this, "That didn't work", ex.Message);
        }

        // ---------------------------------------------------------------- Menu building

        private ContextMenu NewMenu(List<List<object>> groups) => MenuFactory.Create(groups, this);

        private static MenuItem Item(string header, SymbolRegular symbol, Action action, string? gesture = null, bool danger = false) =>
            MenuFactory.Item(header, symbol, action, gesture, danger);

        private static MenuItem Check(string header, bool isChecked, Action action, object? icon = null) =>
            MenuFactory.Check(header, isChecked, action, icon);

        private static MenuItem Submenu(string header, SymbolRegular symbol, params object[] children) =>
            MenuFactory.Submenu(header, symbol, children);

        private static Separator Separator() => MenuFactory.Separator();
    }
}
