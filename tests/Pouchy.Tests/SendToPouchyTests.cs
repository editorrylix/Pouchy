using System.IO;
using Microsoft.Win32;
using Pouchy.Helpers;
using Pouchy.Services;
using Xunit;

namespace Pouchy.Tests
{
    public class SendToPouchyTests
    {
        [Fact]
        public void Parse_TakesEverythingAfterAddAsPaths()
        {
            var options = StartupOptions.Parse(new[] { "--profile", "dev", "--add", @"C:\does not exist\a.txt", "--show" });

            Assert.Equal("dev", options.Profile);
            Assert.False(options.ShowPouch); // "--show" after --add is a path, as Explorer may pass odd file names.
            Assert.Equal(new[] { @"C:\does not exist\a.txt", "--show" }, options.Paths);
        }

        [Fact]
        public void Parse_AcceptsBarePathsOnlyWhenTheyExist()
        {
            using var folder = new TestFolder();
            string file = folder.File("dropped.txt");

            var options = StartupOptions.Parse(new[] { file, @"C:\missing\nope.txt", "--unknown", "--show" });

            Assert.True(options.ShowPouch);
            Assert.Equal(new[] { file }, options.Paths);
        }

        [Fact]
        public void Parse_NoArgumentsMeansNoPaths()
        {
            Assert.Empty(StartupOptions.Parse(Array.Empty<string>()).Paths);
        }

        [Fact]
        public async Task Channel_DeliversPathsToTheRunningCopy()
        {
            string profile = "test" + Guid.NewGuid().ToString("N")[..8];
            using var channel = new InstanceChannel(profile);
            var received = new TaskCompletionSource<IReadOnlyList<string>>();
            channel.Received += (_, paths) => received.TrySetResult(paths);
            channel.Start();

            var paths = new[] { @"C:\a b\one.txt", @"D:\ünïcode\two" };
            Assert.True(await Task.Run(() => InstanceChannel.Send(profile, paths)));

            var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(paths, result);
        }

        [Fact]
        public async Task Channel_EmptyMessageMeansShow()
        {
            string profile = "test" + Guid.NewGuid().ToString("N")[..8];
            using var channel = new InstanceChannel(profile);
            var received = new TaskCompletionSource<IReadOnlyList<string>>();
            channel.Received += (_, paths) => received.TrySetResult(paths);
            channel.Start();

            Assert.True(await Task.Run(() => InstanceChannel.Send(profile, Array.Empty<string>())));
            Assert.Empty(await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void Explorer_EnableWritesMenuAndShortcut_DisableRemovesThem()
        {
            using var folder = new TestFolder();
            string root = @"Software\PouchyTests-" + Guid.NewGuid().ToString("N");
            string exe = Path.Combine(folder.Path, "Pouchy.exe");
            var explorer = new ExplorerIntegrationService("dev", root, folder.Path, exe);
            try
            {
                Assert.False(explorer.IsEnabled);

                explorer.SetEnabled(true);
                Assert.True(explorer.IsEnabled);
                foreach (string target in new[] { "*", "Directory" })
                {
                    using var verb = Registry.CurrentUser.OpenSubKey($@"{root}\{target}\shell\Pouchy.dev");
                    Assert.Equal("Add to Pouchy (dev)", verb!.GetValue(""));
                    using var command = verb.OpenSubKey("command");
                    Assert.Equal($"\"{exe}\" --profile dev --add \"%1\"", command!.GetValue(""));
                }
                Assert.True(File.Exists(Path.Combine(folder.Path, "Pouchy (dev).lnk")));

                explorer.SetEnabled(false);
                Assert.False(explorer.IsEnabled);
                Assert.Null(Registry.CurrentUser.OpenSubKey($@"{root}\Directory\shell\Pouchy.dev"));
                Assert.False(File.Exists(Path.Combine(folder.Path, "Pouchy (dev).lnk")));
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKeyTree(root, throwOnMissingSubKey: false);
            }
        }

        [Fact]
        public void Explorer_RefreshRepointsAMovedExe()
        {
            using var folder = new TestFolder();
            string root = @"Software\PouchyTests-" + Guid.NewGuid().ToString("N");
            try
            {
                new ExplorerIntegrationService(null, root, folder.Path, @"C:\old\Pouchy.exe").SetEnabled(true);
                var moved = new ExplorerIntegrationService(null, root, folder.Path, @"C:\new\Pouchy.exe");

                moved.RefreshPathIfEnabled();

                using var command = Registry.CurrentUser.OpenSubKey($@"{root}\*\shell\Pouchy\command");
                Assert.Equal(@"""C:\new\Pouchy.exe"" --add ""%1""", command!.GetValue(""));
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKeyTree(root, throwOnMissingSubKey: false);
            }
        }
    }
}
