using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.ComponentModel;
using Pouchy.Interop;

namespace Pouchy.Views
{
    /// <summary>What the welcome window shows and the three choices it offers.</summary>
    public partial class WelcomeModel : ObservableObject
    {
        public required string HotkeyText { get; init; }
        public required string ScreenshotText { get; init; }

        [ObservableProperty] private bool _startWithWindows;
        [ObservableProperty] private bool _explorerMenu;
        [ObservableProperty] private bool _clipboardHistory;

        public Action<bool>? StartWithWindowsChanged { get; init; }
        public Action<bool>? ExplorerMenuChanged { get; init; }
        public Action<bool>? ClipboardHistoryChanged { get; init; }

        partial void OnStartWithWindowsChanged(bool value) => StartWithWindowsChanged?.Invoke(value);
        partial void OnExplorerMenuChanged(bool value) => ExplorerMenuChanged?.Invoke(value);
        partial void OnClipboardHistoryChanged(bool value) => ClipboardHistoryChanged?.Invoke(value);
    }

    /// <summary>
    /// Shown the first time Pouchy runs (and from the tray menu): Pouchy is running, where it lives,
    /// and how to use it in four steps.
    /// </summary>
    public partial class WelcomeWindow : Window
    {
        public const string GuideUrl = "https://github.com/editorrylix/Pouchy#quick-start";

        /// <summary>Set by the app: opens the pouch so the user can try it.</summary>
        public Action? TryItNow { get; set; }

        /// <summary>Set by the app: opens a web page.</summary>
        public Action<string>? OpenLink { get; set; }

        public WelcomeWindow(WelcomeModel model)
        {
            InitializeComponent();
            DataContext = model;
            Loaded += (_, _) =>
            {
                HeroMascot.Mood = MascotMood.Happy; // A little hello.
                // Started from the tray or at sign-in, Windows may not let a new window take focus; ask for it.
                if (PresentationSource.FromVisual(this) is HwndSource source) NativeMethods.SetForegroundWindow(source.Handle);
                Activate();
            };
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Guide_Click(object sender, RoutedEventArgs e) => OpenLink?.Invoke(GuideUrl);

        private void TryIt_Click(object sender, RoutedEventArgs e)
        {
            Close();
            TryItNow?.Invoke();
        }
    }
}
