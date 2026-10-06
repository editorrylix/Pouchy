using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Pouchy.Models;

namespace Pouchy.Services.Theming
{
    /// <summary>Windows state a theme can depend on.</summary>
    public readonly record struct ThemeContext(Color SystemAccent, bool SystemIsLight, double BackgroundOpacity);

    /// <summary>
    /// Resource keys the pouch XAML binds to with DynamicResource. Changing a theme
    /// replaces these values and the UI updates live.
    /// </summary>
    public static class ThemeKeys
    {
        public const string Background = "Pouch.Background";
        public const string Border = "Pouch.Border";
        public const string Shadow = "Pouch.Shadow";
        public const string CornerRadius = "Pouch.CornerRadius";
        public const string InnerCornerRadius = "Pouch.InnerCornerRadius";
        public const string TileCornerRadius = "Pouch.TileCornerRadius";
        public const string Accent = "Pouch.Accent";
        public const string AccentColor = "Pouch.AccentColor";
        public const string AccentSoft = "Pouch.AccentSoft";
        public const string AccentText = "Pouch.AccentText";
        public const string Text = "Pouch.Text";
        public const string TextSecondary = "Pouch.TextSecondary";
        public const string TextMuted = "Pouch.TextMuted";
        public const string Tile = "Pouch.Tile";
        public const string TileHover = "Pouch.TileHover";
        public const string TileBorder = "Pouch.TileBorder";
        public const string Card = "Pouch.Card";
        public const string Badge = "Pouch.Badge";
        public const string BadgeText = "Pouch.BadgeText";
        public const string Danger = "Pouch.Danger";
        public const string Warning = "Pouch.Warning";
        public const string Scrollbar = "Pouch.Scrollbar";
        public const string FontFamily = "Pouch.FontFamily";
        public const string HeaderFontFamily = "Pouch.HeaderFontFamily";
        public const string MonoFontFamily = "Pouch.MonoFontFamily";
        public const string ScanlineOpacity = "Pouch.ScanlineOpacity";
    }

    public static class ThemeResourceBuilder
    {
        public static bool IsLight(ThemeDefinition theme, ThemeContext context) => theme.Base.ToLowerInvariant() switch
        {
            "light" => true,
            "system" => context.SystemIsLight,
            _ => false,
        };

        public static ThemeColors ResolveColors(ThemeDefinition theme, ThemeContext context) =>
            theme.Base.Equals("system", StringComparison.OrdinalIgnoreCase) && context.SystemIsLight && theme.LightColors != null
                ? theme.LightColors
                : theme.Colors;

        /// <exception cref="FormatException">A colour in the theme is invalid.</exception>
        public static Dictionary<string, object> Build(ThemeDefinition theme, ThemeContext context)
        {
            var colors = ResolveColors(theme, context);
            var accentColor = Color(colors.Accent);

            var background = BrushParser.ParseBrush(colors.Background, context.SystemAccent).CloneCurrentValue();
            background.Opacity = Math.Clamp(context.BackgroundOpacity, 0.1, 1.0);
            background.Freeze();

            var shadow = new DropShadowEffect
            {
                Color = Color(theme.Shadow.Color),
                BlurRadius = Math.Clamp(theme.Shadow.Blur, 0, 60),
                ShadowDepth = Math.Clamp(theme.Shadow.Depth, 0, 30),
                Opacity = Math.Clamp(theme.Shadow.Opacity, 0, 1),
                Direction = 270,
            };
            shadow.Freeze();

            double radius = Math.Clamp(theme.CornerRadius, 0, 40);
            double tileRadius = Math.Clamp(theme.TileCornerRadius, 0, 30);

            return new Dictionary<string, object>
            {
                [ThemeKeys.Background] = background,
                [ThemeKeys.Border] = Brush(colors.Border),
                [ThemeKeys.Shadow] = shadow,
                [ThemeKeys.CornerRadius] = new CornerRadius(radius),
                [ThemeKeys.InnerCornerRadius] = new CornerRadius(Math.Max(0, radius - 6)),
                [ThemeKeys.TileCornerRadius] = new CornerRadius(tileRadius),
                [ThemeKeys.Accent] = Solid(accentColor),
                [ThemeKeys.AccentColor] = accentColor,
                [ThemeKeys.AccentSoft] = Solid(System.Windows.Media.Color.FromArgb(0x33, accentColor.R, accentColor.G, accentColor.B)),
                [ThemeKeys.AccentText] = Brush(colors.AccentText),
                [ThemeKeys.Text] = Brush(colors.Text),
                [ThemeKeys.TextSecondary] = Brush(colors.TextSecondary),
                [ThemeKeys.TextMuted] = Brush(colors.TextMuted),
                [ThemeKeys.Tile] = Brush(colors.Tile),
                [ThemeKeys.TileHover] = Brush(colors.TileHover),
                [ThemeKeys.TileBorder] = Brush(colors.TileBorder),
                [ThemeKeys.Card] = Brush(colors.Card),
                [ThemeKeys.Badge] = Brush(colors.Badge),
                [ThemeKeys.BadgeText] = Brush(colors.BadgeText),
                [ThemeKeys.Danger] = Brush(colors.Danger),
                [ThemeKeys.Warning] = Brush(colors.Warning),
                [ThemeKeys.Scrollbar] = Brush(colors.Scrollbar),
                [ThemeKeys.FontFamily] = new FontFamily(theme.FontFamily),
                [ThemeKeys.HeaderFontFamily] = new FontFamily(theme.HeaderFontFamily),
                [ThemeKeys.MonoFontFamily] = new FontFamily(theme.MonoFontFamily),
                [ThemeKeys.ScanlineOpacity] = theme.Effects.Scanlines ? Math.Clamp(theme.Effects.ScanlineOpacity, 0, 0.5) : 0.0,
            };

            Color Color(string spec) => BrushParser.ParseColor(spec, context.SystemAccent);
            Brush Brush(string spec) => BrushParser.ParseBrush(spec, context.SystemAccent);

            static SolidColorBrush Solid(Color color)
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
        }
    }
}
