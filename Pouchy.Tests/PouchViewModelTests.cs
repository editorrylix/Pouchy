using System.IO;
using System.Windows.Media;
using Pouchy.Models;
using Pouchy.Services;
using Pouchy.ViewModels;
using Xunit;

namespace Pouchy.Tests
{
    public class PouchViewModelTests : IDisposable
    {
        private sealed class NoIcons : IThumbnailProvider
        {
            public Thumbnail? GetThumbnail(string path) => null;
            public ImageSource? GetStackIcon() => null;
        }

        private readonly TestFolder _folder = new();
        private readonly PouchViewModel _vm;
        private readonly PersistenceService _persistence;

        public PouchViewModelTests()
        {
            var settings = new SettingsService(Path.Combine(_folder.Path, "settings.json"));
            _persistence = new PersistenceService(Path.Combine(_folder.Path, "state"));
            _vm = new PouchViewModel(new ItemFactory(new NoIcons()), _persistence, settings);
        }

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void Clear_KeepsPinnedItems()
        {
            var keep = _vm.AddText("keep")!;
            _vm.AddText("drop");
            _vm.TogglePin(new[] { keep });

            _vm.ClearCommand.Execute(null);

            Assert.Equal(new[] { keep }, _vm.Items);
        }

        [Fact]
        public void TogglePin_PinsAllUnlessAllPinned()
        {
            var a = _vm.AddText("a")!;
            var b = _vm.AddText("b")!;
            a.IsPinned = true;

            _vm.TogglePin(new[] { a, b });
            Assert.True(a.IsPinned && b.IsPinned);

            _vm.TogglePin(new[] { a, b });
            Assert.False(a.IsPinned || b.IsPinned);
        }

        [Fact]
        public void Pinned_IsPersisted()
        {
            var item = _vm.AddText("remember me")!;
            _vm.TogglePin(new[] { item });
            _vm.Flush();

            Assert.True(_persistence.Load().Single().IsPinned);
        }

        [Fact]
        public void AddText_ReturnsExistingDuplicate()
        {
            var first = _vm.AddText("same");
            var second = _vm.AddText("same");

            Assert.Same(first, second);
            Assert.Single(_vm.Items);
        }

        [Fact]
        public async Task GroupThenUngroup_RoundTrips()
        {
            string a = _folder.File("a.txt");
            string b = _folder.File("b.txt");
            string c = _folder.File("c.txt");
            var note = _vm.AddText("note")!;
            await _vm.AddPathsAsync(new[] { a });
            await _vm.AddPathsAsync(new[] { b });
            await _vm.AddPathsAsync(new[] { c });
            var fileItems = _vm.Items.Where(i => i.Kind == PouchItemKind.File).ToList();

            await _vm.GroupAsync(fileItems.Take(2).ToList());

            Assert.Equal(3, _vm.Items.Count);
            var stack = _vm.Items[1];
            Assert.Equal(PouchItemKind.Stack, stack.Kind);
            Assert.Equal(new[] { a, b }, stack.StackFiles);
            Assert.Same(note, _vm.Items[0]);

            await _vm.UngroupAsync(stack);

            Assert.Equal(new[] { "note", "a", "b", "c" }, _vm.Items.Select(i => i.DisplayName));
        }

        [Fact]
        public async Task UpdatePaths_KeepsIdPositionAndPin()
        {
            string a = _folder.File("a.txt");
            await _vm.AddPathsAsync(new[] { a });
            var item = _vm.Items[0];
            item.IsPinned = true;
            string renamed = FileActions.Rename(a, "renamed.txt");

            await _vm.UpdatePathsAsync(item, new[] { renamed });

            var updated = Assert.Single(_vm.Items);
            Assert.Equal(item.Id, updated.Id);
            Assert.Equal(renamed, updated.FilePath);
            Assert.Equal("renamed", updated.DisplayName);
            Assert.True(updated.IsPinned);
        }

        [Fact]
        public void UpdateText_ReplacesSnippet()
        {
            var item = _vm.AddText("hello")!;

            _vm.UpdateText(item, "HELLO");

            var updated = Assert.Single(_vm.Items);
            Assert.Equal("HELLO", updated.TextContent);
            Assert.Equal(item.Id, updated.Id);
        }

        [Fact]
        public async Task InsertAfter_PlacesNewItemNextToSource()
        {
            var first = _vm.AddText("first")!;
            _vm.AddText("last");
            string zip = _folder.File("archive.zip");

            await _vm.InsertAfterAsync(first, zip);

            Assert.Equal(new[] { "first", "archive", "last" }, _vm.Items.Select(i => i.DisplayName));
        }
    }
}
