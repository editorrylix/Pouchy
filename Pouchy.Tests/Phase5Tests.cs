using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media;
using Pouchy.Helpers;
using Pouchy.Models;
using Pouchy.Services;
using Pouchy.Services.Actions;
using Pouchy.ViewModels;
using Xunit;

namespace Pouchy.Tests
{
    /// <summary>Smart shelves, clipboard history, auto-clear, recent destinations and the command palette's matching.</summary>
    public class SmartShelfTests : IDisposable
    {
        private sealed class NoIcons : IThumbnailProvider
        {
            public Thumbnail? GetThumbnail(string path) => null;
            public ImageSource? GetStackIcon() => null;
        }

        private readonly TestFolder _folder = new();
        private readonly SettingsService _settings;
        private readonly PersistenceService _persistence;
        private readonly ItemFactory _factory = new(new NoIcons());
        private readonly PouchViewModel _vm;

        public SmartShelfTests()
        {
            _settings = new SettingsService(Path.Combine(_folder.Path, "settings.json"));
            _persistence = new PersistenceService(Path.Combine(_folder.Path, "state"));
            _vm = new PouchViewModel(_factory, _persistence, _settings);
        }

        public void Dispose() => _folder.Dispose();

        [Theory]
        [InlineData(ShelfRule.Images, "shot.PNG", true)]
        [InlineData(ShelfRule.Images, "notes.pdf", false)]
        [InlineData(ShelfRule.Documents, "notes.pdf", true)]
        [InlineData(ShelfRule.Videos, "clip.mp4", true)]
        [InlineData(ShelfRule.Audio, "song.flac", true)]
        [InlineData(ShelfRule.Archives, "backup.7z", true)]
        [InlineData(ShelfRule.Archives, "backup.txt", false)]
        public void Rules_MatchFilesByExtension(ShelfRule rule, string name, bool expected)
        {
            var item = _factory.CreateFromPath(_folder.File(name));
            Assert.Equal(expected, ShelfRules.Matches(rule, item));
        }

        [Fact]
        public void Rules_MatchStacksOnlyWhenEveryFileMatches()
        {
            var images = _factory.CreateStack(new[] { _folder.File("a.png"), _folder.File("b.jpg") });
            var mixed = _factory.CreateStack(new[] { _folder.File("c.png"), _folder.File("d.txt") });

            Assert.True(ShelfRules.Matches(ShelfRule.Images, images));
            Assert.False(ShelfRules.Matches(ShelfRule.Images, mixed));
        }

        [Fact]
        public void Rules_ClipboardNeverMatchesNewItems()
        {
            Assert.False(ShelfRules.Matches(ShelfRule.Clipboard, _factory.CreateText("hello")));
        }

        [Fact]
        public void NewItems_GoToTheSmartShelfThatCollectsThem()
        {
            var main = _vm.ActiveShelf;
            var links = _vm.NewShelf("Links");
            _vm.SetShelfRule(links, ShelfRule.Links);
            _vm.ActivateShelf(main);

            var link = _vm.AddText("https://example.com/page")!;
            var note = _vm.AddText("just a note")!;

            Assert.Contains(link, links.Items);
            Assert.Contains(note, main.Items);
            Assert.Equal("Added to \u201cLinks\u201d", _vm.Notice);
        }

        [Fact]
        public async Task DroppingOnATab_IgnoresSmartShelves()
        {
            var main = _vm.ActiveShelf;
            var images = _vm.NewShelf("Images");
            _vm.SetShelfRule(images, ShelfRule.Images);

            await _vm.AddPathsAsync(new[] { _folder.File("photo.png") }, main);

            Assert.Single(main.Items);
            Assert.Empty(images.Items);
        }

        [Fact]
        public async Task AddPaths_SkipsFilesAlreadyOnTheTargetShelf()
        {
            string file = _folder.File("same.txt");
            Assert.NotNull(await _vm.AddPathsAsync(new[] { file }));
            Assert.Null(await _vm.AddPathsAsync(new[] { file }));
            Assert.Single(_vm.Items);
        }

