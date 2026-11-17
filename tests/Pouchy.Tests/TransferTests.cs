using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pouchy.Helpers;
using Pouchy.Interop;
using Pouchy.Models;
using Pouchy.Services;
using Pouchy.ViewModels;
using Xunit;
using ComIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;
using DataObject = System.Windows.DataObject;

namespace Pouchy.Tests
{
    /// <summary>
    /// What comes in (drops, paste) and what goes out (drag-out, copy): virtual files, formatting,
    /// transparency, links as shortcuts and pictures as files.
    /// </summary>
    public class TransferTests : IDisposable
    {
        private sealed class NoIcons : IThumbnailProvider
        {
            public Thumbnail? GetThumbnail(string path) => null;
            public ImageSource? GetStackIcon() => null;
        }

        private readonly TestFolder _folder = new();

        public void Dispose()
        {
            DataObjectBuilder.ExportFolder = null;
            _folder.Dispose();
        }

        // ------------------------------------------------------------------ incoming

        [Fact]
        public void VirtualFiles_AreSaved_WithTheirFoldersAndBytes()
        {
            ViewSmokeTests.RunOnSta(() =>
            {
                var source = new FakeVirtualSource(
                    ("Report.pdf", Encoding.UTF8.GetBytes("pdf bytes"), false, true),
                    ("Photos", null, true, false),
                    ("Photos\\Beach.jpg", Encoding.UTF8.GetBytes("jpeg bytes"), false, false));

                // A real drop hands over WPF's wrapper around the source's COM object.
                var incoming = IncomingData.Read(new DataObject(source), Path.Combine(_folder.Path, "received"));

                Assert.Equal(IncomingKind.Files, incoming.Kind);
                Assert.True(incoming.FromVirtualFiles);
                Assert.Equal(new[] { "Report.pdf", "Photos" }, incoming.Files.Select(Path.GetFileName));
                Assert.Equal("pdf bytes", File.ReadAllText(incoming.Files[0]));
                Assert.Equal("jpeg bytes", File.ReadAllText(Path.Combine(incoming.Files[1], "Beach.jpg")));
                Assert.StartsWith(Path.Combine(_folder.Path, "received"), incoming.Files[0]);
            });
        }

        [Fact]
        public void VirtualFiles_IgnoredWithoutAFolder_AndShortcutsLoseToTheLink()
        {
            ViewSmokeTests.RunOnSta(() =>
            {
                // A browser's dragged link: the address as text, plus a .url "file".
                var link = new DataObject();
                link.SetData(DataFormats.UnicodeText, "https://example.com/page");
                link.SetData(VirtualFiles.DescriptorFormat, new MemoryStream(DataObjectBuilder.FileDescriptor("Example.url", 10)));
                link.SetData(VirtualFiles.ContentsFormat, new MemoryStream(new byte[10]));
                var read = IncomingData.Read(link, _folder.Path);
                Assert.Equal(IncomingKind.Text, read.Kind);
                Assert.Equal("https://example.com/page", read.Text);
                Assert.Empty(Directory.GetDirectories(_folder.Path));

                var attachment = new FakeVirtualSource(("notes.txt", Encoding.UTF8.GetBytes("hi"), false, true));
                Assert.Equal(IncomingKind.None, IncomingData.Read(new DataObject(attachment), receivedFolder: null).Kind);
            });
        }

        [Theory]
        [InlineData("a.txt", true)]
        [InlineData("sub\\b.txt", true)]
        [InlineData("..\\evil.txt", false)]
        [InlineData("sub\\..\\..\\evil.txt", false)]
        [InlineData("C:\\Windows\\evil.txt", true)] // Becomes "C_\Windows\evil.txt" inside the folder.
        public void VirtualFileNames_StayInsideTheFolder(string name, bool allowed)
        {
            string? path = VirtualFiles.SafePath(_folder.Path, name);
            Assert.Equal(allowed, path != null);
            if (path != null) Assert.StartsWith(_folder.Path + Path.DirectorySeparatorChar, path);
        }

