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

        private static Shelf ShelfWith(string name, params PouchItem[] items)
        {
            var shelf = new Shelf { Name = name, Color = "#3B82F6" };
            foreach (var item in items) shelf.Items.Add(item);
            return shelf;
        }

        private static BitmapSource Pixel(int size = 2)
        {
            var image = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, new byte[size * size * 4], size * 4);
            image.Freeze();
            return image;
        }

        [Fact]
        public void SaveNow_ThenLoad_RoundTripsShelvesAndAllKinds()
        {
            var service = new PersistenceService(_folder.Path);
            var work = ShelfWith("Work",
                new PouchItem { Kind = PouchItemKind.File, FilePath = @"C:\a.txt", DisplayName = "a", Label = ColorLabel.Red, IsPinned = true },
                new PouchItem { Kind = PouchItemKind.Stack, StackFiles = new[] { @"C:\a.txt", @"C:\b.txt" } },
                new PouchItem { Kind = PouchItemKind.Text, TextContent = "hello" },
                new PouchItem { Kind = PouchItemKind.Image, ImageContent = Pixel() });
            var links = ShelfWith("Links",
                new PouchItem { Kind = PouchItemKind.Link, TextContent = "https://example.com", Icon = Pixel(4) },
                new PouchItem { Kind = PouchItemKind.Color, TextContent = "#FF8800" });

            service.SaveNow(new[] { work, links }, links.Id);
            var state = service.Load();

            Assert.Equal(PersistedState.CurrentVersion, state.Version);
            Assert.Equal(links.Id, state.ActiveShelfId);
            Assert.Equal(new[] { "Work", "Links" }, state.Shelves.Select(s => s.Name));
            Assert.Equal("#3B82F6", state.Shelves[0].Color);

            var saved = state.Shelves[0].Items;
            Assert.Equal(work.Items.Select(i => i.Id), saved.Select(i => i.Id));
            Assert.Equal(ColorLabel.Red, saved[0].Label);
            Assert.True(saved[0].IsPinned);
            Assert.Equal(2, saved[1].StackFiles!.Count);
            Assert.Equal(2, service.LoadImage(saved[3].ImageFile!)!.PixelWidth);

            var link = state.Shelves[1].Items[0];
            Assert.Equal(PouchItemKind.Link, link.Kind);
            Assert.Equal(4, service.LoadImage(link.ImageFile!)!.PixelWidth);
            Assert.Null(state.Shelves[1].Items[1].ImageFile);
        }

        [Fact]
        public void RemovedImageItems_HaveTheirFilesPruned()
        {
            var service = new PersistenceService(_folder.Path);
            var item = new PouchItem { Kind = PouchItemKind.Image, ImageContent = Pixel(1) };
            var shelf = ShelfWith("Pouch", item);

            service.SaveNow(new[] { shelf }, shelf.Id);
            string imagePath = Path.Combine(_folder.Path, "images", $"{item.Id}.png");
            Assert.True(File.Exists(imagePath));

            shelf.Items.Clear();
            service.SaveNow(new[] { shelf }, shelf.Id);
            Assert.False(File.Exists(imagePath));
        }

        [Fact]
        public void Load_Version1Format_BecomesOneShelf()
        {
            File.WriteAllText(Path.Combine(_folder.Path, "shelf_state.json"),
                """[{ "FilePath": "C:\\x.txt", "TextContent": null, "DisplayName": "x" }, { "TextContent": "note", "DisplayName": "note" }]""");

            var state = new PersistenceService(_folder.Path).Load();

            var shelf = Assert.Single(state.Shelves);
            Assert.Equal("Pouch", shelf.Name);
            Assert.Equal(2, shelf.Items.Count);
            Assert.Null(shelf.Items[0].Kind);
            Assert.Equal(@"C:\x.txt", shelf.Items[0].FilePath);
        }

        [Fact]
        public void Load_CorruptFile_ReturnsEmpty()
        {
            File.WriteAllText(Path.Combine(_folder.Path, "shelf_state.json"), "{ not json");
            Assert.Empty(new PersistenceService(_folder.Path).Load().Shelves);
        }

        [Fact]
        public async Task ScheduleSave_IsDebounced()
        {
            var service = new PersistenceService(_folder.Path);
            var shelf = new Shelf();
            for (int i = 0; i < 5; i++)
            {
                shelf.Items.Clear();
                shelf.Items.Add(new PouchItem { Kind = PouchItemKind.Text, TextContent = $"v{i}" });
                service.ScheduleSave(new[] { shelf }, shelf.Id);
            }

            await Task.Delay(1000);
            var saved = Assert.Single(service.Load().Shelves.Single().Items);

            Assert.Equal("v4", saved.TextContent);
        }
    }
}