        [Fact]
        public void ClipboardHistory_CreatesOneClipboardShelf()
        {
            _vm.SetClipboardHistory(true);
            var shelf = _vm.ClipboardShelf;
            Assert.NotNull(shelf);
            Assert.Equal("Clipboard", shelf!.Name);

            var other = _vm.NewShelf("Other");
            _vm.SetShelfRule(other, ShelfRule.Clipboard);
            Assert.Equal(other, _vm.ClipboardShelf);
            Assert.Equal(ShelfRule.None, shelf.Rule); // Only one shelf keeps history.

            _vm.SetClipboardHistory(false);
            Assert.Null(_vm.ClipboardShelf);
        }

        [Fact]
        public async Task ShelfRule_IsSavedAndRestored()
        {
            var shelf = _vm.NewShelf("Pics");
            _vm.SetShelfRule(shelf, ShelfRule.Images);
            _vm.Flush();

            var reloaded = new PouchViewModel(_factory, _persistence, _settings);
            await reloaded.LoadAsync(clearUnpinned: false);

            Assert.Equal(ShelfRule.Images, reloaded.Shelves.Single(s => s.Name == "Pics").Rule);
        }

        [Fact]
        public void RemoveExpired_RemovesOldUnpinnedItemsOnly()
        {
            var now = new DateTime(2026, 10, 7, 12, 0, 0);
            var old = _factory.CreateText("old", Guid.NewGuid(), now.AddDays(-2));
            var oldPinned = _factory.CreateText("old pinned", Guid.NewGuid(), now.AddDays(-2));
            oldPinned.IsPinned = true;
            var fresh = _factory.CreateText("fresh", Guid.NewGuid(), now.AddHours(-1));
            foreach (var item in new[] { old, oldPinned, fresh }) _vm.Items.Add(item);

            Assert.Equal(0, _vm.RemoveExpired(now)); // Auto-clear is off by default.

            _settings.Update(s => s.AutoClear = ItemExpiry.OneDay);
            Assert.Equal(1, _vm.RemoveExpired(now));
            Assert.Equal(new[] { oldPinned, fresh }, _vm.Items);
            Assert.True(_vm.CanUndo);
        }

        [Fact]
        public void RecentDestinations_NewestFirstWithoutDuplicates()
        {
            string a = _folder.Folder("A");
            string b = _folder.Folder("B");
            string gone = Path.Combine(_folder.Path, "Gone");

            _vm.RecordDestination(a);
            _vm.RecordDestination(gone);
            _vm.RecordDestination(b);
            _vm.RecordDestination(a + "\\");

            Assert.Equal(new[] { a, b }, _vm.RecentDestinations); // "Gone" doesn't exist, so it isn't offered.
        }
    }

    public class FuzzyMatchTests
    {
        [Theory]
        [InlineData("new", "New shelf")]
        [InlineData("ns", "New shelf")]
        [InlineData("shelf new", "New shelf")]
        [InlineData("SCREEN", "Take a screenshot")]
        public void Matches(string query, string text) => Assert.True(FuzzyMatch.Score(query, text) > 0);

        [Theory]
        [InlineData("xyz", "New shelf")]
        [InlineData("new zebra", "New shelf")]
        public void DoesNotMatch(string query, string text) => Assert.Equal(0, FuzzyMatch.Score(query, text));

        [Fact]
        public void WordStartsRankAboveScatteredLetters()
        {
            Assert.True(FuzzyMatch.Score("ns", "New shelf") > FuzzyMatch.Score("ns", "Copy paths"));
            Assert.True(FuzzyMatch.Score("copy", "Copy paths") > FuzzyMatch.Score("copy", "Take a screenshot copy"));
        }
    }

    public class DropActionTests : IDisposable
    {
        private readonly TestFolder _folder = new();
        private readonly List<string> _recent = new();

        public void Dispose() => _folder.Dispose();

        private ActionRegistry Registry() => new(Path.Combine(_folder.Path, "actions"), () => _recent, f => _recent.Insert(0, f));

