using System.IO;
using System.IO.Compression;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pouchy.Services;
using Xunit;

namespace Pouchy.Tests
{
    public class FileActionsTests : IDisposable
    {
        private readonly TestFolder _folder = new();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void UniquePath_AddsCounter()
        {
            string original = _folder.File("report.pdf");
            _folder.File("report (2).pdf");

            Assert.Equal(Path.Combine(_folder.Path, "report (3).pdf"), FileActions.UniquePath(original));
            Assert.Equal(Path.Combine(_folder.Path, "new.pdf"), FileActions.UniquePath(Path.Combine(_folder.Path, "new.pdf")));
        }

        [Fact]
        public void Rename_File()
        {
            string original = _folder.File("old.txt", "content");

            string renamed = FileActions.Rename(original, "  new.txt ");

            Assert.Equal(Path.Combine(_folder.Path, "new.txt"), renamed);
            Assert.False(File.Exists(original));
            Assert.Equal("content", File.ReadAllText(renamed));
        }

        [Fact]
        public void Rename_FolderCaseOnly()
        {
            string original = _folder.Folder("photos");

            string renamed = FileActions.Rename(original, "Photos");

            Assert.Equal("Photos", new DirectoryInfo(renamed).Name);
            Assert.Contains("Photos", Directory.GetDirectories(_folder.Path).Select(Path.GetFileName));
        }

        [Fact]
        public void Rename_ToExistingName_Throws()
        {
            string a = _folder.File("a.txt");
            _folder.File("b.txt");

            Assert.Throws<IOException>(() => FileActions.Rename(a, "b.txt"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("bad:name.txt")]
        [InlineData("a/b")]
        [InlineData("..")]
        public void ValidateFileName_RejectsInvalid(string name)
        {
            Assert.Throws<ArgumentException>(() => FileActions.ValidateFileName(name));
        }

        [Fact]
        public void MoveTo_And_CopyTo_KeepBothOnConflict()
        {
            string target = _folder.Folder("target");
            File.WriteAllText(Path.Combine(target, "a.txt"), "existing");
            string a = _folder.File("a.txt", "moved");
            string dir = _folder.Folder("dir");
            File.WriteAllText(Path.Combine(dir, "inner.txt"), "x");

            var copied = FileActions.CopyTo(new[] { dir }, target);
            Assert.True(File.Exists(Path.Combine(copied[0], "inner.txt")));
            Assert.True(Directory.Exists(dir));

            var moved = FileActions.MoveTo(new[] { a }, target);
            Assert.Equal(Path.Combine(target, "a (2).txt"), moved[0]);
            Assert.Equal("moved", File.ReadAllText(moved[0]));
            Assert.Equal("existing", File.ReadAllText(Path.Combine(target, "a.txt")));
            Assert.False(File.Exists(a));
        }

        [Fact]
        public void CompressAndExtract_RoundTrip()
        {
            string file = _folder.File("notes.txt", "hello");
            string dir = _folder.Folder("pics");
            File.WriteAllText(Path.Combine(dir, "one.txt"), "1");
            Directory.CreateDirectory(Path.Combine(dir, "nested"));
            File.WriteAllText(Path.Combine(dir, "nested", "two.txt"), "2");

            string zip = FileActions.CompressToZip(new[] { file, dir });

            Assert.Equal(Path.Combine(_folder.Path, "Archive.zip"), zip);
            using (var archive = ZipFile.OpenRead(zip))
            {
                var names = archive.Entries.Select(e => e.FullName).OrderBy(n => n).ToList();
                Assert.Equal(new[] { "notes.txt", "pics/nested/two.txt", "pics/one.txt" }, names);
            }

            string extracted = FileActions.ExtractZip(zip);
            Assert.Equal(Path.Combine(_folder.Path, "Archive"), extracted);
            Assert.Equal("2", File.ReadAllText(Path.Combine(extracted, "pics", "nested", "two.txt")));
        }

        [Fact]
        public void Compress_SingleFile_UsesItsName()
        {
            string file = _folder.File("budget.xlsx");
            Assert.Equal(Path.Combine(_folder.Path, "budget.zip"), FileActions.CompressToZip(new[] { file }));
        }

        [Fact]
        public void ConvertAndResizeImage()
        {
            string png = Path.Combine(_folder.Path, "pic.png");
            var bitmap = BitmapSource.Create(40, 20, 96, 96, PixelFormats.Bgra32, null, new byte[40 * 20 * 4], 40 * 4);
            FileActions.SaveImage(bitmap, png, ImageFileFormat.Png);

            string jpg = FileActions.ConvertImage(png, ImageFileFormat.Jpeg);
            Assert.Equal(Path.Combine(_folder.Path, "pic.jpg"), jpg);
            Assert.Equal(40, FileActions.LoadImage(jpg).PixelWidth);

            string half = FileActions.ResizeImage(png, 0.5);
            var resized = FileActions.LoadImage(half);
            Assert.Equal(20, resized.PixelWidth);
            Assert.Equal(10, resized.PixelHeight);
            Assert.StartsWith("pic (50", Path.GetFileName(half));
        }

        [Fact]
        public void ReadText_IsCapped()
        {
            string file = _folder.File("big.txt", new string('x', 5000));
            Assert.Equal(100, FileActions.ReadText(file, maxChars: 100).Length);
        }

        [Theory]
        [InlineData("https://example.com/page", true)]
        [InlineData("  http://example.com  ", true)]
        [InlineData("example.com", false)]
        [InlineData("see https://example.com", false)]
        [InlineData("ftp://example.com", false)]
        public void TextTools_IsUrl(string text, bool expected)
        {
            Assert.Equal(expected, TextTools.IsUrl(text));
        }

        [Fact]
        public void TextTools_TidyWhitespace()
        {
            string messy = "  first  \n\n\n   second\t\n\n";
            Assert.Equal($"first{Environment.NewLine}{Environment.NewLine}second", TextTools.TidyWhitespace(messy));
        }
    }
}