        [Fact]
        public void TextBeatsThePictureOfIt_ButNotABrowsersCopyImage()
        {
            ViewSmokeTests.RunOnSta(() =>
            {
                // Excel: the cells as text and HTML, plus a picture of them.
                var cells = new DataObject();
                cells.SetData(DataFormats.UnicodeText, "Q1\t120\r\nQ2\t140");
                cells.SetData(DataFormats.Html, "<table><tr><td>Q1</td><td>120</td></tr></table>");
                cells.SetImage(SolidBitmap(Colors.White, 255));
                var read = IncomingData.Read(cells, null);
                Assert.Equal(IncomingKind.Text, read.Kind);
                Assert.Contains("<table>", read.Html);

                // A browser's "Copy image": the picture, plus its address as text.
                var copied = new DataObject();
                copied.SetData(DataFormats.UnicodeText, "https://example.com/cat.png");
                copied.SetImage(SolidBitmap(Colors.Orange, 255));
                Assert.Equal(IncomingKind.Image, IncomingData.Read(copied, null).Kind);
            });
        }

        [Fact]
        public void PngData_KeepsTransparency()
        {
            ViewSmokeTests.RunOnSta(() =>
            {
                var data = new DataObject();
                data.SetData("PNG", new MemoryStream(EncodePng(SolidBitmap(Colors.Red, 0x40))));
                data.SetImage(SolidBitmap(Colors.Red, 255)); // The bitmap version can't hold alpha.

                var read = IncomingData.Read(data, null);
                Assert.Equal(IncomingKind.Image, read.Kind);
                Assert.Equal(0x40, AlphaAt(read.Image!));
            });
        }

        [Fact]
        public async Task FormattedText_IsKept_SavedAndGivenBack()
        {
            var settings = new SettingsService(Path.Combine(_folder.Path, "settings.json"));
            var factory = new ItemFactory(new NoIcons());
            var vm = new PouchViewModel(factory, new PersistenceService(_folder.Path), settings);
            var item = await vm.AddIncomingAsync(Incoming("Hello world", "<b>Hello</b> world", @"{\rtf1 {\b Hello} world}"));
            Assert.NotNull(item);
            Assert.StartsWith("Formatted text", item!.Metadata);
            vm.Flush();

            var reloaded = new PouchViewModel(factory, new PersistenceService(_folder.Path), settings);
            await reloaded.LoadAsync(clearUnpinned: false);
            var restored = Assert.Single(reloaded.Items);
            Assert.Equal("<b>Hello</b> world", restored.HtmlContent);
            Assert.Equal(@"{\rtf1 {\b Hello} world}", restored.RtfContent);

            var data = DataObjectBuilder.Build(new[] { restored })!;
            Assert.Equal("Hello world", data.GetData(DataFormats.UnicodeText));
            Assert.True(data.GetDataPresent(DataFormats.Html));
            Assert.True(data.GetDataPresent(DataFormats.Rtf));

            // Editing the text drops the formatting, which no longer matches it.
            reloaded.UpdateText(restored, "Goodbye");
            Assert.False(reloaded.Items[0].HasFormatting);
        }

        // ------------------------------------------------------------------ outgoing

        [Fact]
        public void Pictures_GoOutAsPngFiles_WithTransparency()
        {
            ViewSmokeTests.RunOnSta(() =>
            {
                var factory = new ItemFactory(new NoIcons(), _folder.Folder("images"));
                var picture = factory.CreateImage(SolidBitmap(Colors.DeepSkyBlue, 0x80));
                DataObjectBuilder.ExportFolder = Path.Combine(_folder.Path, "out");

                var data = DataObjectBuilder.Build(new[] { picture })!;

                var file = Assert.Single((string[])data.GetData(DataFormats.FileDrop));
                Assert.Matches(@"^Image \d{4}-\d\d-\d\d \d\d\.\d\d\.\d\d\.png$", Path.GetFileName(file));
                using (var stream = File.OpenRead(file)) Assert.Equal(0x80, AlphaAt(BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad)));
                Assert.True(data.GetDataPresent("PNG"));
                Assert.True(data.GetDataPresent(DataFormats.Bitmap));
                Assert.True(data.GetDataPresent("Preferred DropEffect"));

                picture.DisplayName = "Logo: final?";
                Assert.Equal("Logo final.png", DataObjectBuilder.ImageFileName(picture));
            });
        }

