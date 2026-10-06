using System.Text.Json.Serialization;

namespace Pouchy.Models
{
    /// <summary>
    /// A pouch theme as stored in JSON. Every property has a default, so a custom theme
    /// only needs the values it changes.
    ///
    /// Colour values accept "#RGB", "#RRGGBB", "#AARRGGBB", named colours ("White"),
    /// "system" (the Windows accent colour) and, for brushes, CSS-style gradients:
    /// "linear(135deg, #FF2A1B3D 0%, #FF6B2D3C 100%)".
    /// </summary>
    public class ThemeDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Author { get; set; }

        /// <summary>"dark", "light", or "system" (follow Windows; uses <see cref="LightColors"/> in light mode).</summary>
        public string Base { get; set; } = "dark";

        public string FontFamily { get; set; } = "Segoe UI Variable Text, Segoe UI";
        public string HeaderFontFamily { get; set; } = "Segoe UI Variable Display, Segoe UI";
        public string MonoFontFamily { get; set; } = "Cascadia Mono, Consolas";

        public double CornerRadius { get; set; } = 18;
        public double TileCornerRadius { get; set; } = 12;

        public ThemeColors Colors { get; set; } = new();
        public ThemeColors? LightColors { get; set; }
        public ThemeShadow Shadow { get; set; } = new();
        public ThemeEffects Effects { get; set; } = new();

        [JsonIgnore]
        public bool IsBuiltIn { get; set; }

        [JsonIgnore]
        public string? FilePath { get; set; }
    }

    public class ThemeColors
    {
        /// <summary>Pouch background. Solid colour or gradient.</summary>
        public string Background { get; set; } = "linear(160deg, #F01C1C24 0%, #F00B0B0F 100%)";

        /// <summary>Pouch outline. Solid colour or gradient.</summary>
        public string Border { get; set; } = "linear(180deg, #40FFFFFF 0%, #0FFFFFFF 100%)";

        public string Accent { get; set; } = "#8B7CFF";
        /// <summary>Text drawn on top of the accent colour.</summary>
        public string AccentText { get; set; } = "#FFFFFF";

        public string Text { get; set; } = "#F5F5F7";
        public string TextSecondary { get; set; } = "#B3FFFFFF";
        public string TextMuted { get; set; } = "#66FFFFFF";

        public string Tile { get; set; } = "#12FFFFFF";
        public string TileHover { get; set; } = "#22FFFFFF";
        public string TileBorder { get; set; } = "#14FFFFFF";

        /// <summary>Opaque surface for stacked preview cards.</summary>
        public string Card { get; set; } = "#FF2A2A33";

        public string Badge { get; set; } = "#B3000000";
        public string BadgeText { get; set; } = "#FFFFFF";

        public string Danger { get; set; } = "#FF5A52";
        public string Warning { get; set; } = "#FFB27A";
        public string Scrollbar { get; set; } = "#44FFFFFF";
    }

    public class ThemeShadow
    {
        public string Color { get; set; } = "#000000";
        public double Blur { get; set; } = 36;
        public double Depth { get; set; } = 8;
        public double Opacity { get; set; } = 0.6;
    }

    public class ThemeEffects
    {
        /// <summary>CRT-style horizontal lines over the pouch.</summary>
        public bool Scanlines { get; set; }
        public double ScanlineOpacity { get; set; } = 0.08;
    }
}
