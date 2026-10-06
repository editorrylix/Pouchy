using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Pouchy.Services;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Pouchy.Views
{
    /// <summary>
    /// Builds themed menus (styles in Themes/AppResources.xaml) for the pouch and the tray icon.
    /// </summary>
    internal static class MenuFactory
    {
        /// <summary>Shows errors from menu actions; set by the app.</summary>
        public static Action<Exception>? ErrorHandler { get; set; }

        /// <summary>A context menu with separators between the non-empty groups.</summary>
        public static ContextMenu Create(IEnumerable<IEnumerable<object>> groups, UIElement? target)
        {
            var menu = new ContextMenu
            {
                Style = Style("PouchContextMenu"),
                PlacementTarget = target,
                Placement = PlacementMode.MousePoint,
            };
            Fill(menu, groups);
            return menu;
        }

        /// <summary>Replaces a menu's items with the groups, separated.</summary>
        public static void Fill(ItemsControl menu, IEnumerable<IEnumerable<object>> groups)
        {
            menu.Items.Clear();
            foreach (var group in groups.Select(g => g.ToList()).Where(g => g.Count > 0))
            {
                bool startsWithHeader = menu.Items.Count == 0;
                if (!startsWithHeader && menu.Items[^1] is not Separator { Tag: string }) menu.Items.Add(Separator());
                foreach (var entry in group) menu.Items.Add(entry);
            }
        }

        public static MenuItem Item(string header, SymbolRegular symbol, Action action, string? gesture = null, bool danger = false) =>
            Item(header, new SymbolIcon(symbol) { FontSize = 15 }, action, gesture, danger);

        public static MenuItem Item(string header, object? icon, Action action, string? gesture = null, bool danger = false)
        {
            var item = new MenuItem
            {
                Header = header,
                Icon = icon,
                InputGestureText = gesture ?? "",
                Style = Style(danger ? "PouchDangerMenuItem" : "PouchMenuItem"),
            };
            item.Click += (_, _) =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Logger.Log("Menu action failed: " + ex);
                    ErrorHandler?.Invoke(ex);
                }
            };
            return item;
        }

        /// <summary>A menu item with a check mark when <paramref name="isChecked"/>.</summary>
        public static MenuItem Check(string header, bool isChecked, Action action, object? icon = null)
        {
            var item = Item(header, icon ?? (isChecked ? new SymbolIcon(SymbolRegular.Checkmark24) { FontSize = 15 } : null), action);
            item.IsChecked = isChecked;
            return item;
        }

        public static MenuItem Submenu(string header, SymbolRegular symbol, IEnumerable<object> children)
        {
            var item = new MenuItem
            {
                Header = header,
                Icon = new SymbolIcon(symbol) { FontSize = 15 },
                Style = Style("PouchMenuItem"),
            };
            foreach (var child in children) item.Items.Add(child);
            return item;
        }

        public static Separator Separator() => new() { Style = Style("PouchMenuSeparator") };

        /// <summary>A non-clickable app header ("Pouchy" plus a status line) for the top of a menu.</summary>
        public static Separator Header(string subtitle) => new() { Style = Style("PouchMenuHeader"), Tag = subtitle };

        /// <summary>
        /// A submenu that lays its entries out as a grid of square tiles (icon and colour pickers).
        /// </summary>
        public static MenuItem Grid(string header, SymbolRegular symbol, IEnumerable<(object Content, string ToolTip, bool Checked, Action Action)> tiles, int columns)
        {
            var panel = new FrameworkElementFactory(typeof(WrapPanel));
            panel.SetValue(FrameworkElement.WidthProperty, columns * 40.0);
            panel.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0));

            var submenu = Submenu(header, symbol, Array.Empty<object>());
            submenu.ItemsPanel = new ItemsPanelTemplate(panel);
            foreach (var (content, toolTip, isChecked, action) in tiles)
            {
                var tile = new MenuItem { Header = content, ToolTip = toolTip, IsChecked = isChecked, Style = Style("PouchMenuTile") };
                tile.Click += (_, _) =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Menu action failed: " + ex);
                        ErrorHandler?.Invoke(ex);
                    }
                };
                submenu.Items.Add(tile);
            }
            return submenu;
        }

        /// <summary>A shelf's icon in its colour, as a menu icon.</summary>
        public static SymbolIcon ShelfIcon(Models.Shelf shelf, double size = 15) => new()
        {
            Symbol = Enum.TryParse<SymbolRegular>($"{shelf.Icon}24", out var symbol) ? symbol : SymbolRegular.Archive24,
            Filled = true,
            FontSize = size,
            Foreground = shelf.ColorBrush,
        };

        /// <summary>A small filled circle, used as the icon for colour choices.</summary>
        public static Ellipse Dot(Brush fill, bool ring = false) => new()
        {
            Width = 12,
            Height = 12,
            Fill = fill,
            Stroke = ring ? Brushes.White : null,
            StrokeThickness = ring ? 2 : 0,
        };

        private static Style Style(string key) => (Style)Application.Current.FindResource(key);
    }
}
