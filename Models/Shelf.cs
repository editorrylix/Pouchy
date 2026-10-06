using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Pouchy.Models
{
    /// <summary>A named collection of items. The pouch shows one shelf at a time, as tabs.</summary>
    public partial class Shelf : ObservableObject
    {
        public static readonly string[] Palette =
        {
            "#8B7CFF", "#3B82F6", "#14B8A6", "#22C55E", "#EAB308", "#F97316", "#EF4444", "#EC4899", "#94A3B8",
        };

        /// <summary>Icons a shelf can show on its tab (Fluent icon names, drawn filled in the shelf colour).</summary>
        public static readonly string[] Icons =
        {
            "Archive", "Heart", "Star", "Sparkle", "Briefcase", "Camera", "Image", "Video",
            "MusicNote2", "Document", "Book", "Bookmark", "Code", "Bug", "Lightbulb", "Rocket",
            "Gift", "Balloon", "Umbrella", "Trophy", "Diamond", "Fire", "Flash", "LeafOne",
            "WeatherSunny", "WeatherMoon", "Cloud", "Backpack", "Airplane", "Beach", "Home", "Cart",
            "AnimalCat", "AnimalDog", "AnimalRabbit", "AnimalTurtle", "DrinkCoffee", "FoodPizza", "FoodCake", "Games",
        };

        public Guid Id { get; init; } = Guid.NewGuid();

        public ObservableCollection<PouchItem> Items { get; } = new();

        /// <summary>One of <see cref="Icons"/>.</summary>
        [ObservableProperty]
        private string _icon = Icons[0];

        [ObservableProperty]
        private string _name = "Pouch";

        /// <summary>Hex colour of the shelf's dot.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ColorBrush))]
        private string _color = Palette[0];

        /// <summary>True for the shelf currently shown in the pouch. Not saved.</summary>
        [ObservableProperty]
        private bool _isActive;

        public Brush ColorBrush => LabelColors.BrushFromHex(Color);
    }

    /// <summary>Finder-style colour labels for items.</summary>
    public enum ColorLabel
    {
        None,
        Red,
        Orange,
        Yellow,
        Green,
        Blue,
        Purple,
        Gray,
    }

    public static class LabelColors
    {
        /// <summary>The label colour at low opacity, for tinting a tile's background.</summary>
        public static Brush? TintFor(ColorLabel label)
        {
            if (BrushFor(label) is not SolidColorBrush solid) return null;
            var tint = new SolidColorBrush(Color.FromArgb(0x3D, solid.Color.R, solid.Color.G, solid.Color.B));
            tint.Freeze();
            return tint;
        }

        private static readonly Dictionary<ColorLabel, Brush> Brushes = new()
        {
            [ColorLabel.Red] = BrushFromHex("#EF4444"),
            [ColorLabel.Orange] = BrushFromHex("#F97316"),
            [ColorLabel.Yellow] = BrushFromHex("#EAB308"),
            [ColorLabel.Green] = BrushFromHex("#22C55E"),
            [ColorLabel.Blue] = BrushFromHex("#3B82F6"),
            [ColorLabel.Purple] = BrushFromHex("#A855F7"),
            [ColorLabel.Gray] = BrushFromHex("#94A3B8"),
        };

        public static IReadOnlyList<ColorLabel> All { get; } = Enum.GetValues<ColorLabel>().Where(l => l != ColorLabel.None).ToList();

        public static Brush? BrushFor(ColorLabel label) => Brushes.TryGetValue(label, out var brush) ? brush : null;

        public static Brush BrushFromHex(string hex)
        {
            Color color;
            try
            {
                color = (Color)ColorConverter.ConvertFromString(hex);
            }
            catch (Exception)
            {
                color = Colors.Gray;
            }
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