        [Fact]
        public void ImageActions_OnlyForImages()
        {
            string png = _folder.File("a.png");
            string txt = _folder.File("a.txt");
            var registry = Registry();

            var forImage = registry.For(new[] { png }, Array.Empty<string>()).Select(a => a.Id).ToList();
            var forText = registry.For(new[] { txt }, Array.Empty<string>()).Select(a => a.Id).ToList();

            Assert.Contains("jpg", forImage);
            Assert.DoesNotContain("png", forImage); // Already a PNG.
            Assert.Contains("ocr", forImage);
            Assert.DoesNotContain("jpg", forText);
            Assert.Contains("zip", forText);
        }

        [Fact]
        public void HiddenActions_AreLeftOut()
        {
            var ids = Registry().For(new[] { _folder.File("a.txt") }, new[] { "zip", "print" }).Select(a => a.Id).ToList();
            Assert.DoesNotContain("zip", ids);
            Assert.DoesNotContain("print", ids);
        }

        [Fact]
        public void RecentFolders_BecomeCopyTiles_ExceptTheSourceFolder()
        {
            string target = _folder.Folder("Target");
            _recent.Add(_folder.Path);
            _recent.Add(target);

            var copy = Registry().For(new[] { _folder.File("a.txt") }, Array.Empty<string>()).Where(a => a.Id.StartsWith("copyto:")).ToList();

            Assert.Single(copy);
            Assert.Equal("Target", copy[0].ShortName);
        }

        [Fact]
        public async Task CopyTile_CopiesAndRemembersTheFolder()
        {
            string target = _folder.Folder("Out");
            string file = _folder.File("doc.txt", "content");
            _recent.Add(target);

            var action = Registry().For(new[] { file }, Array.Empty<string>()).Single(a => a.Id.StartsWith("copyto:"));
            var result = await action.RunAsync(new ActionContext(new[] { file }, IntPtr.Zero));

            Assert.True(File.Exists(Path.Combine(target, "doc.txt")));
            Assert.Equal("Copied 1 item to Out", result.Message);
        }

        [Fact]
        public async Task Zip_ReturnsTheArchive()
        {
            string file = _folder.File("doc.txt");
            var zip = Registry().For(new[] { file }, Array.Empty<string>()).Single(a => a.Id == "zip");

            var result = await zip.RunAsync(new ActionContext(new[] { file }, IntPtr.Zero));

            Assert.Single(result.NewFiles);
            Assert.EndsWith("doc.zip", result.NewFiles[0]);
            Assert.True(File.Exists(result.NewFiles[0]));
        }

        [Fact]
        public void Script_ReadsItsHeader()
        {
            string folder = _folder.Folder("actions");
            File.WriteAllText(Path.Combine(folder, "upload.ps1"), "# Pouchy-Name: Upload to server\n# Pouchy-Extensions: png jpg\n# Pouchy-Icon: CloudArrowUp24\nWrite-Output hi\n");
            File.WriteAllText(Path.Combine(folder, "notes.txt"), "not a script");

            var scripts = ScriptAction.LoadAll(folder);

            var script = Assert.Single(scripts);
            Assert.Equal("Upload to server", script.Name);
            Assert.Equal("CloudArrowUp24", script.Icon);
            Assert.True(script.CanRun(new[] { "C:\\a.PNG" }));
            Assert.False(script.CanRun(new[] { "C:\\a.txt" }));
        }

        [Fact]
        public async Task Script_RunsWithPathsAndAddsPrintedFiles()
        {
            string folder = _folder.Folder("actions");
            string input = _folder.File("in put.txt", "x");
            // Copies the file next to itself and prints the new path.
            File.WriteAllText(Path.Combine(folder, "dup.cmd"), "@echo off\r\ncopy /y \"%~1\" \"%~dpn1 copy%~x1\" >nul\r\necho %~dpn1 copy%~x1\r\n");

            var script = ScriptAction.LoadAll(folder).Single();
            var result = await script.RunAsync(new ActionContext(new[] { input }, IntPtr.Zero));

            string expected = Path.Combine(_folder.Path, "in put copy.txt");
            Assert.Equal(new[] { expected }, result.NewFiles);
        }

