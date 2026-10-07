using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace Pouchy.Services.Theming
{
    /// <summary>Parses theme colour strings into frozen WPF colours and brushes.</summary>
    public static partial class BrushParser
    {
        [GeneratedRegex(@"^linear\((?<body>.*)\)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
        private static partial Regex LinearPattern();

        [GeneratedRegex(@"^(?<color>\S+)(\s+(?<pos>-?[\d.]+)%)?$")]
        private static partial Regex StopPattern();

        /// <exception cref="FormatException">The string isn't a valid colour.</exception>
        public static Color ParseColor(string spec, Color systemAccent)
        {
            string value = spec.Trim();
            if (value.Equals("system", StringComparison.OrdinalIgnoreCase)) return systemAccent;

            try
            {
                if (ColorConverter.ConvertFromString(value) is Color color) return color;
            }
            catch (Exception ex) when (ex is not FormatException)
            {
                // ColorConverter throws assorted exception types for bad input.
            }
            throw new FormatException($"'{spec}' is not a valid colour.");
        }

        /// <exception cref="FormatException">The string isn't a valid colour or gradient.</exception>
        public static Brush ParseBrush(string spec, Color systemAccent)
        {
            Brush brush = LinearPattern().Match(spec.Trim()) is { Success: true } match
                ? ParseLinear(match.Groups["body"].Value, systemAccent)
                : new SolidColorBrush(ParseColor(spec, systemAccent));
            brush.Freeze();
            return brush;
        }

        private static LinearGradientBrush ParseLinear(string body, Color systemAccent)
        {
            var parts = body.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                throw new FormatException("A gradient needs an angle and at least two colours: linear(135deg, #000 0%, #fff 100%).");
            }

            string angleText = parts[0].EndsWith("deg", StringComparison.OrdinalIgnoreCase) ? parts[0][..^3] : parts[0];
            if (!double.TryParse(angleText, NumberStyles.Float, CultureInfo.InvariantCulture, out double angle))
            {
                throw new FormatException($"'{parts[0]}' is not a valid gradient angle.");
            }

            // CSS convention: 0deg points up, 90deg points right.
            double radians = angle * Math.PI / 180;
            double dx = Math.Sin(radians) / 2;
            double dy = -Math.Cos(radians) / 2;
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0.5 - dx, 0.5 - dy),
                EndPoint = new Point(0.5 + dx, 0.5 + dy),
            };

            int stopCount = parts.Length - 1;
            for (int i = 0; i < stopCount; i++)
            {
                var stop = StopPattern().Match(parts[i + 1]);
                if (!stop.Success) throw new FormatException($"'{parts[i + 1]}' is not a valid gradient stop.");

                double offset = stop.Groups["pos"].Success
                    ? double.Parse(stop.Groups["pos"].Value, CultureInfo.InvariantCulture) / 100
                    : (double)i / (stopCount - 1);
                brush.GradientStops.Add(new GradientStop(ParseColor(stop.Groups["color"].Value, systemAccent), offset));
            }
            return brush;
        }
    }
}
