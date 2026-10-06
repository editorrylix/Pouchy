using Pouchy.Models;

namespace Pouchy.Services.Theming
{
    public static class BuiltInThemes
    {
        public const string DefaultId = "midnight";

        public static IReadOnlyList<ThemeDefinition> All() => new[]
        {
            Midnight(),
            FollowWindows(),
            Frost(),
            Paper(),
            Sunset(),
            Terminal(),
        };

        /// <summary>Dark translucent glass with a soft violet accent. The defaults of <see cref="ThemeColors"/>.</summary>
        private static ThemeDefinition Midnight() => new()
        {
            Id = DefaultId,
            Name = "Midnight Glass",
            Base = "dark",
            IsBuiltIn = true,
        };

        private static ThemeDefinition FollowWindows() => new()
        {
            Id = "system",
            Name = "Follow Windows",
            Base = "system",
            IsBuiltIn = true,
            Colors = new ThemeColors { Accent = "system" },
            LightColors = FrostColors("system"),
        };

        private static ThemeDefinition Frost() => new()
        {
            Id = "frost",
            Name = "Frost",
            Base = "light",
            IsBuiltIn = true,
            CornerRadius = 22,
            TileCornerRadius = 14,
            Colors = FrostColors("#2563EB"),
            Shadow = new ThemeShadow { Color = "#1E293B", Blur = 40, Depth = 12, Opacity = 0.28 },
        };

        private static ThemeColors FrostColors(string accent) => new()
        {
            Background = "linear(160deg, #F2FFFFFF 0%, #EBEEF2F8 100%)",
            Border = "linear(180deg, #FFFFFFFF 0%, #40AAB4C3 100%)",
            Accent = accent,
            AccentText = "#FFFFFF",
            Text = "#0F172A",
            TextSecondary = "#A60F172A",
            TextMuted = "#730F172A",
            Tile = "#B3FFFFFF",
            TileHover = "#FFFFFFFF",
            TileBorder = "#1A0F172A",
            Card = "#FFFFFFFF",
            Badge = "#CC0F172A",
            BadgeText = "#FFFFFF",
            Danger = "#E11D48",
            Warning = "#D97706",
            Scrollbar = "#330F172A",
        };

        private static ThemeDefinition Paper() => new()
        {
            Id = "paper",
            Name = "Paper",
            Base = "light",
            IsBuiltIn = true,
            HeaderFontFamily = "Georgia",
            CornerRadius = 14,
            TileCornerRadius = 8,
            Colors = new ThemeColors
            {
                Background = "linear(170deg, #FFFBF6EC 0%, #FFF1E8D8 100%)",
                Border = "#337A6248",
                Accent = "#C2410C",
                AccentText = "#FFF8EC",
                Text = "#2B2118",
                TextSecondary = "#B32B2118",
                TextMuted = "#802B2118",
                Tile = "#99FFFFFF",
                TileHover = "#FFFFFFFF",
                TileBorder = "#1F7A6248",
                Card = "#FFFFFDF8",
                Badge = "#D92B2118",
                BadgeText = "#FFF8EC",
                Danger = "#B91C1C",
                Warning = "#B45309",
                Scrollbar = "#4D7A6248",
            },
            Shadow = new ThemeShadow { Color = "#5C4632", Blur = 30, Depth = 10, Opacity = 0.35 },
        };

        private static ThemeDefinition Sunset() => new()
        {
            Id = "sunset",
            Name = "Sunset",
            Base = "dark",
            IsBuiltIn = true,
            CornerRadius = 20,
            Colors = new ThemeColors
            {
                Background = "linear(135deg, #F52A1B3D 0%, #F5482A55 50%, #F57A3042 100%)",
                Border = "linear(180deg, #55FFC9A8 0%, #14FFC9A8 100%)",
                Accent = "#FF8A5B",
                AccentText = "#1F0F1A",
                Text = "#FFF4EC",
                TextSecondary = "#BFFFE9DC",
                TextMuted = "#80FFE9DC",
                Tile = "#17FFFFFF",
                TileHover = "#29FFFFFF",
                TileBorder = "#1FFFC9A8",
                Card = "#FF3A2440",
                Badge = "#B31A0A14",
                BadgeText = "#FFE9DC",
                Danger = "#FF6B6B",
                Warning = "#FFC46B",
                Scrollbar = "#55FFC9A8",
            },
            Shadow = new ThemeShadow { Color = "#12060E", Blur = 40, Depth = 10, Opacity = 0.7 },
        };

        private static ThemeDefinition Terminal() => new()
        {
            Id = "terminal",
            Name = "Terminal",
            Base = "dark",
            IsBuiltIn = true,
            FontFamily = "Cascadia Mono, Consolas",
            HeaderFontFamily = "Cascadia Mono, Consolas",
            MonoFontFamily = "Cascadia Mono, Consolas",
            CornerRadius = 4,
            TileCornerRadius = 2,
            Colors = new ThemeColors
            {
                Background = "#F5040805",
                Border = "#7339FF88",
                Accent = "#39FF88",
                AccentText = "#03140A",
                Text = "#C8FFDD",
                TextSecondary = "#B339FF88",
                TextMuted = "#7339FF88",
                Tile = "#0D39FF88",
                TileHover = "#2239FF88",
                TileBorder = "#3339FF88",
                Card = "#FF061A0E",
                Badge = "#E6031A0C",
                BadgeText = "#39FF88",
                Danger = "#FF4D4D",
                Warning = "#FFD166",
                Scrollbar = "#6639FF88",
            },
            Shadow = new ThemeShadow { Color = "#39FF88", Blur = 28, Depth = 0, Opacity = 0.3 },
            Effects = new ThemeEffects { Scanlines = true, ScanlineOpacity = 0.07 },
        };
    }
}
