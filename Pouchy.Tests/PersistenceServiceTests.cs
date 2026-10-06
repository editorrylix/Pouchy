using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pouchy.Models;
using Pouchy.Services;
using Xunit;

namespace Pouchy.Tests
{
    public class PersistenceServiceTests : IDisposable
    {
        private readonly TestFolder _folder = new();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void SaveNow_ThenLoad_RoundTripsAllKinds()
        {
            var service = new PersistenceService(_folder.Path);
            var image = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8);
            image.Freeze();

            var items = new[]
            {
                new PouchItem { Kind = PouchItemKind.File, FilePath = @"C:\a.txt", DisplayName = "a" },
                new PouchItem { Kind = PouchItemKind.Stack, StackFiles = new[] { @"C:\a.txt", @"C:\b.txt" } },
                new PouchItem { Kind = PouchItemKind.Text, TextContent = "hello" },
                new PouchItem { Kind = PouchItemKind.Image, ImageContent = image },
            };

            service.SaveNow(items);
            var loaded = service.Load();

            Assert.Equal(4, loaded.Count);
            Assert.Equal(items.Select(i => i.Id), loaded.Select(l => l.Id));
            Assert.Equal(PouchItemKind.Stack, loaded[1].Kind);
            Assert.Equal(2, loaded[1].StackFiles!.Count);
            Assert.Equal("hello", loaded[2].TextContent);

            var imageFile = loaded[3].ImageFile;
            Assert.NotNull(imageFile);
            var reloaded = service.LoadImage(imageFile!);
            Assert.NotNull(reloaded);
            Assert.Equal(2, reloaded!.PixelWidth);
        }

        [Fact]
        public void RemovedImageItems_HaveTheirFilesPruned()
        {
            var service = new PersistenceService(_folder.Path);
            var image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
            image.Freeze();
            var item = new PouchItem { Kind = PouchItemKind.Image, ImageContent = image };

            service.SaveNow(new[] { item });
            string imagePath = Path.Combine(_folder.Path, "images", $"{item.Id}.png");
            Assert.True(File.Exists(imagePath));

            service.SaveNow(Array.Empty<PouchItem>());
            Assert.False(File.Exists(imagePath));
        }

        [Fact]
        public void Load_LegacyFormat_Works()
        {
            File.WriteAllText(Path.Combine(_folder.Path, "shelf_state.json"),
                """[{ "FilePath": "C:\\x.txt", "TextContent": null, "DisplayName": "x" }]""");

            var loaded = new PersistenceService(_folder.Path).Load();

            Assert.Single(loaded);
            Assert.Null(loaded[0].Kind);
            Assert.Equal(@"C:\x.txt", loaded[0].FilePath);
        }

        [Fact]
        public void Load_CorruptFile_ReturnsEmpty()
        {
            File.WriteAllText(Path.Combine(_folder.Path, "shelf_state.json"), "{ not json");
            Assert.Empty(new PersistenceService(_folder.Path).Load());
        }

        [Fact]
        public async Task ScheduleSave_IsDebounced()
        {
            var service = new PersistenceService(_folder.Path);
            for (int i = 0; i < 5; i++)
            {
                service.ScheduleSave(new[] { new PouchItem { Kind = PouchItemKind.Text, TextContent = $"v{i}" } });
            }

            await Task.Delay(1000);
            var loaded = service.Load();

            Assert.Single(loaded);
            Assert.Equal("v4", loaded[0].TextContent);
        }
    }
}
