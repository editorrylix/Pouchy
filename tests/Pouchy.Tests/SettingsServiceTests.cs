using System.IO;
using System.Windows.Input;
using Pouchy.Models;
using Pouchy.Services;
using Xunit;

namespace Pouchy.Tests
{
    public class SettingsServiceTests : IDisposable
    {
        private readonly TestFolder _folder = new();
        private string SettingsFile => Path.Combine(_folder.Path, "settings.json");

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void FirstRun_CreatesDefaults()
        {
            var service = new SettingsService(SettingsFile);

            Assert.True(File.Exists(SettingsFile));
            Assert.True(service.Current.ShakeEnabled);
            Assert.False(service.Current.ModifierDragEnabled);
            Assert.Equal(DragDetectionMode.Balanced, service.Current.DragDetection);
            Assert.Contains("photoshop.exe", service.Current.Blacklist);
        }

        [Fact]
        public void Update_ThenFlush_RoundTrips()
        {
            var service = new SettingsService(SettingsFile);
            service.Update(s =>
            {
                s.ShakeMinDistance = 45;
                s.DragDetection = DragDetectionMode.Strict;
                s.Hotkey = new HotkeySetting { Modifiers = ModifierKeys.Control | ModifierKeys.Windows, Key = Key.P };
                s.Blacklist = new List<string> { "game.exe" };
            });
            service.Flush();

            var reloaded = new SettingsService(SettingsFile).Current;

            Assert.Equal(45, reloaded.ShakeMinDistance);
            Assert.Equal(DragDetectionMode.Strict, reloaded.DragDetection);
            Assert.Equal(ModifierKeys.Control | ModifierKeys.Windows, reloaded.Hotkey.Modifiers);
            Assert.Equal(Key.P, reloaded.Hotkey.Key);
            Assert.Equal(new[] { "game.exe" }, reloaded.Blacklist);
        }

        [Fact]
        public void Update_RaisesChanged()
        {
            var service = new SettingsService(SettingsFile);
            int raised = 0;
            service.Changed += (_, _) => raised++;

            service.Update(s => s.ShakeEnabled = false);

            Assert.Equal(1, raised);
        }

        [Fact]
        public void LegacyBlacklist_IsMigrated()
        {
            string legacy = _folder.File("blacklist.json", """["mygame.exe"]""");

            var service = new SettingsService(SettingsFile, legacy);

            Assert.Equal(new[] { "mygame.exe" }, service.Current.Blacklist);
        }

        [Fact]
        public void CorruptFile_FallsBackToDefaults()
        {
            File.WriteAllText(SettingsFile, "{ broken");

            var service = new SettingsService(SettingsFile);

            Assert.True(service.Current.ShakeEnabled);
            Assert.True(File.Exists(SettingsFile + ".corrupt"));
        }

        [Fact]
        public void OutOfRangeValues_AreClamped()
        {
            File.WriteAllText(SettingsFile, """{ "ShakeMinDistance": 0, "ShakeReversals": 99 }""");

            var s = new SettingsService(SettingsFile).Current;

            Assert.Equal(5, s.ShakeMinDistance);
            Assert.Equal(10, s.ShakeReversals);
        }

        [Fact]
        public void HotkeySetting_ToString_IsReadable()
        {
            var hotkey = new HotkeySetting { Modifiers = ModifierKeys.Alt | ModifierKeys.Shift, Key = Key.Z };
            Assert.Equal("Alt + Shift + Z", hotkey.ToString());
        }
    
        [Fact]
        public void IsFirstRun_OnlyWithoutASettingsFile()
        {
            using var folder = new TestFolder();
            string file = System.IO.Path.Combine(folder.Path, "settings.json");

            Assert.True(new SettingsService(file).IsFirstRun);
            Assert.False(new SettingsService(file).IsFirstRun); // The first run wrote the file.
        }

        [Fact]
        public void NullListsFromOldFiles_AreRepaired()
        {
            using var folder = new TestFolder();
            string file = System.IO.Path.Combine(folder.Path, "settings.json");
            System.IO.File.WriteAllText(file, "{ \"ScreenshotHotkey\": null, \"RecentDestinations\": null, \"HiddenActions\": null, \"SoundVolume\": 7 }");

            var s = new SettingsService(file).Current;

            Assert.NotNull(s.ScreenshotHotkey);
            Assert.Empty(s.RecentDestinations);
            Assert.Empty(s.HiddenActions);
            Assert.Equal(1, s.SoundVolume);
        }
    }
}
