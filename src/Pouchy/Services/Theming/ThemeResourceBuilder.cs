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
        public const string MenuBackground = "Pouch.MenuBackground";
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

        /// <summary>Soft coloured light at the top of the pouch (a radial gradient).</summary>
        public const string Glow = "Pouch.Glow";
        /// <summary>Glassy highlight along the top of the pouch.</summary>
        public const string Sheen = "Pouch.Sheen";
        /// <summary>Highlight along the top of each tile.</summary>
        public const string TileSheen = "Pouch.TileSheen";
        /// <summary>Accent to <see cref="ThemeColors.Accent2"/>, for the Drag all button.</summary>
        public const string AccentGradient = "Pouch.AccentGradient";
        /// <summary>A coloured shadow under accent surfaces.</summary>
        public const string AccentGlow = "Pouch.AccentGlow";
        /// <summary>The selection ring's glow: the accent at half strength, for a soft outer edge.</summary>
        public const string SelectionGlow = "Pouch.SelectionGlow";
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

            bool light = IsLight(theme, context);
            var accent2 = string.IsNullOrWhiteSpace(colors.Accent2) ? ShiftHue(accentColor, 30, 0.88) : Color(colors.Accent2);
            var effects = theme.Effects;
            var glowColor = string.IsNullOrWhiteSpace(effects.GlowColor) ? accentColor : Color(effects.GlowColor);

            return new Dictionary<string, object>
            {
                [ThemeKeys.Background] = background,
                [ThemeKeys.MenuBackground] = Solid(MenuColor(colors.Background)),
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
                [ThemeKeys.Glow] = GlowBrush(glowColor, effects.Glow),
                [ThemeKeys.Sheen] = SheenBrush(effects.Sheen * (light ? 2.5 : 1), 0.38),
                [ThemeKeys.TileSheen] = SheenBrush(light ? 0 : effects.Sheen * 1.4, 0.55),
                [ThemeKeys.AccentGradient] = Gradient(accentColor, accent2),
                [ThemeKeys.AccentGlow] = Glow(Blend(accentColor, accent2, 0.5), 18, 4, effects.AccentGlow),
                [ThemeKeys.SelectionGlow] = Glow(accentColor, 12, 0, effects.AccentGlow * 1.2),
            };

            Color Color(string spec) => BrushParser.ParseColor(spec, context.SystemAccent);

            // Menus and dialogs float over anything, so they use an opaque version of the background.
            Color MenuColor(string spec)
            {
                var color = BrushParser.ParseBrush(spec, context.SystemAccent) switch
                {
                    SolidColorBrush solid => solid.Color,
                    GradientBrush { GradientStops.Count: > 0 } gradient => gradient.GradientStops[0].Color,
                    _ => System.Windows.Media.Color.FromRgb(30, 30, 34),
                };
                return System.Windows.Media.Color.FromArgb(0xFF, color.R, color.G, color.B);
            }
            Brush Brush(string spec) => BrushParser.ParseBrush(spec, context.SystemAccent);

            static SolidColorBrush Solid(Color color)
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
        }

        /// <summary>Light that falls from the top-left of the pouch and fades out by the middle.</summary>
        internal static Brush GlowBrush(Color color, double strength)
        {
            byte alpha = (byte)Math.Round(Math.Clamp(strength, 0, 1) * 255);
            var brush = new RadialGradientBrush
            {
                Center = new Point(0.22, -0.05),
                GradientOrigin = new Point(0.22, -0.05),
                RadiusX = 0.95,
                RadiusY = 0.55,
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(alpha, color.R, color.G, color.B), 0),
                    new GradientStop(Color.FromArgb((byte)(alpha * 0.35), color.R, color.G, color.B), 0.45),
                    new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1),
                },
            };
            brush.Freeze();
            return brush;
        }

        /// <summary>White fading to nothing from the top edge down to <paramref name="reach"/> of the height.</summary>
        internal static Brush SheenBrush(double strength, double reach)
        {
            byte alpha = (byte)Math.Round(Math.Clamp(strength, 0, 1) * 255);
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(alpha, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), Math.Clamp(reach, 0.05, 1)),
                },
            };
            brush.Freeze();
            return brush;
        }

        private static Brush Gradient(Color from, Color to)
        {
            var brush = new LinearGradientBrush(from, to, new Point(0, 0), new Point(1, 1));
            brush.Freeze();
            return brush;
        }

        private static DropShadowEffect Glow(Color color, double blur, double depth, double strength)
        {
            var effect = new DropShadowEffect
            {
                Color = color,
                BlurRadius = blur,
                ShadowDepth = depth,
                Direction = 270,
                Opacity = Math.Clamp(strength, 0, 1),
            };
            effect.Freeze();
            return effect;
        }

        private static Color Blend(Color a, Color b, double t) => Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));

        /// <summary>The same colour with its hue turned by <paramref name="degrees"/> and its lightness scaled.</summary>
        internal static Color ShiftHue(Color c, double degrees, double lightness = 1)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double l = (max + min) / 2, h = 0, s = 0;
            if (max - min > 1e-6)
            {
                double d = max - min;
                s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
                h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
                h *= 60;
            }
            h = ((h + degrees) % 360 + 360) % 360;
            l = Math.Clamp(l * lightness, 0, 1);

            double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            double Channel(double t)
            {
                t = (t % 1 + 1) % 1;
                return t < 1 / 6.0 ? p + (q - p) * 6 * t
                    : t < 0.5 ? q
                    : t < 2 / 3.0 ? p + (q - p) * (2 / 3.0 - t) * 6
                    : p;
            }
            double hk = h / 360;
            return Color.FromArgb(c.A,
                (byte)Math.Round(Channel(hk + 1 / 3.0) * 255),
                (byte)Math.Round(Channel(hk) * 255),
                (byte)Math.Round(Channel(hk - 1 / 3.0) * 255));
        }
    }
}
