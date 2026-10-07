using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Pouchy.Models;
using Pouchy.Services;
using Pouchy.Services.Theming;

namespace Pouchy.ViewModels
{
    /// <summary>One editable colour in the theme editor.</summary>
    public partial class ThemeColorField : ObservableObject
    {
        private readonly Action _changed;

        public ThemeColorField(string label, string description, Color color, Action changed)
        {
            Label = label;
            Description = description;
            _color = color;
            _hex = ThemeEditorViewModel.ToHex(color);
            _changed = changed;
        }

        public string Label { get; }
        public string Description { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Brush))]
        private Color _color;

        /// <summary>#RRGGBB or #AARRGGBB, editable as text.</summary>
        [ObservableProperty]
        private string _hex;

        public Brush Brush
        {
            get
            {
                var brush = new SolidColorBrush(Color);
                brush.Freeze();
                return brush;
            }
        }

        private bool _syncing;

        partial void OnColorChanged(Color value)
        {
            if (_syncing) return;
            _syncing = true;
            Hex = ThemeEditorViewModel.ToHex(value);
            _syncing = false;
            _changed();
        }

        partial void OnHexChanged(string value)
        {
            if (_syncing) return;
            try
            {
                var parsed = BrushParser.ParseColor(value, Colors.Gray);
                _syncing = true;
                Color = parsed;
                _syncing = false;
                _changed();
            }
            catch (FormatException)
            {
                // Still typing; keep the last good colour.
            }
        }
    }

    /// <summary>
    /// Edits a custom theme file with colour pickers and sliders. Every change is written to the
    /// file and applied right away, so the pouch restyles while you edit.
    /// </summary>
    public partial class ThemeEditorViewModel : ObservableObject
    {
        private readonly ThemeService _themes;
        private readonly string _path;
        private readonly ThemeDefinition _theme;
        private readonly DispatcherTimer _saveTimer;
        private readonly bool _initialized;

        public static IReadOnlyList<string> BaseOptions { get; } = new[] { "Dark", "Light", "Follow Windows" };

        public IReadOnlyList<string> FontOptions { get; } =
            Fonts.SystemFontFamilies.Select(f => f.Source).Order(StringComparer.CurrentCultureIgnoreCase).ToList();

        public ThemeColorField Accent { get; }
        public ThemeColorField AccentText { get; }
        public ThemeColorField Text { get; }
        public ThemeColorField BackgroundTop { get; }
        public ThemeColorField BackgroundBottom { get; }
        public ThemeColorField Tile { get; }
        public ThemeColorField Outline { get; }
        public IReadOnlyList<ThemeColorField> ColorFields { get; }

        [ObservableProperty] private string _name;
        [ObservableProperty] private string _base;
        [ObservableProperty] private bool _useGradient;
        [ObservableProperty] private double _backgroundOpacity;
        [ObservableProperty] private double _cornerRadius;
        [ObservableProperty] private double _tileCornerRadius;
        [ObservableProperty] private double _shadowOpacity;
        [ObservableProperty] private string _fontFamily;
        [ObservableProperty] private string _headerFontFamily;
        [ObservableProperty] private bool _scanlines;

        public string FilePath => _path;

        public ThemeEditorViewModel(ThemeService themes, string path)
        {
            _themes = themes;
            _path = path;
            _theme = JsonSerializer.Deserialize<ThemeDefinition>(File.ReadAllText(path), SettingsService.JsonOptions)
                     ?? throw new InvalidDataException("The theme file is empty.");
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _saveTimer.Tick += (_, _) =>
            {
                _saveTimer.Stop();
                Save();
            };

            var accent = themes.AccentColor;
            var c = _theme.Colors;
            Color Parse(string spec, Color fallback)
            {
                try
                {
                    return BrushParser.ParseColor(spec, accent);
                }
                catch (FormatException)
                {
                    return fallback;
                }
            }

            var (top, bottom, isGradient) = ReadBackground(c.Background, accent);
            _name = _theme.Name;
            _base = _theme.Base.ToLowerInvariant() switch { "light" => "Light", "system" => "Follow Windows", _ => "Dark" };
            _useGradient = isGradient;
            _backgroundOpacity = Math.Round(top.A / 255.0, 2);
            _cornerRadius = _theme.CornerRadius;
            _tileCornerRadius = _theme.TileCornerRadius;
            _shadowOpacity = _theme.Shadow.Opacity;
            _fontFamily = FirstFont(_theme.FontFamily);
            _headerFontFamily = FirstFont(_theme.HeaderFontFamily);
            _scanlines = _theme.Effects.Scanlines;

            Accent = new ThemeColorField("Accent", "Buttons, the selection and highlights.", Parse(c.Accent, Colors.MediumPurple), QueueSave);
            AccentText = new ThemeColorField("Text on accent", "Labels drawn on top of the accent colour.", Parse(c.AccentText, Colors.White), QueueSave);
            Text = new ThemeColorField("Text", "Names and titles. Secondary text is a faded version.", Parse(c.Text, Colors.White), QueueSave);
            BackgroundTop = new ThemeColorField("Background", "The pouch itself (top colour of the gradient).", Opaque(top), QueueSave);
            BackgroundBottom = new ThemeColorField("Background, bottom", "Bottom colour of the gradient.", Opaque(bottom), QueueSave);
            Tile = new ThemeColorField("Tiles", "Behind each item. Usually a faint version of the text colour.", Parse(c.Tile, Color.FromArgb(0x12, 255, 255, 255)), QueueSave);
            Outline = new ThemeColorField("Outline", "The thin border around the pouch.", Opaque(OutlineColor(c.Border, accent)), QueueSave);
            ColorFields = new[] { Accent, AccentText, Text, BackgroundTop, BackgroundBottom, Tile, Outline };

            _initialized = true;
        }

        private static string FirstFont(string families) => families.Split(',')[0].Trim();

        private static Color Opaque(Color c) => Color.FromRgb(c.R, c.G, c.B);

        internal static (Color Top, Color Bottom, bool Gradient) ReadBackground(string spec, Color accent)
        {
            try
            {
                return BrushParser.ParseBrush(spec, accent) switch
                {
                    GradientBrush { GradientStops.Count: >= 2 } g => (g.GradientStops[0].Color, g.GradientStops[^1].Color, true),
                    SolidColorBrush s => (s.Color, s.Color, false),
                    _ => (Color.FromRgb(28, 28, 36), Color.FromRgb(11, 11, 15), true),
                };
            }
            catch (FormatException)
            {
                return (Color.FromRgb(28, 28, 36), Color.FromRgb(11, 11, 15), true);
            }
        }

        private static Color OutlineColor(string spec, Color accent)
        {
            var (top, _, _) = ReadBackground(spec, accent);
            return top;
        }

        public static string ToHex(Color c) => c.A == 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

        private static string WithAlpha(Color c, byte alpha) => $"#{alpha:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

        partial void OnNameChanged(string value) => QueueSave();
        partial void OnBaseChanged(string value) => QueueSave();
        partial void OnUseGradientChanged(bool value) => QueueSave();
        partial void OnBackgroundOpacityChanged(double value) => QueueSave();
        partial void OnCornerRadiusChanged(double value) => QueueSave();
        partial void OnTileCornerRadiusChanged(double value) => QueueSave();
        partial void OnShadowOpacityChanged(double value) => QueueSave();
        partial void OnFontFamilyChanged(string value) => QueueSave();
        partial void OnHeaderFontFamilyChanged(string value) => QueueSave();
        partial void OnScanlinesChanged(bool value) => QueueSave();

        private void QueueSave()
        {
            if (!_initialized) return;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        /// <summary>Writes the theme now (also called when the editor closes).</summary>
        public void Save()
        {
            _saveTimer.Stop();
            Apply(_theme);
            try
            {
                File.WriteAllText(_path, JsonSerializer.Serialize(_theme, SettingsService.JsonOptions));
                _themes.Reload();
                _themes.Apply();
            }
            catch (IOException ex)
            {
                Logger.Log("Could not save the theme: " + ex.Message);
            }
        }

        /// <summary>Copies the editor's values into a theme definition.</summary>
        internal void Apply(ThemeDefinition theme)
        {
            theme.Name = string.IsNullOrWhiteSpace(Name) ? "My theme" : Name.Trim();
            theme.Base = Base switch { "Light" => "light", "Follow Windows" => "system", _ => "dark" };
            theme.CornerRadius = Math.Round(CornerRadius);
            theme.TileCornerRadius = Math.Round(TileCornerRadius);
            theme.Shadow.Opacity = Math.Round(ShadowOpacity, 2);
            theme.FontFamily = $"{FontFamily}, Segoe UI";
            theme.HeaderFontFamily = $"{HeaderFontFamily}, Segoe UI";
            theme.Effects.Scanlines = Scanlines;

            var c = theme.Colors;
            byte alpha = (byte)Math.Round(Math.Clamp(BackgroundOpacity, 0.3, 1) * 255);
            c.Background = UseGradient
                ? $"linear(160deg, {WithAlpha(BackgroundTop.Color, alpha)} 0%, {WithAlpha(BackgroundBottom.Color, alpha)} 100%)"
                : WithAlpha(BackgroundTop.Color, alpha);
            c.Accent = ToHex(Accent.Color);
            c.AccentText = ToHex(AccentText.Color);
            c.Text = ToHex(Text.Color);
            c.TextSecondary = WithAlpha(Text.Color, 0xB3);
            c.TextMuted = WithAlpha(Text.Color, 0x70);
            c.Tile = ToHex(Tile.Color);
            c.TileHover = WithAlpha(Tile.Color, (byte)Math.Clamp(Tile.Color.A * 2 + 8, 0x18, 0xFF));
            c.TileBorder = WithAlpha(Text.Color, 0x14);
            c.Card = ToHex(Blend(BackgroundTop.Color, Text.Color, 0.08));
            c.Scrollbar = WithAlpha(Text.Color, 0x44);
            c.Border = $"linear(180deg, {WithAlpha(Outline.Color, 0x50)} 0%, {WithAlpha(Outline.Color, 0x14)} 100%)";
        }

        private static Color Blend(Color a, Color b, double amount) => Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * amount),
            (byte)(a.G + (b.G - a.G) * amount),
            (byte)(a.B + (b.B - a.B) * amount));
    }
}
