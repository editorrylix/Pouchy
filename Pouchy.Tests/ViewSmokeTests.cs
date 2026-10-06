using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Pouchy.Helpers;
using Pouchy.Models;
using Pouchy.Services;
using Pouchy.Services.Theming;
using Pouchy.ViewModels;
using Pouchy.Views;
using Xunit;

namespace Pouchy.Tests
{
    /// <summary>
    /// XAML problems (bad resource keys, unknown icon names, broken template bindings)
    /// only surface at runtime, so load every window with every theme.
    /// </summary>
    public class ViewSmokeTests
    {
        private sealed class NoIcons : IThumbnailProvider
        {
            public Thumbnail? GetThumbnail(string path) => null;
            public ImageSource? GetStackIcon() => null;
        }

        private static readonly ThemeContext FixedContext = new(Color.FromRgb(0, 120, 212), SystemIsLight: false, BackgroundOpacity: 1);

        [Fact]
        public void AllWindows_LoadWithoutXamlErrors()
        {
            RunOnSta(() =>
            {
                using var folder = new TestFolder();
                var app = CreateApp();
                var settings = new SettingsService(Path.Combine(folder.Path, "settings.json"));
                using var themes = new ThemeService(settings, app.Resources, Path.Combine(folder.Path, "themes"), contextOverride: () => FixedContext);
                themes.Start();

                var pouchVm = new PouchViewModel(new ItemFactory(new NoIcons()), new PersistenceService(folder.Path), settings);
                pouchVm.AddText("hello");
                var pouch = new PouchWindow(pouchVm);

                foreach (var theme in themes.Themes)
                {
                    settings.Update(s => s.ThemeId = theme.Id);
                    Assert.Equal(theme.Id, themes.Current.Id);
                    pouch.Measure(new Size(800, 800));
                }

                var settingsWindow = new SettingsWindow(new SettingsViewModel(settings, new StartupService(), new HotkeyService(), themes));
                var quickLook = new QuickLookWindow(new PouchItem { Kind = PouchItemKind.Text, TextContent = "preview" });

                settingsWindow.Close();
                quickLook.Close();
                pouch.AllowClose = true;
                pouch.Close();
            });
        }

