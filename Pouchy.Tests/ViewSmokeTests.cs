using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows.Media;
using Pouchy.Models;
using Pouchy.Services;
using Pouchy.ViewModels;
using Pouchy.Views;
using Xunit;

namespace Pouchy.Tests
{
    /// <summary>
    /// XAML problems (bad resource keys, unknown icon names, broken bindings paths in
    /// templates) only surface at runtime, so load every window once.
    /// </summary>
    public class ViewSmokeTests
    {
        private sealed class NoIcons : IIconProvider
        {
            public ImageSource? GetFileIcon(string path) => null;
            public ImageSource? GetStackIcon() => null;
        }

        [Fact]
        public void AllWindows_LoadWithoutXamlErrors()
        {
            RunOnSta(() =>
            {
                using var folder = new TestFolder();
                var app = new App();
                app.InitializeComponent();

                var pouchVm = new PouchViewModel(new ItemFactory(new NoIcons()), new PersistenceService(folder.Path));
                pouchVm.AddText("hello");
                var pouch = new PouchWindow(pouchVm);
                pouch.Measure(new System.Windows.Size(400, 800));

                var settingsVm = new SettingsViewModel(
                    new SettingsService(Path.Combine(folder.Path, "settings.json")),
                    new StartupService(),
                    new HotkeyService());
                var settings = new SettingsWindow(settingsVm);

                var quickLook = new QuickLookWindow(new PouchItem { Kind = PouchItemKind.Text, TextContent = "preview" });

                settings.Close();
                quickLook.Close();
                pouch.AllowClose = true;
                pouch.Close();
                app.Shutdown();
            });
        }

        private static void RunOnSta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
