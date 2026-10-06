using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Pouchy.Helpers
{
    /// <summary>true → Collapsed, false → Visible.</summary>
    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility v && v != Visibility.Visible;
    }

    /// <summary>null → Visible, anything else → Collapsed. Used for icon fallbacks.</summary>
    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value == null ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Multiplies a number by the converter parameter, e.g. tile size × 0.45 for icon size.</summary>
    public sealed class MultiplyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double factor = double.TryParse(parameter?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 1;
            return value is double d ? d * factor : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
