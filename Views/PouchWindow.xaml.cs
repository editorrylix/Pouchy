using System.ComponentModel;
using System.IO;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Pouchy.Helpers;
using Pouchy.Interop;
using Pouchy.Models;
using Pouchy.Services;
using Pouchy.ViewModels;

namespace Pouchy.Views
{
    /// <summary>
    /// The floating pouch. Data lives in <see cref="PouchViewModel"/>; this class only
    /// handles window behaviour: positioning, docking, animation and drag-and-drop.
    /// Positions are computed in physical pixels so mixed-DPI monitors work.
    /// </summary>
    public partial class PouchWindow : Window
    {
        private const double ShadowMargin = 30;   // RootGrid margin reserved for the drop shadow
        private const double CursorOffset = 40;
        private const double DockThreshold = 30;
        private const double UndockNudge = 20;
        private const double SlideDistance = 28;

        private readonly PouchViewModel _vm;
        private Point _dragStartPoint;
        private bool _isDraggingIn;
        private bool _isDocked;
        private bool _dockedLeft;
        private int _preDockLeftPx;
        private int _animationVersion;

        /// <summary>Set by the app on shutdown so closing isn't turned into hiding.</summary>
        public bool AllowClose { get; set; }

        public PouchWindow(PouchViewModel viewModel)
        {
            InitializeComponent();
            _vm = viewModel;
            DataContext = viewModel;
            InitializeShelves();
        }

        private IntPtr Handle => new WindowInteropHelper(this).EnsureHandle();

        private double DpiScale => VisualTreeHelper.GetDpi(this).DpiScaleX;

        private int ToPx(double dip) => (int)Math.Round(dip * DpiScale);

        // ---------------------------------------------------------------- Show / hide

        /// <summary>Shows the pouch next to a physical-pixel screen point.</summary>
        public void SpawnAt(int x, int y)
        {
            _animationVersion++; // Cancels a hide queued by a running Despawn animation.

            if (!IsVisible) Show();
            if (_isDocked) Undock();
            _vm.RefreshMissingState();
            UpdateLayout();

            IntPtr hwnd = Handle;
            var monitor = MonitorInfo.FromPoint(x, y);
            NativeMethods.GetWindowRect(hwnd, out var rect);
            int width = rect.Width;
            int height = rect.Height;
            int offset = (int)(CursorOffset * monitor.Scale);
            int shadow = (int)(ShadowMargin * monitor.Scale);
            var work = monitor.WorkArea;

            int left = x + offset;
            bool opensRightOfCursor = true;
            if (left + width - shadow > work.Right)
            {
                left = x - width - offset; // Not enough room on the right: open to the left.
                opensRightOfCursor = false;
            }
            int top = y - height / 2;

            // Keep the visible card (not the transparent shadow margin) inside the work area.
            left = Clamp(left, work.Left - shadow, work.Right - width + shadow);
            top = Clamp(top, work.Top - shadow, work.Bottom - height + shadow);

            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, left, top, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

            AnimateIn(opensRightOfCursor);
        }

        /// <summary>Shows the pouch if hidden, hides it if shown.</summary>
        public void Toggle(int x, int y)
        {
            if (IsVisible && !_isDocked)
            {
                Despawn();
            }
            else
            {
                SpawnAt(x, y);
                Activate();
            }
        }

        public void Despawn()
        {
            int version = ++_animationVersion;
            if (_vm.SpawnAnimation == SpawnAnimation.None)
            {
                Hide();
                return;
            }

            var fadeOut = Animation(1, 0, 160, new CubicEase { EasingMode = EasingMode.EaseIn });
            fadeOut.Completed += (_, _) =>
            {
                if (version == _animationVersion) Hide();
            };
            MainContainer.BeginAnimation(OpacityProperty, fadeOut);

            if (_vm.SpawnAnimation == SpawnAnimation.Pop)
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
                WindowScale.BeginAnimation(ScaleTransform.ScaleXProperty, Animation(1.0, 0.94, 160, ease));
                WindowScale.BeginAnimation(ScaleTransform.ScaleYProperty, Animation(1.0, 0.94, 160, ease));
            }
        }

