using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Pouchy.Interop;
using Pouchy.Services;
using Pouchy.ViewModels;
using Wpf.Ui.Controls;

namespace Pouchy.Views
{
    public partial class ThemeEditorWindow : FluentWindow
    {
        // The colour dialog's 16 "custom colours" slots, kept for as long as Pouchy runs.
        private static readonly int[] CustomColors = new int[16];

        private readonly ThemeEditorViewModel _vm;

        public ThemeEditorWindow(ThemeEditorViewModel viewModel)
        {
            InitializeComponent();
            _vm = viewModel;
            DataContext = viewModel;
        }

        /// <summary>Opens Windows' colour picker for a field, keeping the field's transparency.</summary>
        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: ThemeColorField field }) return;
            if (PickColor(new WindowInteropHelper(this).Handle, field.Color) is Color picked)
            {
                field.Color = Color.FromArgb(field.Color.A, picked.R, picked.G, picked.B);
            }
        }

        internal static Color? PickColor(IntPtr owner, Color initial)
        {
            var custom = Marshal.AllocHGlobal(CustomColors.Length * sizeof(int));
            try
            {
                Marshal.Copy(CustomColors, 0, custom, CustomColors.Length);
                var dialog = new NativeMethods.CHOOSECOLOR
                {
                    lStructSize = Marshal.SizeOf<NativeMethods.CHOOSECOLOR>(),
                    hwndOwner = owner,
                    rgbResult = initial.R | (initial.G << 8) | (initial.B << 16), // COLORREF is 0x00BBGGRR.
                    lpCustColors = custom,
                    Flags = NativeMethods.CC_RGBINIT | NativeMethods.CC_FULLOPEN,
                };
                if (!NativeMethods.ChooseColor(ref dialog)) return null;
                Marshal.Copy(custom, CustomColors, 0, CustomColors.Length);
                int rgb = dialog.rgbResult;
                return Color.FromRgb((byte)(rgb & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)((rgb >> 16) & 0xFF));
            }
            finally
            {
                Marshal.FreeHGlobal(custom);
            }
        }

        private void EditJson_Click(object sender, RoutedEventArgs e)
        {
            _vm.Save();
            try
            {
                FileActions.Open(_vm.FilePath);
            }
            catch (Exception ex)
            {
                Logger.Log("Could not open the theme file: " + ex.Message);
            }
        }

        private void Done_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closing(object? sender, CancelEventArgs e) => _vm.Save();
    }
}
