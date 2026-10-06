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

                var settingsWindow = new SettingsWindow(new SettingsViewModel(settings, new StartupService(), new HotkeyService(), themes))
                {
                    ShowActivated = false, Left = -20000, Top = -20000,
                };
                settingsWindow.Show();
                Render(settingsWindow, Path.Combine(outDir, "settings.png"));
                settingsWindow.Close();

                pouch.AllowClose = true;
                pouch.Close();
                Motion.Enabled = true;
            });
        }

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
            yield return factory.CreateText("Remember to email the designs to the team before Friday.");
            yield return factory.CreateFromPath(Path.Combine(folder.Path, "Deleted report.pdf"));
        }

        private static void SaveSamplePng(string path, Color from, Color to)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new LinearGradientBrush(from, to, 90), null, new Rect(0, 0, 320, 240));
                dc.DrawEllipse(Brushes.Gold, null, new Point(220, 90), 40, 40);
            }
            var bitmap = new RenderTargetBitmap(320, 240, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }

        private static void Render(Window window, string path)
        {
            window.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();

            var root = (FrameworkElement)window.Content;
            double width = Math.Ceiling(root.ActualWidth + 40);
            double height = Math.Ceiling(root.ActualHeight + 40);

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // A neutral desktop-like backdrop so translucent themes are visible.
                dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(70, 90, 120), Color.FromRgb(150, 120, 160), 45), null, new Rect(0, 0, width, height));
                dc.DrawRectangle(new VisualBrush(root), null, new Rect(20, 20, root.ActualWidth, root.ActualHeight));
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
                _app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
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
