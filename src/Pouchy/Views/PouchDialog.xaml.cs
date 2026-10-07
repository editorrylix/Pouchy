using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Pouchy.Views
{
    /// <summary>A small themed dialog for confirmations and single-value input.</summary>
    public partial class PouchDialog : Window
    {
        private Func<string, string?>? _validate;

        private PouchDialog(Window? owner, string title, string? message, string okText)
        {
            InitializeComponent();
            if (owner is { IsVisible: true }) Owner = owner;
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;

            TitleText.Text = title;
            MessageText.Text = message ?? "";
            MessageText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
            OkButton.Content = okText;
        }

        /// <summary>Asks for a value.</summary>
        /// <param name="validate">Returns an error message for invalid input, or null if it's fine.</param>
        /// <param name="selectNameOnly">Select the file name but not its extension, like Explorer.</param>
        /// <returns>The entered text, or null if cancelled.</returns>
        public static string? Prompt(Window? owner, string title, string? message, string initialText, string okText = "Save",
            bool multiline = false, Func<string, string?>? validate = null, bool selectNameOnly = false)
        {
            var dialog = new PouchDialog(owner, title, message, okText) { _validate = validate };
            dialog.InputBorder.Visibility = Visibility.Visible;
            dialog.InputBox.Text = initialText;
            if (multiline)
            {
                dialog.Width = 460;
                dialog.InputBox.AcceptsReturn = true;
                dialog.InputBox.TextWrapping = TextWrapping.Wrap;
                dialog.InputBox.MinHeight = 120;
                dialog.InputBox.MaxHeight = 320;
                dialog.InputBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                dialog.OkButton.IsDefault = false; // Enter makes a new line; Ctrl+Enter saves.
            }

            dialog.Loaded += (_, _) =>
            {
                dialog.InputBox.Focus();
                int dot = initialText.LastIndexOf('.');
                if (selectNameOnly && dot > 0) dialog.InputBox.Select(0, dot);
                else dialog.InputBox.SelectAll();
            };

            return dialog.ShowDialog() == true ? dialog.InputBox.Text : null;
        }

        /// <summary>Shows a message with a single OK button.</summary>
        public static void Alert(Window? owner, string title, string message)
        {
            var dialog = new PouchDialog(owner, title, message, "OK");
            dialog.CancelButton.Visibility = Visibility.Collapsed;
            dialog.ShowDialog();
        }

        /// <returns>True if the user confirmed.</returns>
        public static bool Confirm(Window? owner, string title, string message, string okText, bool destructive = false)
        {
            var dialog = new PouchDialog(owner, title, message, okText);
            if (destructive)
            {
                dialog.OkButton.SetResourceReference(BackgroundProperty, "Pouch.Danger");
                dialog.OkButton.Foreground = System.Windows.Media.Brushes.White;
                // Don't let a stray Enter delete things.
                dialog.OkButton.IsDefault = false;
                dialog.CancelButton.IsDefault = true;
            }
            dialog.Loaded += (_, _) => dialog.CancelButton.Focus();
            return dialog.ShowDialog() == true;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (_validate != null && _validate(InputBox.Text) is string error)
            {
                ErrorText.Text = error;
                ErrorText.Visibility = Visibility.Visible;
                return;
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e) => ErrorText.Visibility = Visibility.Collapsed;

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Ok_Click(this, e);
                e.Handled = true;
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }
    }
}