        [Fact]
        public async Task Script_FailureReportsTheExitCode()
        {
            string folder = _folder.Folder("actions");
            File.WriteAllText(Path.Combine(folder, "fail.bat"), "@echo off\r\necho something broke 1>&2\r\nexit /b 3\r\n");

            var script = ScriptAction.LoadAll(folder).Single();
            var error = await Assert.ThrowsAsync<IOException>(() => script.RunAsync(new ActionContext(new[] { _folder.File("a.txt") }, IntPtr.Zero)));

            Assert.Contains("exit code 3", error.Message);
            Assert.Contains("something broke", error.Message);
        }

        [Fact]
        public void ScriptOutput_KeepsOnlyExistingAbsolutePaths()
        {
            string real = _folder.File("real.txt");
            var files = ScriptAction.OutputFiles($"Working...\r\n{real}\r\n\"{real}\"\r\nC:\\does\\not\\exist.txt\r\nrelative.txt\r\n");
            Assert.Equal(new[] { real }, files);
        }
    }

    public class UpdateInstallerTests : IDisposable
    {
        private readonly TestFolder _folder = new();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void ZipName_MatchesTheReleaseAssets()
        {
            Assert.Equal("Pouchy-1.2.0-win-x64.zip", UpdateInstaller.ZipName(new Version(1, 2, 0), Architecture.X64));
            Assert.Equal("Pouchy-1.2.0-win-arm64.zip", UpdateInstaller.ZipName(new Version(1, 2, 0), Architecture.Arm64));
        }

        [Fact]
        public void FindChecksum_ReadsTheSumsFile()
        {
            string sums = "\uFEFF7328b5bef3235ab92928e281e8170ff2bf93b93bd0a84ff56930eeb4c695b850  Pouchy-1.0.1-win-arm64.zip\r\n" +
                          "327aa9ff1e9c0c5621e3504f6c724a05ddc712710d6738bb5eb0f728aabf336d  Pouchy-1.0.1-win-x64.zip\r\n";

            Assert.Equal("327aa9ff1e9c0c5621e3504f6c724a05ddc712710d6738bb5eb0f728aabf336d", UpdateInstaller.FindChecksum(sums, "Pouchy-1.0.1-win-x64.zip"));
            Assert.Equal("7328b5bef3235ab92928e281e8170ff2bf93b93bd0a84ff56930eeb4c695b850", UpdateInstaller.FindChecksum(sums, "Pouchy-1.0.1-win-arm64.zip"));
            Assert.Null(UpdateInstaller.FindChecksum(sums, "Other.zip"));
        }

        [Fact]
        public void ParseAssets_ReadsNamesAndUrls()
        {
            using var json = JsonDocument.Parse("""{"assets":[{"name":"SHA256SUMS.txt","browser_download_url":"https://x/sums","size":10},{"name":"a.zip","browser_download_url":"https://x/a.zip","size":5}]}""");
            var assets = UpdateService.ParseAssets(json.RootElement);
            Assert.Equal(new[] { "SHA256SUMS.txt", "a.zip" }, assets.Select(a => a.Name));
            Assert.Equal(5, assets[1].Size);
        }

        [Fact]
        public async Task ExtractAndSwap_ReplacesTheExeAndKeepsTheOldOne()
        {
            string zip = Path.Combine(_folder.Path, "update.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(archive.CreateEntry("Pouchy.exe").Open());
                writer.Write("new version");
            }
            string current = _folder.File("Pouchy.exe", "old version");

            string extracted = UpdateInstaller.ExtractExe(zip, _folder.Folder("download"));
            UpdateInstaller.Swap(extracted, current);

            Assert.Equal("new version", File.ReadAllText(current));
            Assert.Equal("old version", File.ReadAllText(current + ".old"));
            Assert.Equal(64, (await UpdateInstaller.HashFileAsync(current)).Length);

            UpdateInstaller.CleanUpOldVersion(current);
            Assert.False(File.Exists(current + ".old"));
        }