        [Fact]
        public void Links_GoOutAsUrls_AndAsAShortcutFile()
        {
            ViewSmokeTests.RunOnSta(() =>
            {
                var link = new ItemFactory(new NoIcons()).CreateLink("https://github.com/editorrylix/Pouchy");
                link.DisplayName = "Pouchy on GitHub";

                var data = DataObjectBuilder.Build(new[] { link })!;
                Assert.Equal("https://github.com/editorrylix/Pouchy", data.GetData(DataFormats.UnicodeText));
                var urlW = (MemoryStream)data.GetData("UniformResourceLocatorW");
                Assert.Equal("https://github.com/editorrylix/Pouchy\0", Encoding.Unicode.GetString(urlW.ToArray()));

                // Read back through COM, the way Explorer asks for it when the link is dropped in a folder.
                var saved = VirtualFiles.SaveAll(data, Path.Combine(_folder.Path, "desktop"));
                var shortcut = Assert.Single(saved);
                Assert.Equal("Pouchy on GitHub.url", Path.GetFileName(shortcut));
                Assert.Contains("URL=https://github.com/editorrylix/Pouchy", File.ReadAllText(shortcut));
            });
        }

        [Fact]
        public void ReceivedFiles_NoItemUses_AreRemovedOnExit()
        {
            string received = _folder.Folder("received");
            string kept = Path.Combine(received, "2026-11-17 100000-000");
            string orphan = Path.Combine(received, "2026-11-17 110000-000");
            Directory.CreateDirectory(kept);
            Directory.CreateDirectory(orphan);
            File.WriteAllText(Path.Combine(kept, "invoice.pdf"), "x");
            File.WriteAllText(Path.Combine(orphan, "old.pdf"), "x");

            var factory = new ItemFactory(new NoIcons());
            var persistence = new PersistenceService(_folder.Path);
            var shelf = new Shelf();
            shelf.Items.Add(factory.CreateFromPath(Path.Combine(kept, "invoice.pdf")));
            persistence.SaveNow(new[] { shelf }, shelf.Id);

            Assert.True(Directory.Exists(kept));
            Assert.False(Directory.Exists(orphan));
        }

        // ------------------------------------------------------------------ helpers

