using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pouchy.Models;
using Pouchy.Services;
using Xunit;

namespace Pouchy.Tests
{
    /// <summary>Image items keep their full picture on disk and only a thumbnail in memory.</summary>
    public class ImageStorageTests : IDisposable
    {
        private sealed class NoIcons : IThumbnailProvider
        {
            public Thumbnail? GetThumbnail(string path) => null;
            public ImageSource? GetStackIcon() => null;
        }

        private readonly TestFolder _folder = new();

        public void Dispose() => _folder.Dispose();

        private static BitmapSource Big(int width = 1600, int height = 900)
        {
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, new byte[width * height * 4], width * 4);
            bitmap.Freeze();
            return bitmap;
        }

        [Fact]
        public void CreateImage_WithCacheFolder_KeepsOnlyAThumbnailInMemory()
        {
            var persistence = new PersistenceService(_folder.Path);
            var factory = new ItemFactory(new NoIcons(), persistence.ImageFolder);

            var item = factory.CreateImage(Big());

            Assert.Null(item.InMemoryImage);
            Assert.True(File.Exists(item.ImageFilePath));
            Assert.True(((BitmapSource)item.Icon!).PixelWidth <= 360);
            Assert.Equal("Image · 1600×900", item.Metadata);
            Assert.Equal(1600, item.ImageContent!.PixelWidth); // Full picture loads on demand.
        }

        [Fact]
        public void FileBackedImage_SurvivesSaveAndRestore()
        {
            var persistence = new PersistenceService(_folder.Path);
            var factory = new ItemFactory(new NoIcons(), persistence.ImageFolder);
            var item = factory.CreateImage(Big(800, 600));
            var shelf = new Shelf();
            shelf.Items.Add(item);

            persistence.SaveNow(new[] { shelf }, shelf.Id);
            var saved = persistence.Load().Shelves.Single().Items.Single();
            var restored = factory.Restore(saved, persistence.ImagePath(saved.ImageFile!));

            Assert.NotNull(restored);
            Assert.Equal(item.Id, restored!.Id);
            Assert.Equal(item.ImageFilePath, restored.ImageFilePath);
            Assert.Equal(800, restored.ImageContent!.PixelWidth);
        }

        [Fact]
        public async Task ScheduledSave_KeepsRemovedImages_ForUndo_ButExitPrunes()
        {
            var persistence = new PersistenceService(_folder.Path);
            var factory = new ItemFactory(new NoIcons(), persistence.ImageFolder);
            var item = factory.CreateImage(Big(64, 64));
            var shelf = new Shelf();

            persistence.ScheduleSave(new[] { shelf }, shelf.Id);
            await Task.Delay(800);
            Assert.True(File.Exists(item.ImageFilePath)); // Undo could still bring it back.

            persistence.SaveNow(new[] { shelf }, shelf.Id);
            Assert.False(File.Exists(item.ImageFilePath));
        }
    }
}
