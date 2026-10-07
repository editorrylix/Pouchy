using System.IO;
using System.Windows;
using System.Windows.Media;
using Pouchy.Models;
using Pouchy.Services;
using Pouchy.Services.Theming;
using Xunit;

namespace Pouchy.Tests
{
    public class BrushParserTests
    {
        private static readonly Color Accent = Color.FromRgb(1, 2, 3);

        [Theory]
        [InlineData("#FF0000", 255, 255, 0, 0)]
        [InlineData("#80FFFFFF", 128, 255, 255, 255)]
        [InlineData("White", 255, 255, 255, 255)]
        public void ParseColor_Formats(string spec, byte a, byte r, byte g, byte b)
        {
            Assert.Equal(Color.FromArgb(a, r, g, b), BrushParser.ParseColor(spec, Accent));
        }

        [Fact]
        public void ParseColor_System_IsAccent()
        {
            Assert.Equal(Accent, BrushParser.ParseColor(" System ", Accent));
        }

        [Theory]
        [InlineData("not-a-colour")]
        [InlineData("#12")]
        [InlineData("")]
        public void ParseColor_Invalid_Throws(string spec)
        {
            Assert.Throws<FormatException>(() => BrushParser.ParseColor(spec, Accent));
        }

        [Fact]
        public void ParseBrush_Solid_IsFrozen()
        {
            var brush = Assert.IsType<SolidColorBrush>(BrushParser.ParseBrush("#00FF00", Accent));
            Assert.True(brush.IsFrozen);
            Assert.Equal(Colors.Lime, brush.Color);
        }

        [Fact]
        public void ParseBrush_Gradient_ParsesAngleAndStops()
        {
            var brush = Assert.IsType<LinearGradientBrush>(BrushParser.ParseBrush("linear(90deg, #000000 0%, system 25%, #FFFFFF 100%)", Accent));

            Assert.Equal(3, brush.GradientStops.Count);
            Assert.Equal(0.25, brush.GradientStops[1].Offset, 3);
            Assert.Equal(Accent, brush.GradientStops[1].Color);
            // 90deg runs left to right.
            Assert.Equal(0, brush.StartPoint.X, 3);
            Assert.Equal(1, brush.EndPoint.X, 3);
            Assert.Equal(0.5, brush.StartPoint.Y, 3);
        }

        [Fact]
        public void ParseBrush_GradientWithoutPositions_SpacesStopsEvenly()
        {
            var brush = (LinearGradientBrush)BrushParser.ParseBrush("linear(180, red, green, blue)", Accent);
            Assert.Equal(new[] { 0.0, 0.5, 1.0 }, brush.GradientStops.Select(s => Math.Round(s.Offset, 3)));
        }

        [Theory]
        [InlineData("linear(45deg, #000)")]
        [InlineData("linear(sideways, #000, #fff)")]
        [InlineData("linear(45deg, #000 x%, #fff)")]
        public void ParseBrush_InvalidGradient_Throws(string spec)
        {
            Assert.Throws<FormatException>(() => BrushParser.ParseBrush(spec, Accent));
        }
    }

    public class ThemeResourceBuilderTests
    {
        private static readonly ThemeContext Dark = new(Colors.Orange, SystemIsLight: false, BackgroundOpacity: 1);

        [Fact]
        public void AllBuiltInThemes_Build()
        {
            foreach (var theme in BuiltInThemes.All())
            {
                var resources = ThemeResourceBuilder.Build(theme, Dark);
                Assert.True(resources.Count > 20, theme.Id);
                Assert.True(ThemeResourceBuilder.Build(theme, Dark with { SystemIsLight = true }).Count > 20);
            }
        }

