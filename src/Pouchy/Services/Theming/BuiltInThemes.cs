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
            Aurora(),
            Bubblegum(),
            Acid(),
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

        /// <summary>Night sky: deep navy glass, a teal-to-periwinkle accent and northern-lights glow.</summary>
        private static ThemeDefinition Aurora() => new()
        {
            Id = "aurora",
            Name = "Aurora",
            Base = "dark",
            IsBuiltIn = true,
            CornerRadius = 20,
            TileCornerRadius = 13,
            Colors = new ThemeColors
            {
                Background = "linear(165deg, #F40E1A2A 0%, #F40A0F1C 55%, #F4140E26 100%)",
                Border = "linear(180deg, #5584F5E1 0%, #148B7CFF 100%)",
                Accent = "#4FE3C1",
                Accent2 = "#7C8CFF",
                AccentText = "#04201A",
                Text = "#EAFBFF",
                TextSecondary = "#B8EAFBFF",
                TextMuted = "#70EAFBFF",
                Tile = "#10FFFFFF",
                TileHover = "#20FFFFFF",
                TileBorder = "#1A84F5E1",
                Card = "#FF14223A",
                Badge = "#B3020A12",
                BadgeText = "#EAFBFF",
                Danger = "#FF6B81",
                Warning = "#FFC46B",
                Scrollbar = "#4484F5E1",
            },
            Shadow = new ThemeShadow { Color = "#020812", Blur = 44, Depth = 10, Opacity = 0.7 },
            Effects = new ThemeEffects { Glow = 0.34, GlowColor = "#2DD4BF", AccentGlow = 0.5 },
        };

        /// <summary>Y2K pastel: pink to lilac to baby blue, hot-pink accent, extra round.</summary>
        private static ThemeDefinition Bubblegum() => new()
        {
            Id = "bubblegum",
            Name = "Bubblegum",
            Base = "light",
            IsBuiltIn = true,
            CornerRadius = 26,
            TileCornerRadius = 17,
            Colors = new ThemeColors
            {
                Background = "linear(150deg, #F8FFEEF7 0%, #F8F5EAFF 55%, #F8E8F1FF 100%)",
                Border = "linear(180deg, #FFFFFFFF 0%, #66E9A8D6 100%)",
                Accent = "#FF3D9A",
                Accent2 = "#9B5CFF",
                AccentText = "#FFFFFF",
                Text = "#3A0F2E",
                TextSecondary = "#B33A0F2E",
                TextMuted = "#803A0F2E",
                Tile = "#B3FFFFFF",
                TileHover = "#FFFFFFFF",
                TileBorder = "#26C0398A",
                Card = "#FFFFFFFF",
                Badge = "#D93A0F2E",
                BadgeText = "#FFFFFF",
                Danger = "#E11D48",
                Warning = "#D97706",
                Scrollbar = "#40C0398A",
            },
            Shadow = new ThemeShadow { Color = "#C0398A", Blur = 40, Depth = 12, Opacity = 0.3 },
            Effects = new ThemeEffects { Glow = 0.2, GlowColor = "#FF8AD0", Sheen = 0.12, AccentGlow = 0.45 },
        };

        /// <summary>Graphite black with an acid-lime accent. Loud on purpose.</summary>
        private static ThemeDefinition Acid() => new()
        {
            Id = "acid",
            Name = "Acid",
            Base = "dark",
            IsBuiltIn = true,
            CornerRadius = 16,
            TileCornerRadius = 10,
            Colors = new ThemeColors
            {
                Background = "linear(170deg, #F7171717 0%, #F70A0A0A 100%)",
                Border = "linear(180deg, #40FFFFFF 0%, #0FFFFFFF 100%)",
                Accent = "#D4FF3A",
                Accent2 = "#6BFF8E",
                AccentText = "#0B0B0B",
                Text = "#F4F4F0",
                TextSecondary = "#B3F4F4F0",
                TextMuted = "#66F4F4F0",
                Tile = "#0FFFFFFF",
                TileHover = "#1FFFFFFF",
                TileBorder = "#14FFFFFF",
                Card = "#FF262626",
                Badge = "#E6000000",
                BadgeText = "#D4FF3A",
                Danger = "#FF5A52",
                Warning = "#FFB27A",
                Scrollbar = "#44FFFFFF",
            },
            Shadow = new ThemeShadow { Color = "#000000", Blur = 36, Depth = 8, Opacity = 0.65 },
            Effects = new ThemeEffects { Glow = 0.13, Sheen = 0.05, AccentGlow = 0.38 },
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
            Effects = new ThemeEffects { Glow = 0.14, Sheen = 0.1, AccentGlow = 0.35 },
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
                Accent2 = "#9A3412",
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
            Effects = new ThemeEffects { Glow = 0, Sheen = 0, AccentGlow = 0.22 },
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
                Accent2 = "#FF5C8A",
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
            Effects = new ThemeEffects { Glow = 0.2, GlowColor = "#FF8A5B" },
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
                Accent2 = "#39FF88",
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
            Effects = new ThemeEffects { Scanlines = true, ScanlineOpacity = 0.07, Glow = 0.1, Sheen = 0, AccentGlow = 0.5 },
        };
    }
}
