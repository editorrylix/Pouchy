using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Pouchy.Helpers;
using Pouchy.Interop;
using Pouchy.Services;
using Wpf.Ui.Controls;

namespace Pouchy.Views
{
    /// <summary>One entry in the command palette.</summary>
    public sealed class PaletteCommand
    {
        public required string Title { get; init; }
        public string? Subtitle { get; init; }
        public SymbolRegular Icon { get; init; } = SymbolRegular.Flash24;
        public ImageSource? Thumbnail { get; init; }
        public bool HasThumbnail => Thumbnail != null;

        /// <summary>Category tag on the right ("Shelf", "Item", "Theme"...).</summary>
        public string Group { get; init; } = "Command";
        public string? Shortcut { get; init; }

        /// <summary>Extra words that should find this command ("delete" for Remove).</summary>
        public string? Keywords { get; init; }

        public required Action Run { get; init; }

        internal string SearchText => $"{Title} {Group} {Keywords} {Subtitle}";
    }

    /// <summary>
    /// Ctrl+K: type to find any command, item, shelf, theme or action and run it with Enter.
    /// Closes when it loses focus, like the Windows search box.
    /// </summary>
    public partial class CommandPaletteWindow : Window
    {
        private const int MaxResults = 60;
        private readonly IReadOnlyList<PaletteCommand> _commands;
        private bool _closing;

        public CommandPaletteWindow(IReadOnlyList<PaletteCommand> commands)
        {
            InitializeComponent();
            _commands = commands;
            Filter("");
            Closing += (_, _) => _closing = true; // Losing focus while closing mustn't close again.
            Loaded += (_, _) =>
            {
                QueryBox.Focus();
                if (PresentationSource.FromVisual(this) is HwndSource source) NativeMethods.SetForegroundWindow(source.Handle);
            };
        }

        /// <summary>Opens centred near the top of the monitor with the given physical-pixel point (or the cursor).</summary>
        internal void ShowNear(NativeMethods.POINT? point)
        {
            if (point == null && NativeMethods.GetCursorPos(out var cursor)) point = cursor;
            var work = MonitorInfo.FromPoint(point?.x ?? 0, point?.y ?? 0).WorkArea;

            // Place it in physical pixels (like the pouch), so mixed-DPI monitors work.
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -20000;
            Show();
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.GetWindowRect(hwnd, out var rect);
            int x = work.Left + (work.Width - rect.Width) / 2;
            int y = work.Top + (int)(work.Height * 0.16);
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
            Activate();
        }

        private void QueryBox_TextChanged(object sender, TextChangedEventArgs e) => Filter(QueryBox.Text);

        private void Filter(string query)
        {
            query = query.Trim();
            List<PaletteCommand> results = query.Length == 0
                ? _commands.Where(c => c.Group is "Command" or "Selection").Take(MaxResults).ToList()
                : _commands
                    // Scattered letters only count in titles; keywords and details need real words.
                    .Select((c, index) => (Command: c, Score: Math.Max(FuzzyMatch.Score(query, c.Title) * 2, FuzzyMatch.Score(query, c.SearchText, scattered: false)), Index: index))
                    .Where(r => r.Score > 0)
                    .OrderByDescending(r => r.Score)
                    .ThenBy(r => r.Index)
                    .Take(MaxResults)
                    .Select(r => r.Command)
                    .ToList();

            Results.ItemsSource = results;
            Results.SelectedIndex = results.Count > 0 ? 0 : -1;
            EmptyText.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            int count = Results.Items.Count;
            switch (e.Key)
            {
                case Key.Escape:
                    CloseOnce();
                    e.Handled = true;
                    break;
                case Key.Down when count > 0:
                    Results.SelectedIndex = (Results.SelectedIndex + 1) % count;
                    Results.ScrollIntoView(Results.SelectedItem);
                    e.Handled = true;
                    break;
                case Key.Up when count > 0:
                    Results.SelectedIndex = (Results.SelectedIndex - 1 + count) % count;
                    Results.ScrollIntoView(Results.SelectedItem);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    RunSelected();
                    e.Handled = true;
                    break;
            }
        }

        private void Results_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (ItemsControl.ContainerFromElement(Results, (DependencyObject)e.OriginalSource) is ListBoxItem) RunSelected();
        }

        private void RunSelected()
        {
            if (Results.SelectedItem is not PaletteCommand command) return;
            CloseOnce();
            // Run after closing, so dialogs the command opens get focus.
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    command.Run();
                }
                catch (Exception ex)
                {
                    Logger.Log("Palette command failed: " + ex);
                    MenuFactory.ErrorHandler?.Invoke(ex);
                }
            });
        }

        private void Window_Deactivated(object? sender, EventArgs e) => CloseOnce();

        private void CloseOnce()
        {
            if (_closing) return;
            _closing = true;
            Close();
        }
    }
}
