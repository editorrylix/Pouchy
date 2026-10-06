using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Pouchy.Services.Theming
{
    /// <summary>Reads Windows' accent colour and light/dark app mode.</summary>
    public static class SystemTheme
    {
        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public static Color GetAccentColor()
        {
            try
            {
                var accent = new Windows.UI.ViewManagement.UISettings()
                    .GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
                return Color.FromArgb(accent.A, accent.R, accent.G, accent.B);
            }
            catch (Exception)
            {
                var glass = SystemParameters.WindowGlassColor;
                return Color.FromRgb(glass.R, glass.G, glass.B);
            }
        }

        public static bool IsLightMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
                return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static ThemeContext CurrentContext(double backgroundOpacity) =>
            new(GetAccentColor(), IsLightMode(), backgroundOpacity);
    }
}
