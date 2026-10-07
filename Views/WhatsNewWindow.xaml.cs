using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using Pouchy.Helpers;
using Pouchy.Interop;
using Pouchy.Services;

namespace Pouchy.Views
{
    /// <summary>Shown once after Pouchy updates (and from the tray menu): what changed, from CHANGELOG.md.</summary>
    public partial class WhatsNewWindow : Window
    {
        public const string ChangelogUrl = "https://github.com/editorrylix/Pouchy/blob/main/CHANGELOG.md";

        private readonly Action<string> _openLink;

        /// <param name="releases">Newest first.</param>
        /// <param name="previous">The version the user had before, if known.</param>
        public WhatsNewWindow(IReadOnlyList<ReleaseNotes> releases, Version current, Version? previous, Action<string> openLink)
        {
            InitializeComponent();
            _openLink = openLink;

            TitleText.Text = previous != null && previous < current
                ? $"Pouchy is updated to {current.ToString(3)}"
                : $"What's new in Pouchy {current.ToString(3)}";
            SubtitleText.Text = previous != null && previous < current
                ? $"You had version {previous.ToString(3)}. Here's what changed."
                : "Here's what changed in this version.";

            foreach (var release in releases)
            {
                if (releases.Count > 1 || release.Version != current)
                {
                    var heading = new Paragraph(new Run($"Version {release.Version.ToString(3)}{(release.Date != null ? " · " + release.Date : "")}"))
                    {
                        FontSize = 15,
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, NotesDocument.Blocks.Count == 0 ? 0 : 16, 0, 2),
                    };
                    heading.SetResourceReference(TextElement.ForegroundProperty, "Pouch.Accent");
                    NotesDocument.Blocks.Add(heading);
                }
                NotesDocument.Blocks.AddRange(MarkdownLite.Render(release.Markdown, openLink));
            }

            Loaded += (_, _) =>
            {
                HeroMascot.Mood = MascotMood.Happy;
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

        private void FullChangelog_Click(object sender, RoutedEventArgs e) => _openLink(ChangelogUrl);
    }
}