        [Fact]
        public void Swap_PutsTheOldExeBackIfCopyingFails()
        {
            string current = _folder.File("Pouchy.exe", "old version");
            Assert.ThrowsAny<IOException>(() => UpdateInstaller.Swap(Path.Combine(_folder.Path, "missing.exe"), current));
            Assert.Equal("old version", File.ReadAllText(current));
        }

        [Fact]
        public void StartupOptions_ParseUpdateFlags()
        {
            var options = StartupOptions.Parse(new[] { "--wait-for", "1234", "--updated", "--cleanup", "--palette" });
            Assert.Equal(1234, options.WaitForProcess);
            Assert.True(options.Updated && options.Cleanup && options.ShowPalette);
        }
    }

    public class SoundTests
    {
        /// <summary>Developer tool: POUCHY_SOUND_DIR=folder writes every built-in sound as a WAV file to listen to.</summary>
        [Fact]
        public void ExportSounds()
        {
            string? dir = Environment.GetEnvironmentVariable("POUCHY_SOUND_DIR");
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            foreach (var pack in new[] { SoundPack.Soft, SoundPack.Bubbly, SoundPack.Clicky })
            {
                foreach (var sound in Enum.GetValues<SoundEvent>())
                {
                    File.WriteAllBytes(Path.Combine(dir, $"{pack}-{sound}.wav".ToLowerInvariant()), SoundSynth.Create(pack, sound, 1.0));
                }
            }
        }

        /// <summary>Plays every sound through the speakers. Only with POUCHY_PLAY_SOUNDS=1 (it makes noise).</summary>
        [Fact]
        public void BuiltInPacks_PlayOnThisPc()
        {
            if (Environment.GetEnvironmentVariable("POUCHY_PLAY_SOUNDS") != "1") return;
            using var folder = new TestFolder();
            using var sounds = new SoundService(new SettingsService(Path.Combine(folder.Path, "settings.json")), folder.Path);
            foreach (var pack in new[] { SoundPack.Soft, SoundPack.Bubbly, SoundPack.Clicky })
            {
                foreach (var sound in Enum.GetValues<SoundEvent>())
                {
                    Assert.True(sounds.Play(pack, sound, 0.4), $"{pack} {sound} didn't play");
                    Thread.Sleep(350);
                }
            }
            Assert.False(sounds.Play(SoundPack.Off, SoundEvent.Add, 1));
            Assert.False(sounds.Play(SoundPack.Custom, SoundEvent.Add, 1)); // No WAV files in the folder.
        }

        [Fact]
        public void Volume_ScalesLoudness()
        {
            static int Peak(byte[] wav)
            {
                int peak = 0;
                for (int i = 44; i + 1 < wav.Length; i += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(wav, i)));
                return peak;
            }
            int full = Peak(SoundSynth.Create(SoundPack.Soft, SoundEvent.Add, 1.0));
            int half = Peak(SoundSynth.Create(SoundPack.Soft, SoundEvent.Add, 0.5));
            Assert.InRange(full, 20000, 24000); // About 70% of full scale: loud enough, no clipping.
            Assert.InRange(half, full * 0.3, full * 0.4);
            // Every pack peaks at the same level, so switching packs doesn't jump in volume.
            Assert.InRange(Peak(SoundSynth.Create(SoundPack.Clicky, SoundEvent.Remove, 1.0)), full - 300, full + 300);
        }

        [Theory]
        [InlineData(SoundPack.Soft)]
        [InlineData(SoundPack.Bubbly)]
        [InlineData(SoundPack.Clicky)]
        public void BuiltInPacks_AreValidWavFiles(SoundPack pack)
        {
            foreach (var sound in Enum.GetValues<SoundEvent>())
            {
                byte[] wav = SoundSynth.Create(pack, sound, 0.6);
                Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
                Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
                Assert.Equal(wav.Length - 8, BitConverter.ToInt32(wav, 4));
                Assert.InRange(wav.Length, 2000, 44100 * 2); // Short: well under a second.
            }
        }
    }
}