        private void AnimateIn(bool fromLeft)
        {
            ResetTransforms();
            var style = _vm.SpawnAnimation;
            if (style == SpawnAnimation.None)
            {
                MainContainer.Opacity = 1;
                return;
            }

            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            MainContainer.BeginAnimation(OpacityProperty, Animation(0, 1, 180, easeOut));

            switch (style)
            {
                case SpawnAnimation.Pop:
                    var pop = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };
                    WindowScale.BeginAnimation(ScaleTransform.ScaleXProperty, Animation(0.86, 1.0, 260, pop));
                    WindowScale.BeginAnimation(ScaleTransform.ScaleYProperty, Animation(0.86, 1.0, 260, pop));
                    break;
                case SpawnAnimation.Slide:
                    double distance = fromLeft ? -SlideDistance : SlideDistance;
                    WindowSlide.BeginAnimation(TranslateTransform.XProperty, Animation(distance, 0, 260, new QuinticEase { EasingMode = EasingMode.EaseOut }));
                    break;
            }
        }

        private void ResetTransforms()
        {
            MainContainer.BeginAnimation(OpacityProperty, null);
            WindowScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            WindowScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            WindowSlide.BeginAnimation(TranslateTransform.XProperty, null);
            WindowScale.ScaleX = WindowScale.ScaleY = 1;
            WindowSlide.X = 0;
        }

        private static DoubleAnimation Animation(double from, double to, double milliseconds, IEasingFunction easing) =>
            new(from, to, Motion.Duration(milliseconds)) { EasingFunction = easing };

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (AllowClose) return;
            e.Cancel = true; // Alt+F4 hides the pouch instead of destroying it.
            Despawn();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Despawn();

        // ---------------------------------------------------------------- Edge docking

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            DragMove();
            CheckEdgeDock();
        }

        private void CheckEdgeDock()
        {
            if (_vm.IsDraggingOut || _isDraggingIn || _isDocked) return;

            IntPtr hwnd = Handle;
            if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return;
            var screen = MonitorInfo.FromWindow(hwnd).Bounds;
            int shadow = ToPx(ShadowMargin);
            int threshold = ToPx(DockThreshold);

            if (Math.Abs(rect.Left + shadow - screen.Left) < threshold)
            {
                DockToEdge(left: true, screen, rect);
            }
            else if (Math.Abs(screen.Right - (rect.Right - shadow)) < threshold)
            {
                DockToEdge(left: false, screen, rect);
            }
        }

        private void DockToEdge(bool left, NativeMethods.RECT screen, NativeMethods.RECT windowRect)
        {
            _isDocked = true;
            _dockedLeft = left;
            _preDockLeftPx = windowRect.Left;

            MainContainer.Visibility = Visibility.Collapsed;
            (left ? EdgeTabLeft : EdgeTabRight).Visibility = Visibility.Visible;

            // The window shrinks to the tab, so measure it again before placing it.
            UpdateLayout();
            NativeMethods.GetWindowRect(Handle, out var tabRect);
            int shadow = ToPx(ShadowMargin);
            int x = left ? screen.Left - shadow : screen.Right - tabRect.Width + shadow;
            NativeMethods.SetWindowPos(Handle, IntPtr.Zero, x, windowRect.Top, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }

        private void EdgeTab_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_isDocked) Undock();
        }

        private void Undock()
        {
            _isDocked = false;
            EdgeTabLeft.Visibility = Visibility.Collapsed;
            EdgeTabRight.Visibility = Visibility.Collapsed;
            MainContainer.Visibility = Visibility.Visible;

            // Nudge away from the edge so it doesn't immediately re-dock.
            int nudge = ToPx(UndockNudge);
            NativeMethods.GetWindowRect(Handle, out var rect);
            int x = _dockedLeft ? _preDockLeftPx + nudge : _preDockLeftPx - nudge;
            NativeMethods.SetWindowPos(Handle, IntPtr.Zero, x, rect.Top, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }

        // ---------------------------------------------------------------- Drag in

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (!_vm.IsDraggingOut && !_isDraggingIn)
            {
                _isDraggingIn = true;
                ShowDropOverlay(true);
            }
            e.Effects = GetDragEffect(e);
            e.Handled = true;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = GetDragEffect(e);
            e.Handled = true;
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            // DragLeave also fires when moving between child elements; only react when really leaving.
            var position = e.GetPosition(MainContainer);
            bool inside = position.X >= 0 && position.Y >= 0 &&
                          position.X < MainContainer.ActualWidth && position.Y < MainContainer.ActualHeight;
            if (inside) return;

            _isDraggingIn = false;
            ShowDropOverlay(false);
        }

        private void ShowDropOverlay(bool show)
        {
            var animation = new DoubleAnimation(show ? 1 : 0, Motion.Enabled ? Motion.Duration(show ? 120 : 180) : TimeSpan.Zero);
            DropOverlay.BeginAnimation(OpacityProperty, animation);
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            _isDraggingIn = false;
            ShowDropOverlay(false);
            if (_vm.IsDraggingOut) return; // Dropped back onto ourselves.

            e.Effects = GetDragEffect(e);
            try
            {
                // Read everything from the data object before the first await.
                var data = e.Data;
                if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
                {
                    await _vm.AddPathsAsync(files);
                }
                else if ((data.GetData(DataFormats.UnicodeText) ?? data.GetData(DataFormats.Text)) is string text)
                {
                    _vm.AddText(text);
                }
                else if (data.GetDataPresent(DataFormats.Bitmap))
                {
                    if (data.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
                    {
                        _vm.AddImage(bitmap);
                    }
                    else
                    {
                        Logger.Log("Dropped bitmap is in an unsupported format.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error handling drop: " + ex);
            }
        }

        private static DragDropEffects GetDragEffect(DragEventArgs e)
        {
            var modifiers = Keyboard.Modifiers;
            if (modifiers.HasFlag(ModifierKeys.Control)) return DragDropEffects.Copy;
            if (modifiers.HasFlag(ModifierKeys.Shift)) return DragDropEffects.Move;
            if (modifiers.HasFlag(ModifierKeys.Alt)) return DragDropEffects.Link;

            if (e.AllowedEffects.HasFlag(DragDropEffects.Move)) return DragDropEffects.Move;
            if (e.AllowedEffects.HasFlag(DragDropEffects.Copy)) return DragDropEffects.Copy;
            return DragDropEffects.None;
        }

        // ---------------------------------------------------------------- Click, select, drag out

        private bool _pressedOnItem;
        private PouchItem? _collapseSelectionTo;

        private void PouchItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: PouchItem item }) return;
            if (IsInsideButton(e.OriginalSource as DependencyObject)) return; // Tile copy/remove buttons.

            _dragStartPoint = e.GetPosition(null);
            _pressedOnItem = true;
            _collapseSelectionTo = null;

            if (e.ClickCount == 2)
            {
                _pressedOnItem = false;
                OpenItem(item);
                e.Handled = true;
                return;
            }

            // Pressing on an item that's part of a multi-selection: keep the selection so it can
            // be dragged as a group. A plain click (no drag) narrows it on mouse up instead.
            if (PouchItemsControl.SelectedItems.Count > 1 && PouchItemsControl.SelectedItems.Contains(item) &&
                Keyboard.Modifiers == ModifierKeys.None)
            {
                _collapseSelectionTo = item;
                e.Handled = true;
            }
        }

        private void PouchItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_collapseSelectionTo != null && !IsBeyondDragThreshold(e))
            {
                PouchItemsControl.SelectedItems.Clear();
                PouchItemsControl.SelectedItem = _collapseSelectionTo;
            }
            _collapseSelectionTo = null;
            _pressedOnItem = false;
        }

        private void PouchItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_pressedOnItem || e.LeftButton != MouseButtonState.Pressed || !IsBeyondDragThreshold(e)) return;
            if (sender is not FrameworkElement { DataContext: PouchItem item } element) return;

            _pressedOnItem = false;
            _collapseSelectionTo = null;

            // Dragging a selected item drags the whole selection.
            IReadOnlyCollection<PouchItem> items = PouchItemsControl.SelectedItems.Contains(item)
                ? GetSelectedItems()
                : new[] { item };
            PerformDragOut(element, items);
        }

        private static bool IsInsideButton(DependencyObject? element)
        {
            for (var current = element; current != null; current = current is Visual or System.Windows.Media.Media3D.Visual3D
                     ? VisualTreeHelper.GetParent(current)
                     : LogicalTreeHelper.GetParent(current))
            {
                if (current is System.Windows.Controls.Primitives.ButtonBase) return true;
                if (current is ListBoxItem) return false;
            }
            return false;
        }

        private void DragAll_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void DragAll_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && IsBeyondDragThreshold(e) &&
                sender is FrameworkElement element && _vm.Items.Count > 0)
            {
                PerformDragOut(element, _vm.Items.ToList());
            }
        }

        private bool IsBeyondDragThreshold(MouseEventArgs e)
        {
            Vector diff = _dragStartPoint - e.GetPosition(null);
            return Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                   Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance;
        }

        private void PerformDragOut(FrameworkElement element, IReadOnlyCollection<PouchItem> items)
        {
            var data = DataObjectBuilder.Build(items);
            if (data == null) return;

            element.Opacity = 0.5;
            _vm.IsDraggingOut = true;
            _draggedItems = items;
            bool wasTopmost = Topmost;
            Topmost = false;

            try
            {
                // A dummy source stops WPF from drawing its own drag preview.
                DragDrop.DoDragDrop(new DependencyObject(), data, DragDropEffects.All);
            }
            catch (Exception ex)
            {
                Logger.Log("Drag out failed: " + ex.Message);
            }
            finally
            {
                Topmost = wasTopmost;
                _vm.IsDraggingOut = false;
                _draggedItems = null;
                element.Opacity = 1.0;
            }
        }

        // ---------------------------------------------------------------- Keyboard

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var modifiers = Keyboard.Modifiers;
            bool ctrl = modifiers == ModifierKeys.Control;

            // Typing in the search box: only a few keys mean something to the pouch.
            if (Keyboard.FocusedElement is TextBox)
            {
                if (HandleSearchBoxKey(key)) e.Handled = true;
                return;
            }

            var selected = GetSelectedItems();
            bool handled = true;

            switch (key)
            {
                case Key.F when ctrl:
                    OpenSearch();
                    break;
                case Key.Z when ctrl:
                    _vm.Undo();
                    break;
                case Key.T when ctrl:
                    PromptNewShelf();
                    break;
                case Key.Tab when ctrl:
                    _vm.CycleShelf(1);
                    break;
                case Key.Tab when modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                    _vm.CycleShelf(-1);
                    break;
                case >= Key.D1 and <= Key.D9 when ctrl && key - Key.D1 < _vm.Shelves.Count:
                    _vm.ActivateShelf(_vm.Shelves[key - Key.D1]);
                    break;
                case Key.Escape when _vm.IsSearchOpen && selected.Count == 0:
                    _vm.IsSearchOpen = false;
                    break;
                case Key.Escape when selected.Count > 0:
                    PouchItemsControl.SelectedItems.Clear();
                    break;
                case Key.Escape:
                    Despawn();
                    break;
                case Key.A when ctrl:
                    PouchItemsControl.SelectAll();
                    break;
                case Key.V when ctrl:
                    RunAsync(_vm.PasteAsync);
                    break;
                case Key.N when ctrl:
                    NewNote();
                    break;
                case Key.Apps:
                case Key.F10 when modifiers == ModifierKeys.Shift:
                    ShowItemMenuFromKeyboard();
                    break;
                default:
                    handled = selected.Count > 0 && HandleSelectionKey(key, modifiers, selected);
                    break;
            }
            if (handled) e.Handled = true;
        }

        /// <returns>True if the key did something.</returns>
        private bool HandleSelectionKey(Key key, ModifierKeys modifiers, List<PouchItem> selected)
        {
            bool ctrl = modifiers == ModifierKeys.Control;
            var paths = selected.SelectMany(i => i.FilePaths).ToList();
            var existing = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();

            switch (key)
            {
                case Key.Space:
                    OpenQuickLook(selected[0]);
                    return true;
                case Key.Enter:
                    if (selected.Count == 1) OpenItem(selected[0]);
                    else OpenPaths(existing);
                    return true;
                case Key.F2 when selected.Count == 1:
                    Rename(selected[0]);
                    return true;
                case Key.Delete when modifiers == ModifierKeys.Shift && existing.Count > 0 && selected.All(i => i.IsFileSystemItem):
                    DeleteFromDisk(selected, existing);
                    return true;
                case Key.Delete:
                    _vm.RemoveItems(selected);
                    return true;
                case Key.C when ctrl:
                    PouchViewModel.CopyItems(selected);
                    return true;
                case Key.C when modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && paths.Count > 0:
                    PouchViewModel.CopyText(string.Join(Environment.NewLine, paths));
                    return true;
                case Key.P when ctrl:
                    _vm.TogglePin(selected);
                    return true;
                case Key.G when ctrl && selected.Count > 1 && selected.All(i => i.IsFileSystemItem):
                    RunAsync(() => _vm.GroupAsync(selected));
                    return true;
                default:
                    return false;
            }
        }

        private static void OpenQuickLook(PouchItem item)
        {
            if (item.IsQuickLookSupported)
            {
                new QuickLookWindow(item).Show();
            }
        }

        private static int Clamp(int value, int min, int max) => max < min ? min : Math.Clamp(value, min, max);
    }
}