        [Fact]
        public void BuiltInThemeIds_AreUnique()
        {
            var ids = BuiltInThemes.All().Select(t => t.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void FollowWindows_UsesSystemAccentAndLightVariant()
        {
            var system = BuiltInThemes.All().Single(t => t.Id == "system");

            var dark = ThemeResourceBuilder.Build(system, Dark);
            var light = ThemeResourceBuilder.Build(system, Dark with { SystemIsLight = true });

            Assert.Equal(Colors.Orange, (Color)dark[ThemeKeys.AccentColor]);
            Assert.False(ThemeResourceBuilder.IsLight(system, Dark));
            Assert.True(ThemeResourceBuilder.IsLight(system, Dark with { SystemIsLight = true }));
            Assert.NotEqual(((SolidColorBrush)dark[ThemeKeys.Text]).Color, ((SolidColorBrush)light[ThemeKeys.Text]).Color);
        }

        [Fact]
        public void BackgroundOpacity_IsApplied()
        {
            var theme = BuiltInThemes.All()[0];
            var brush = (Brush)ThemeResourceBuilder.Build(theme, Dark with { BackgroundOpacity = 0.5 })[ThemeKeys.Background];
            Assert.Equal(0.5, brush.Opacity, 3);
        }

        [Fact]
        public void ScanlinesOff_GivesZeroOpacity()
        {
            var midnight = BuiltInThemes.All().Single(t => t.Id == "midnight");
            var terminal = BuiltInThemes.All().Single(t => t.Id == "terminal");
            Assert.Equal(0.0, ThemeResourceBuilder.Build(midnight, Dark)[ThemeKeys.ScanlineOpacity]);
            Assert.True((double)ThemeResourceBuilder.Build(terminal, Dark)[ThemeKeys.ScanlineOpacity] > 0);
        }
    }

    public class ThemeServiceTests : IDisposable
    {
        private static readonly ThemeContext Context = new(Colors.Orange, false, 1);
        private readonly TestFolder _folder = new();
        private readonly SettingsService _settings;
        private readonly ResourceDictionary _resources = new();
        private string ThemesFolder => Path.Combine(_folder.Path, "themes");

        public ThemeServiceTests()
        {
            _settings = new SettingsService(Path.Combine(_folder.Path, "settings.json"));
            Directory.CreateDirectory(ThemesFolder);
        }

        public void Dispose() => _folder.Dispose();

        private ThemeService CreateService() => new(_settings, _resources, ThemesFolder, contextOverride: () => Context);

        private void WriteTheme(string fileName, string json) => File.WriteAllText(Path.Combine(ThemesFolder, fileName), json);

        [Fact]
        public void PartialUserTheme_FillsInDefaults()
        {
            WriteTheme("neon.json", """{ "name": "Neon", "colors": { "accent": "#FF00FF" } }""");
            using var service = CreateService();
            service.Reload();

            var neon = service.Find("neon");
            Assert.NotNull(neon);
            Assert.Equal("Neon", neon!.Name);
            Assert.False(neon.IsBuiltIn);
            Assert.Equal("#FF00FF", neon.Colors.Accent);
            Assert.Equal(new ThemeColors().Text, neon.Colors.Text);
        }

        [Fact]
        public void InvalidThemes_AreSkipped()
        {
            WriteTheme("broken.json", "{ nope");
            WriteTheme("badcolour.json", """{ "colors": { "accent": "purple-ish" } }""");
            using var service = CreateService();
            service.Reload();

            Assert.Null(service.Find("broken"));
            Assert.Null(service.Find("badcolour"));
            Assert.Equal(BuiltInThemes.All().Count, service.Themes.Count);
        }

        [Fact]
        public void UserThemeWithBuiltInId_IsRenamed()
        {
            WriteTheme("mine.json", """{ "id": "midnight", "name": "My Midnight" }""");
            using var service = CreateService();
            service.Reload();

            Assert.Equal("Midnight Glass", service.Find("midnight")!.Name);
            Assert.Equal("My Midnight", service.Find("custom-mine")!.Name);
        }

        [Fact]
        public void Apply_WritesResources_AndFollowsSettings()
        {
            using var service = CreateService();
            service.Start();
            Assert.Equal("midnight", service.Current.Id);
            Assert.True(_resources.Contains(ThemeKeys.Background));

            _settings.Update(s => s.ThemeId = "paper");

            Assert.Equal("paper", service.Current.Id);
            Assert.True(service.IsLight);
            Assert.Equal(new CornerRadius(14), _resources[ThemeKeys.CornerRadius]);
        }

        [Fact]
        public void UnknownThemeId_FallsBackToDefault()
        {
            _settings.Update(s => s.ThemeId = "does-not-exist");
            using var service = CreateService();
            service.Start();

            Assert.Equal(BuiltInThemes.DefaultId, service.Current.Id);
        }

        [Fact]
        public void CreateCustomCopy_WritesEditableTheme()
        {
            using var service = CreateService();
            service.Start();

            string path = service.CreateCustomCopy(service.Find("terminal")!);

            Assert.True(File.Exists(path));
            var copy = service.Find(Path.GetFileNameWithoutExtension(path));
            Assert.NotNull(copy);
            Assert.Equal("Terminal (custom)", copy!.Name);
            Assert.True(copy.Effects.Scanlines);

            string second = service.CreateCustomCopy(service.Find("terminal")!);
            Assert.NotEqual(path, second);
        }
    }
}
