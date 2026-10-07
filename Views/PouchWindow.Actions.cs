using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Pouchy.Interop;
using Pouchy.Models;
using Pouchy.Services;
using Pouchy.Services.Actions;
using Pouchy.ViewModels;
using Wpf.Ui.Controls;

namespace Pouchy.Views
{
    /// <summary>A drop action tile shown while files are dragged over the pouch.</summary>
    public sealed record ActionTile(IPouchAction Action, string Label, SymbolRegular Symbol, string ToolTip);

    /// <summary>Drop actions, running actions, and remembering where items were dropped.</summary>
    public partial class PouchWindow
    {
        private const double ActionTileWidth = 74;
        private const double ActionTileHeight = 62;

        /// <summary>Set by the app. Without it the pouch shows no actions.</summary>
        public ActionRegistry? Actions { get; set; }

        /// <summary>Set by the app to play interface sounds.</summary>
        public Action<SoundEvent>? PlaySound { get; set; }

        internal static SymbolRegular ParseSymbol(string name, SymbolRegular fallback = SymbolRegular.Flash24) =>
            Enum.TryParse<SymbolRegular>(name, out var symbol) ? symbol
            : Enum.TryParse(name + "24", out symbol) ? symbol
            : fallback;

        // ---------------------------------------------------------------- Tiles while dragging in

        /// <summary>Fills the action strip for the files being dragged in; returns true if any tiles show.</summary>
        private bool PrepareActionTiles(IDataObject data)
        {
            ActionStrip.ItemsSource = null;
            if (Actions == null || !_vm.ShowDropActions || !data.GetDataPresent(DataFormats.FileDrop)) return false;
            if (data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } paths) return false;

            List<IPouchAction> actions;
            try
            {
                actions = Actions.For(paths, _vm.HiddenActions);
            }
            catch (Exception ex)
            {
                Logger.Log("Could not list drop actions: " + ex.Message);
                return false;
            }

            // Two rows at most, so the pouch doesn't grow taller than the screen.
            int perRow = Math.Max(2, (int)((_vm.ContentWidth + 4) / ActionTileWidth));
            var tiles = actions.Take(perRow * 2)
                .Select(a => new ActionTile(a, a.ShortName, ParseSymbol(a.Icon), a is DelegateAction { ToolTip: { } tip } ? tip : a.Name))
                .ToList();
            if (tiles.Count == 0) return false;

            ActionStrip.ItemsSource = tiles;
            int rows = (tiles.Count + perRow - 1) / perRow;
            DropOverlay.MinHeight = 64 + rows * ActionTileHeight;
            return true;
        }

        private void ShowActionTiles(bool show)
        {
            ActionStrip.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            OverlayContent.VerticalAlignment = show ? VerticalAlignment.Top : VerticalAlignment.Center;
            OverlayContent.Margin = show ? new Thickness(0, 14, 0, 0) : new Thickness(0);
            OverlayMascot.Height = OverlayMascot.Width = show ? 34 : 72;
            if (!show) DropOverlay.MinHeight = 0;
        }

        private void ActionTile_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is Border tile)
            {
                tile.SetResourceReference(Border.BackgroundProperty, "Pouch.Accent");
                tile.SetResourceReference(Border.BorderBrushProperty, "Pouch.Accent");
                SetTileForeground(tile, "Pouch.AccentText");
            }
            ActionTile_DragOver(sender, e);
        }

        private void ActionTile_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void ActionTile_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is not Border tile) return;
            tile.SetResourceReference(Border.BackgroundProperty, "Pouch.MenuBackground");
            tile.SetResourceReference(Border.BorderBrushProperty, "Pouch.TileBorder");
            SetTileForeground(tile, null);
        }

        private static void SetTileForeground(Border tile, string? key)
        {
            if (tile.Child is not StackPanel panel) return;
            foreach (var child in panel.Children.OfType<FrameworkElement>())
            {
                var property = child is SymbolIcon ? Control.ForegroundProperty : System.Windows.Controls.TextBlock.ForegroundProperty;
                if (key != null) child.SetResourceReference(property, key);
                else child.SetResourceReference(property, child is SymbolIcon ? "Pouch.Accent" : "Pouch.Text");
            }
        }

        private void ActionTile_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            ActionTile_DragLeave(sender, e);
            _isDraggingIn = false;
            ShowDropOverlay(false);
            if (sender is not FrameworkElement { DataContext: ActionTile tile }) return;
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } paths) return;

            e.Effects = DragDropEffects.Copy;
            SetMascotMood(MascotMood.Happy);
            RunAsync(() => RunActionAsync(tile.Action, paths, anchor: null));
        }

        // ---------------------------------------------------------------- Running actions

        /// <summary>Runs an action and puts what it made into the pouch (after <paramref name="anchor"/> if given).</summary>
        internal async Task RunActionAsync(IPouchAction action, IReadOnlyList<string> paths, PouchItem? anchor)
        {
            var context = new ActionContext(paths, Handle);
            Logger.Log($"Running action {action.Id} on {paths.Count} item(s).");
            var result = action.NeedsUiThread
                ? await action.RunAsync(context)
                : await Task.Run(() => action.RunAsync(context));

            foreach (var file in result.NewFiles)
            {
                if (anchor != null) await _vm.InsertAfterAsync(anchor, file);
                else await _vm.AddPathsAsync(new[] { file });
            }
            if (!string.IsNullOrWhiteSpace(result.Text))
            {
                PouchViewModel.CopyText(result.Text);
                _vm.AddText(result.Text);
            }
            if (result.NewFiles.Count > 0) PlaySound?.Invoke(SoundEvent.Add);

            string? message = result.Message ?? (result.NewFiles.Count switch
            {
                0 => null,
                1 => $"Added “{Path.GetFileName(result.NewFiles[0])}”",
                int n => $"Added {n} new files",
            });
            if (message != null) _vm.ShowNotice(message);
        }

        // ---------------------------------------------------------------- Recent destinations

        /// <summary>
        /// After a drag out, find the folder the items landed in (desktop or an Explorer window) and
        /// remember it. Explorer may still be copying, so look twice.
        /// </summary>
        private void RememberDropTarget(IReadOnlyCollection<PouchItem> items)
        {
            var paths = items.SelectMany(i => i.FilePaths).ToList();
            if (paths.Count == 0 || !NativeMethods.GetCursorPos(out var point)) return;

            var candidates = DropTargetLocator.CandidateFolders(point.x, point.y);
            if (candidates.Count == 0) return;

            int attempt = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            timer.Tick += (_, _) =>
            {
                attempt++;
                if (DropTargetLocator.Confirm(candidates, paths) is { } folder)
                {
                    timer.Stop();
                    _vm.RecordDestination(folder);
                    Logger.Log("Remembered drop target: " + folder);
                }
                else if (attempt >= 3)
                {
                    timer.Stop();
                }
                else
                {
                    timer.Interval = TimeSpan.FromSeconds(2);
                }
            };
            timer.Start();
        }
    }
}
