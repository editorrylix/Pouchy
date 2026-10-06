using System.ComponentModel;
using System.Windows.Input;
using Pouchy.ViewModels;
using Wpf.Ui.Controls;

namespace Pouchy.Views
{
    public partial class SettingsWindow : FluentWindow
    {
        private static readonly HashSet<Key> ModifierOnlyKeys = new()
        {
            Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt,
            Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin,
        };

        private readonly SettingsViewModel _vm;

        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            _vm = viewModel;
            DataContext = viewModel;
        }

        /// <summary>Records the pressed key combination as the new hotkey.</summary>
        private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Tab) return; // Let focus move on.

            e.Handled = true;
            if (ModifierOnlyKeys.Contains(key)) return;

            var modifiers = Keyboard.Modifiers;
            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) modifiers |= ModifierKeys.Windows;
            _vm.SetHotkey(modifiers, key);
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            // Text boxes save on lost focus; make sure the last edit isn't lost.
            if (Keyboard.FocusedElement is System.Windows.Controls.TextBox box)
            {
                box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
            }
        }
    }
}
