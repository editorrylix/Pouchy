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

            Assert.True(_persistence.Load().Shelves.Single().Items.Single().IsPinned);
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

        // ---------------------------------------------------------------- Phase 4: shelves, search, undo

        [Fact]
        public void NewShelf_BecomesActive_AndItemsGoThere()
        {
            _vm.AddText("in pouch");
            var work = _vm.NewShelf("  Work ");

            Assert.Equal("Work", work.Name);
            Assert.Same(work, _vm.ActiveShelf);
            Assert.True(work.IsActive);
            Assert.False(_vm.Shelves[0].IsActive);
            Assert.True(_vm.IsEmpty);

            _vm.AddText("in work");
            Assert.Single(work.Items);
            Assert.Single(_vm.Shelves[0].Items);
            Assert.NotEqual(_vm.Shelves[0].Color, work.Color);
        }

        [Fact]
        public void MoveToShelf_MovesItems()
        {
            var a = _vm.AddText("a")!;
            var b = _vm.AddText("b")!;
            var first = _vm.ActiveShelf;
            var other = _vm.NewShelf("Other");

            _vm.MoveToShelf(new[] { a, b }, other);

            Assert.Empty(first.Items);
            Assert.Equal(new[] { a, b }, other.Items);
        }

        [Fact]
        public void DeleteShelf_ActivatesNeighbour_AndKeepsLastShelf()
        {
            var first = _vm.ActiveShelf;
            var second = _vm.NewShelf("Second");

            _vm.DeleteShelf(second);
            Assert.Same(first, _vm.ActiveShelf);
            Assert.Single(_vm.Shelves);

            _vm.DeleteShelf(first);
            Assert.Single(_vm.Shelves);
        }

        [Fact]
        public void CycleShelf_Wraps()
        {
            var first = _vm.ActiveShelf;
            var second = _vm.NewShelf("Second");

            _vm.CycleShelf(1);
            Assert.Same(first, _vm.ActiveShelf);
            _vm.CycleShelf(-1);
            Assert.Same(second, _vm.ActiveShelf);
        }

        [Fact]
        public void Search_FindsItemsOnEveryShelf()
        {
            _vm.AddText("quarterly budget draft");
            _vm.NewShelf("Other");
            _vm.AddText("budget for the trip");
            _vm.AddText("unrelated");

            _vm.IsSearchOpen = true;
            _vm.SearchText = "budget";

            Assert.True(_vm.IsFiltering);
            Assert.Equal(2, _vm.DisplayedItems.Count);
            Assert.Same(_vm.SearchResults, _vm.DisplayedItems);

            _vm.SearchText = "budget trip";
            Assert.Single(_vm.DisplayedItems);

            _vm.IsSearchOpen = false;
            Assert.Equal("", _vm.SearchText);
            Assert.Same(_vm.Items, _vm.DisplayedItems);
        }

        [Fact]
        public void LabelFilter_CombinesWithText()
        {
            var red = _vm.AddText("red note")!;
            _vm.AddText("plain note");
            _vm.SetLabel(new[] { red }, ColorLabel.Red);

            _vm.IsSearchOpen = true;
            var redOption = _vm.LabelOptions.Single(o => o.Label == ColorLabel.Red);
            _vm.ToggleLabelFilterCommand.Execute(redOption);

            Assert.Equal(new[] { red }, _vm.DisplayedItems);
            Assert.True(redOption.IsSelected);

            _vm.ToggleLabelFilterCommand.Execute(redOption);
            Assert.Equal(ColorLabel.None, _vm.LabelFilter);
        }

        [Fact]
        public void RemoveThenUndo_RestoresOrder()
        {
            var a = _vm.AddText("a")!;
            var b = _vm.AddText("b")!;
            var c = _vm.AddText("c")!;

            _vm.RemoveItems(new[] { c, a });
            Assert.Equal(new[] { b }, _vm.Items);
            Assert.Equal("Removed 2 items", _vm.UndoMessage);

            _vm.Undo();
            Assert.Equal(new[] { a, b, c }, _vm.Items);
            Assert.Null(_vm.UndoMessage);
            Assert.False(_vm.CanUndo);
        }

        [Fact]
        public void Clear_IsUndoable()
        {
            _vm.AddText("a");
            _vm.AddText("b");

            _vm.ClearCommand.Execute(null);
            Assert.Empty(_vm.Items);
            Assert.Equal("Cleared 2 items", _vm.UndoMessage);

            _vm.UndoCommand.Execute(null);
            Assert.Equal(2, _vm.Items.Count);
        }

        [Fact]
        public void RemoveWithoutUndo_LeavesNothingToUndo()
        {
            var a = _vm.AddText("a")!;
            _vm.RemoveItems(new[] { a }, undoable: false);
            Assert.False(_vm.CanUndo);
            Assert.Null(_vm.UndoMessage);
        }

        [Fact]
        public void AddText_DetectsLinksAndColours()
        {
            var link = _vm.AddText("https://www.example.com/page")!;
            var color = _vm.AddText(" #ff8800 ")!;
            var text = _vm.AddText("just words")!;

            Assert.Equal(PouchItemKind.Link, link.Kind);
            Assert.Equal("example.com", link.DisplayName);
            Assert.Equal(PouchItemKind.Color, color.Kind);
            Assert.Equal("#FF8800", color.DisplayName);
            Assert.Equal("rgb(255, 136, 0)", color.Metadata);
            Assert.NotNull(color.SwatchBrush);
            Assert.Equal(PouchItemKind.Text, text.Kind);
        }

        [Fact]
        public async Task ShelvesLabelsAndLinks_SurviveRestart()
        {
            var link = _vm.AddText("https://example.com")!;
            _vm.SetLabel(new[] { link }, ColorLabel.Blue);
            var work = _vm.NewShelf("Work");
            _vm.AddText("#00FF00");
            _vm.Flush();

            var settings = new SettingsService(Path.Combine(_folder.Path, "settings2.json"));
            var restored = new PouchViewModel(new ItemFactory(new NoIcons()), _persistence, settings);
            await restored.LoadAsync(clearUnpinned: false);

            Assert.Equal(new[] { "Pouch", "Work" }, restored.Shelves.Select(s => s.Name));
            Assert.Equal(work.Id, restored.ActiveShelf.Id);
            Assert.Equal(PouchItemKind.Color, restored.Items.Single().Kind);
            var restoredLink = restored.Shelves[0].Items.Single();
            Assert.Equal(PouchItemKind.Link, restoredLink.Kind);
            Assert.Equal(ColorLabel.Blue, restoredLink.Label);
        }

        [Fact]
        public async Task ClearOnStartup_KeepsPinned()
        {
            var keep = _vm.AddText("keep")!;
            _vm.AddText("drop");
            _vm.TogglePin(new[] { keep });
            _vm.Flush();

            var settings = new SettingsService(Path.Combine(_folder.Path, "settings2.json"));
            var restored = new PouchViewModel(new ItemFactory(new NoIcons()), _persistence, settings);
            await restored.LoadAsync(clearUnpinned: true);

            Assert.Equal("keep", restored.Items.Single().TextContent);
        }
    }
}
