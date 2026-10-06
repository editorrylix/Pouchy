using System.IO;
using System.Windows.Media;
using Pouchy.Models;
using Pouchy.Services;
using Xunit;

namespace Pouchy.Tests
{
    public class ItemFactoryTests : IDisposable
    {
        private sealed class NoIcons : IIconProvider
        {
            public ImageSource? GetFileIcon(string path) => null;
            public ImageSource? GetStackIcon() => null;
        }

        private readonly TestFolder _folder = new();
        private readonly ItemFactory _factory = new(new NoIcons());

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void Folder_DoesNotThrow_AndIsFolderKind()
        {
            string dir = _folder.Folder("Photos");
            _folder.File(Path.Combine("Photos", "a.txt"));
            _folder.File(Path.Combine("Photos", "b.txt"));

            var item = _factory.CreateFromPath(dir);

            Assert.Equal(PouchItemKind.Folder, item.Kind);
            Assert.Equal("Photos", item.DisplayName);
            Assert.Equal("Folder · 2 items", item.Metadata);
            Assert.False(item.IsMissing);
        }

        [Fact]
        public void File_HasExtensionAndSize()
        {
            string file = _folder.File("notes.md", new string('x', 2048));

            var item = _factory.CreateFromPath(file);

            Assert.Equal(PouchItemKind.File, item.Kind);
            Assert.Equal("notes", item.DisplayName);
            Assert.Equal("MD", item.FileExtension);
            Assert.Equal("MD · 2 KB", item.Metadata);
        }

        [Fact]
        public void MissingFile_IsFlagged()
        {
            var item = _factory.CreateFromPath(Path.Combine(_folder.Path, "gone.pdf"));

            Assert.True(item.IsMissing);
            Assert.StartsWith("Missing", item.Metadata);
        }

        [Fact]
        public void Refresh_DetectsDeletedFile()
        {
            string file = _folder.File("temp.txt");
            var item = _factory.CreateFromPath(file);
            Assert.False(item.IsMissing);

            File.Delete(file);
            _factory.Refresh(item);

            Assert.True(item.IsMissing);
        }

        [Fact]
        public void Stack_MixedFilesAndFolders_DoesNotThrow()
        {
            var paths = new[]
            {
                _folder.File("a.txt", "1234"),
                _folder.Folder("sub"),
                Path.Combine(_folder.Path, "missing.txt"),
            };

            var item = _factory.CreateStack(paths);

            Assert.Equal(PouchItemKind.Stack, item.Kind);
            Assert.Equal("Stack of 3 items", item.DisplayName);
            Assert.Equal("3 items · 4 B · 1 missing", item.Metadata);
            Assert.False(item.IsMissing);
        }

        [Fact]
        public void Text_NameIsTruncatedSingleLine()
        {
            var item = _factory.CreateText("first line\nsecond line that is quite long");

            Assert.Equal(PouchItemKind.Text, item.Kind);
            Assert.Equal("first line second line th...", item.DisplayName);
            Assert.True(item.IsQuickLookSupported);
        }

        [Fact]
        public void Restore_LegacyEntryWithoutKind_InfersKind()
        {
            string file = _folder.File("old.txt");
            string dir = _folder.Folder("oldDir");

            var restoredFile = _factory.Restore(new PersistedItem { FilePath = file, DisplayName = "old" }, null);
            var restoredDir = _factory.Restore(new PersistedItem { FilePath = dir, DisplayName = "oldDir" }, null);
            var restoredText = _factory.Restore(new PersistedItem { TextContent = "hi", DisplayName = "hi" }, null);

            Assert.Equal(PouchItemKind.File, restoredFile!.Kind);
            Assert.Equal(PouchItemKind.Folder, restoredDir!.Kind);
            Assert.Equal(PouchItemKind.Text, restoredText!.Kind);
        }

        [Fact]
        public void Restore_ImageWithoutBitmap_IsSkipped()
        {
            var saved = new PersistedItem { Kind = PouchItemKind.Image, ImageFile = "x.png" };
            Assert.Null(_factory.Restore(saved, null));
        }
    }
}
