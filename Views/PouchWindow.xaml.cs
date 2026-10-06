using System.ComponentModel;
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

        // ---------------------------------------------------------------- Drag out / click

        private void PouchItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void PouchItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsBeyondDragThreshold(e) && sender is FrameworkElement { DataContext: PouchItem item })
            {
                OpenQuickLook(item);
            }
        }

        private void PouchItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && IsBeyondDragThreshold(e) &&
                sender is FrameworkElement { DataContext: PouchItem item } element)
            {
                PerformDragOut(element, new[] { item });
            }
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
                element.Opacity = 1.0;
            }
        }

        // ---------------------------------------------------------------- Keyboard

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (PouchItemsControl.SelectedItem is not PouchItem selected)
            {
                if (e.Key == Key.Escape)
                {
                    Despawn();
                    e.Handled = true;
                }
                return;
            }

            switch (e.Key)
            {
                case Key.Space:
                    OpenQuickLook(selected);
                    e.Handled = true;
                    break;
                case Key.Delete:
                    _vm.RemoveCommand.Execute(selected);
                    e.Handled = true;
                    break;
                case Key.C when Keyboard.Modifiers == ModifierKeys.Control:
                    _vm.CopyCommand.Execute(selected);
                    e.Handled = true;
                    break;
                case Key.Escape:
                    Despawn();
                    e.Handled = true;
                    break;
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