        private static IncomingData Incoming(string text, string html, string rtf)
        {
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, text);
            data.SetData(DataFormats.Html, html);
            data.SetData(DataFormats.Rtf, rtf);
            return IncomingData.Read(data, null);
        }

        private static BitmapSource SolidBitmap(Color color, byte alpha)
        {
            const int size = 8;
            var pixels = new byte[size * size * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                // Premultiplied BGRA.
                pixels[i] = (byte)(color.B * alpha / 255);
                pixels[i + 1] = (byte)(color.G * alpha / 255);
                pixels[i + 2] = (byte)(color.R * alpha / 255);
                pixels[i + 3] = alpha;
            }
            var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Pbgra32, null, pixels, size * 4);
            bitmap.Freeze();
            return bitmap;
        }

        private static byte[] EncodePng(BitmapSource image)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }

        private static byte AlphaAt(BitmapSource image)
        {
            var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            var pixel = new byte[4];
            converted.CopyPixels(new Int32Rect(1, 1, 1, 1), pixel, 4, 0);
            return pixel[3];
        }

        /// <summary>
        /// Stands in for Outlook or a zip folder: describes files in FileGroupDescriptorW and hands each
        /// one over by index, as a stream or a memory block.
        /// </summary>
        private sealed class FakeVirtualSource : ComIDataObject
        {
            private const int DvEFormatEtc = unchecked((int)0x80040064);
            private readonly (string Name, byte[]? Bytes, bool Folder, bool AsStream)[] _files;

            public FakeVirtualSource(params (string Name, byte[]? Bytes, bool Folder, bool AsStream)[] files) => _files = files;

            private static short Id(string format) => (short)DataFormats.GetDataFormat(format).Id;

            private byte[] Descriptor()
            {
                var block = new byte[4 + 592 * _files.Length];
                BitConverter.GetBytes(_files.Length).CopyTo(block, 0);
                for (int i = 0; i < _files.Length; i++)
                {
                    int at = 4 + i * 592;
                    var (name, bytes, folder, _) = _files[i];
                    BitConverter.GetBytes(0x4 | 0x40).CopyTo(block, at);                 // FD_ATTRIBUTES | FD_FILESIZE
                    BitConverter.GetBytes(folder ? 0x10 : 0x80).CopyTo(block, at + 36);   // directory / normal
                    BitConverter.GetBytes(bytes?.Length ?? 0).CopyTo(block, at + 68);
                    Encoding.Unicode.GetBytes(name).CopyTo(block, at + 72);
                }
                return block;
            }

            public void GetData(ref FORMATETC format, out STGMEDIUM medium)
            {
                if (format.cfFormat == Id(VirtualFiles.DescriptorFormat))
                {
                    medium = HGlobal(Descriptor());
                    return;
                }
                if (format.cfFormat == Id(VirtualFiles.ContentsFormat) && format.lindex >= 0 && format.lindex < _files.Length)
                {
                    var file = _files[format.lindex];
                    medium = file.AsStream && (format.tymed & TYMED.TYMED_ISTREAM) != 0 ? Stream(file.Bytes!) : HGlobal(file.Bytes ?? Array.Empty<byte>());
                    return;
                }
                throw new COMException("Not offered", DvEFormatEtc);
            }

            public int QueryGetData(ref FORMATETC format) =>
                format.cfFormat == Id(VirtualFiles.DescriptorFormat) || format.cfFormat == Id(VirtualFiles.ContentsFormat) ? 0 : DvEFormatEtc;

            private static STGMEDIUM HGlobal(byte[] bytes)
            {
                IntPtr handle = Marshal.AllocHGlobal(Math.Max(1, bytes.Length));
                Marshal.Copy(bytes, 0, handle, bytes.Length);
                return new STGMEDIUM { tymed = TYMED.TYMED_HGLOBAL, unionmember = handle };
            }

            private static STGMEDIUM Stream(byte[] bytes)
            {
                Marshal.ThrowExceptionForHR(CreateStreamOnHGlobal(IntPtr.Zero, true, out var stream));
                stream.Write(bytes, bytes.Length, IntPtr.Zero);
                stream.Seek(0, 0, IntPtr.Zero);
                var pointer = Marshal.GetComInterfaceForObject(stream, typeof(IStream));
                Marshal.ReleaseComObject(stream);
                return new STGMEDIUM { tymed = TYMED.TYMED_ISTREAM, unionmember = pointer };
            }

            [DllImport("ole32.dll")]
            private static extern int CreateStreamOnHGlobal(IntPtr hGlobal, bool deleteOnRelease, out IStream stream);

            public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) => throw new COMException("Not offered", DvEFormatEtc);
            public int GetCanonicalFormatEtc(ref FORMATETC formatIn, out FORMATETC formatOut)
            {
                formatOut = formatIn;
                return 0x00040130; // DATA_S_SAMEFORMATETC
            }
            public void SetData(ref FORMATETC formatIn, ref STGMEDIUM medium, bool release) => throw new COMException("Read only", unchecked((int)0x80004001));
            public IEnumFORMATETC EnumFormatEtc(DATADIR direction) => throw new COMException("Not implemented", unchecked((int)0x80004001));
            public int DAdvise(ref FORMATETC format, ADVF advf, IAdviseSink adviseSink, out int connection)
            {
                connection = 0;
                return unchecked((int)0x80040003); // OLE_E_ADVISENOTSUPPORTED
            }
            public void DUnadvise(int connection) => throw new COMException("Not implemented", unchecked((int)0x80004001));
            public int EnumDAdvise(out IEnumSTATDATA? enumAdvise)
            {
                enumAdvise = null;
                return unchecked((int)0x80040003);
            }
        }
    }
}