        /// <summary>
        /// Developer tool, not a check: renders the pouch in every theme and view mode to PNGs.
        /// Set POUCHY_RENDER_DIR to a folder to enable it.
        /// </summary>
        [Fact]
        public void RenderPreviews()
        {
            string? outDir = Environment.GetEnvironmentVariable("POUCHY_RENDER_DIR");
            if (string.IsNullOrEmpty(outDir)) return;
            Directory.CreateDirectory(outDir);

            RunOnSta(() =>
            {
                using var folder = new TestFolder();
                var app = CreateApp();
                Motion.Enabled = false;
                var settings = new SettingsService(Path.Combine(folder.Path, "settings.json"));
                using var themes = new ThemeService(settings, app.Resources, Path.Combine(folder.Path, "themes"), contextOverride: () => FixedContext);
                themes.Start();

                using var thumbnails = new ShellThumbnailProvider();
                var factory = new ItemFactory(thumbnails);
                var vm = new PouchViewModel(factory, new PersistenceService(folder.Path), settings);
                var pouch = new PouchWindow(vm) { ShowActivated = false, Left = -20000, Top = -20000 };
                pouch.Show();
                ((FrameworkElement)pouch.FindName("MainContainer")).Opacity = 1;

                Render(pouch, Path.Combine(outDir, "empty.png"));

                foreach (var item in CreateSampleItems(folder, factory)) vm.Items.Add(item);
                vm.Items[0].Label = ColorLabel.Red;
                vm.Items[2].Label = ColorLabel.Blue;
                vm.NewShelf("Work");
                vm.NewShelf("Ideas");
                vm.ActivateShelf(vm.Shelves[0]);

                foreach (var theme in themes.Themes)
                {
                    foreach (var mode in Enum.GetValues<PouchViewMode>())
                    {
                        settings.Update(s =>
                        {
                            s.ThemeId = theme.Id;
                            s.ViewMode = mode;
                        });
                        Render(pouch, Path.Combine(outDir, $"{theme.Id}-{mode}.png".ToLowerInvariant()));
                    }
                }

                settings.Update(s => { s.ThemeId = "midnight"; s.ViewMode = PouchViewMode.Grid; });
                vm.IsSearchOpen = true;
                vm.SearchText = "notes";
                Render(pouch, Path.Combine(outDir, "search.png"));
                vm.IsSearchOpen = false;

                vm.RemoveItems(new[] { vm.Items[^1] });
                Render(pouch, Path.Combine(outDir, "undo.png"));
                vm.Undo();

                // Context menus, hosted in a plain window so they can be rendered.
                settings.Update(s => { s.ThemeId = "midnight"; s.ViewMode = PouchViewMode.Grid; });
                vm.ThemeListProvider = () => themes.Themes.Select(t => (t.Id, t.Name)).ToList();
                var buildItemMenu = typeof(PouchWindow).GetMethod("BuildItemMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                var buildBackgroundMenu = typeof(PouchWindow).GetMethod("BuildBackgroundMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                var newMenu = typeof(PouchWindow).GetMethod("NewMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                var menus = new (string Name, object Groups)[]
                {
                    ("menu-image", buildItemMenu.Invoke(pouch, new object[] { new List<PouchItem> { vm.Items[0] } })!),
                    ("menu-text", buildItemMenu.Invoke(pouch, new object[] { new List<PouchItem> { vm.Items[4] } })!),
                    ("menu-multi", buildItemMenu.Invoke(pouch, new object[] { vm.Items.Take(3).ToList() })!),
                    ("menu-background", buildBackgroundMenu.Invoke(pouch, null)!),
                    ("menu-tray", new List<List<object>>
                    {
                        new() { MenuFactory.Header("8 items on 3 shelves") },
                        new()
                        {
                            MenuFactory.Item("Show pouch", Wpf.Ui.Controls.SymbolRegular.PanelLeft24, () => { }, "Alt + Shift + Z"),
                            MenuFactory.Item("New note…", Wpf.Ui.Controls.SymbolRegular.NoteAdd24, () => { }),
                        },
                        new()
                        {
                            MenuFactory.Item("Pause gestures", Wpf.Ui.Controls.SymbolRegular.Pause24, () => { }),
                            MenuFactory.Item("Settings…", Wpf.Ui.Controls.SymbolRegular.Settings24, () => { }),
                        },
                        new() { MenuFactory.Item("Quit Pouchy", Wpf.Ui.Controls.SymbolRegular.Power24, () => { }) },
                    }),
                };
                foreach (var (name, groups) in menus)
                {
                    // A ContextMenu can't live in a window, so move its items into a vertical Menu
                    // wrapped in the same chrome as the PouchContextMenu template.
                    var contextMenu = (System.Windows.Controls.ContextMenu)newMenu.Invoke(pouch, new[] { groups })!;
                    var entries = contextMenu.Items.Cast<object>().ToList();
                    contextMenu.Items.Clear();
                    var panel = new System.Windows.Controls.Menu
                    {
                        Background = Brushes.Transparent,
                        Template = (System.Windows.Controls.ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                            "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Menu'>" +
                            "<StackPanel IsItemsHost='True'/></ControlTemplate>"),
                    };
                    foreach (var entry in entries) panel.Items.Add(entry);
                    var chrome = new System.Windows.Controls.Border
                    {
                        Margin = new Thickness(10), Padding = new Thickness(0, 4, 0, 4), MinWidth = 220,
                        CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Child = panel,
                    };
                    chrome.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "Pouch.MenuBackground");
                    chrome.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "Pouch.TileBorder");
                    var host = new Window
                    {
                        Content = chrome, SizeToContent = SizeToContent.WidthAndHeight, WindowStyle = WindowStyle.None,
                        AllowsTransparency = true, Background = Brushes.Transparent, ShowActivated = false, Left = -20000, Top = -20000,
                    };
                    host.Show();
                    Render(host, Path.Combine(outDir, name + ".png"));
                    host.Close();
                }

                // Mascot moods side by side.
                var moods = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                foreach (var mood in Enum.GetValues<MascotMood>())
                {
                    moods.Children.Add(new Mascot { Mood = mood, Width = 110, Height = 110, Animated = false, Margin = new Thickness(10) });
                }
                RenderElement(moods, Path.Combine(outDir, "mascot.png"));

                // The shelf icon picker grid.
                var shelfMenu = (List<List<object>>)typeof(PouchWindow)
                    .GetMethod("BuildShelfMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .Invoke(pouch, new object[] { vm.Shelves[1] })!;
                foreach (var picker in shelfMenu[0].OfType<System.Windows.Controls.MenuItem>().Where(m => m.Items.Count > 0))
                {
                    var tiles = picker.Items.Cast<object>().ToList();
                    picker.Items.Clear();
                    var grid = new System.Windows.Controls.Menu
                    {
                        Width = picker.Header as string == "Icon" ? 8 * 40 + 8 : 5 * 40 + 8,
                        Template = (System.Windows.Controls.ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                            "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Menu'>" +
                            "<WrapPanel IsItemsHost='True'/></ControlTemplate>"),
                    };
                    foreach (var tile in tiles) grid.Items.Add(tile);
                    var chrome = new System.Windows.Controls.Border { Padding = new Thickness(6), CornerRadius = new CornerRadius(10), Child = grid };
                    chrome.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "Pouch.MenuBackground");
                    RenderElement(chrome, Path.Combine(outDir, $"picker-{picker.Header}.png".ToLowerInvariant()));
                }

                var settingsWindow = new SettingsWindow(new SettingsViewModel(settings, new StartupService(), new HotkeyService(), themes))
                {
                    ShowActivated = false, Left = -20000, Top = -20000,
                };
                settingsWindow.Show();
                Render(settingsWindow, Path.Combine(outDir, "settings.png"));
                settingsWindow.Close();

                // Light pouch theme: the Fluent windows must switch to light too (text was invisible once).
                settings.Update(s => s.ThemeId = "paper");
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light, Wpf.Ui.Controls.WindowBackdropType.Mica, updateAccent: false);
                var lightSettings = new SettingsWindow(new SettingsViewModel(settings, new StartupService(), new HotkeyService(), themes))
                {
                    ShowActivated = false, Left = -20000, Top = -20000,
                };
                lightSettings.Show();
                Render(lightSettings, Path.Combine(outDir, "settings-light.png"));
                lightSettings.Close();
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark, Wpf.Ui.Controls.WindowBackdropType.Mica, updateAccent: false);

                pouch.AllowClose = true;
                pouch.Close();
                Motion.Enabled = true;
            });
        }

        /// <summary>POUCHY_RENDER_MARKETING=1: transparent backgrounds and only "happy path" items, for README images.</summary>
        private static bool Marketing => Environment.GetEnvironmentVariable("POUCHY_RENDER_MARKETING") == "1";

        private static IEnumerable<PouchItem> CreateSampleItems(TestFolder folder, ItemFactory factory)
        {
            string photo = Path.Combine(folder.Path, "Sunset photo.png");
            SaveSamplePng(photo, Colors.OrangeRed, Colors.MidnightBlue);
            string photo2 = Path.Combine(folder.Path, "Mountains.png");
            SaveSamplePng(photo2, Colors.SeaGreen, Colors.LightSkyBlue);
            string notes = folder.File("Meeting notes.txt", "Agenda\n- Ship Pouchy themes\n- Review grid layout\n- Plan phase 3");
            string docs = folder.Folder("Project files");
            folder.File(Path.Combine("Project files", "a.txt"));

            yield return factory.CreateFromPath(photo);
            yield return factory.CreateFromPath(notes);
            yield return factory.CreateFromPath(docs);
            yield return factory.CreateStack(new[] { photo2, photo, notes });
            var note = factory.CreateText("Remember to email the designs to the team before Friday.");
            note.IsPinned = true;
            yield return note;
            if (Marketing)
            {
                string beach = Path.Combine(folder.Path, "Beach day.png");
                SaveSamplePng(beach, Color.FromRgb(255, 196, 120), Color.FromRgb(40, 170, 220));
                yield return factory.CreateFromPath(beach);
            }
            else
            {
                yield return factory.CreateFromPath(Path.Combine(folder.Path, "Deleted report.pdf"));
            }
            yield return factory.CreateLink("https://github.com/editorrylix/Pouchy");
            yield return factory.CreateColor("#FF8A5B");
        }

        private static void SaveSamplePng(string path, Color from, Color to)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new LinearGradientBrush(to, from, 90), null, new Rect(0, 0, 320, 240));
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(235, 255, 236, 160)), null, new Point(220, 110), 34, 34);
                var far = Geometry.Parse("M0,170 L60,120 L110,150 L170,95 L240,145 L320,110 L320,240 L0,240 Z");
                var near = Geometry.Parse("M0,200 L70,160 L140,195 L210,150 L280,190 L320,175 L320,240 L0,240 Z");
                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(150, 30, 20, 60)), null, far);
                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(220, 20, 12, 40)), null, near);
            }
            var bitmap = new RenderTargetBitmap(320, 240, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }

        private static void RenderElement(FrameworkElement element, string path)
        {
            var host = new Window
            {
                Content = element, SizeToContent = SizeToContent.WidthAndHeight, WindowStyle = WindowStyle.None,
                AllowsTransparency = true, Background = Brushes.Transparent, ShowActivated = false, Left = -20000, Top = -20000,
            };
            host.Show();
            Render(host, path);
            host.Close();
        }

        private static void Render(Window window, string path)
        {
            window.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();

            // The whole window, including its transparent shadow margin, mapped 1:1.
            var root = (Visual)window;
            var bounds = VisualTreeHelper.GetDescendantBounds(root);
            bounds.Union(new Rect(0, 0, window.ActualWidth, window.ActualHeight));
            double width = Math.Ceiling(bounds.Width + 40);
            double height = Math.Ceiling(bounds.Height + 40);

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // A neutral desktop-like backdrop so translucent themes are visible (transparent for README images).
                if (!Marketing) dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(70, 90, 120), Color.FromRgb(150, 120, 160), 45), null, new Rect(0, 0, width, height));
                var brush = new VisualBrush(root)
                {
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top,
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = bounds,
                };
                dc.DrawRectangle(brush, null, new Rect(20, 20, bounds.Width, bounds.Height));
            }

            var bitmap = new RenderTargetBitmap((int)width * 2, (int)height * 2, 192, 192, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }

        // WPF allows one Application per process, bound to the thread that created it,
        // so every UI test runs on this one STA thread and shares the app.
        // A plain Application with the app's resources: creating the real App would run
        // its startup (mouse hook, tray icon, single-instance check) as soon as the
        // dispatcher pumps.
        private static readonly StaWorker UiThread = new("UI tests");
        private static Application? _app;

        private static Application CreateApp()
        {
            if (_app == null)
            {
                // Same layout as App.xaml: WPF-UI dictionaries at the top level, then ours.
                _app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                _app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark });
                _app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
                _app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Pouchy;component/Themes/AppResources.xaml"),
                });
            }
            return _app;
        }

        private static void RunOnSta(Action action)
        {
            var failure = UiThread.Invoke<Exception?>(() =>
            {
                try
                {
                    action();
                    return null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            });
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
