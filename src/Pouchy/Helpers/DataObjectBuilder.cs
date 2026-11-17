using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using Pouchy.Interop;
using Pouchy.Models;
using Pouchy.Services;

namespace Pouchy.Helpers
{
    /// <summary>
    /// Builds the <see cref="DataObject"/> used for drag-out and copy. Each kind of item is offered in
    /// every form other apps understand, so the drop target can pick:
    /// <list type="bullet">
    /// <item>Files: their paths.</item>
    /// <item>Pictures: a real .png file (Explorer and chat apps take files), PNG data (keeps
    /// transparency in Office, Discord, design tools) and a plain bitmap for older apps.</item>
    /// <item>Links: the address as text and as a URL (browsers), plus an Internet shortcut, so dropping
    /// on the desktop or in a folder makes a .url file, as dragging a link out of a browser does.</item>
    /// <item>Text: plain, plus the HTML and RTF formatting it came with.</item>
    /// </list>
    /// </summary>
    public static class DataObjectBuilder
    {
        /// <summary>Where pictures are written as .png files for dragging out. Null: no files (pictures go as data only).</summary>
        public static string? ExportFolder { get; set; }

        /// <returns>The data object, or null if none of the items has anything to transfer.</returns>
        public static DataObject? Build(IReadOnlyCollection<PouchItem> items)
        {
            var paths = items
                .SelectMany(i => i.FilePaths)
                .Where(p => File.Exists(p) || Directory.Exists(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool exportedImages = false;
            foreach (var picture in items.Where(i => i.Kind == PouchItemKind.Image))
            {
                if (ExportImage(picture) is { } exported)
                {
                    paths.Add(exported);
                    exportedImages = true;
                }
            }

            string text = string.Join(Environment.NewLine,
                items.Where(i => i.IsTextLike && !string.IsNullOrEmpty(i.TextContent))
                     .Select(i => i.TextContent));

            // Formats that describe one thing only make sense when it's the only thing being transferred.
            var single = items.Count == 1 ? items.First() : null;
            var image = single?.Kind == PouchItemKind.Image ? single.ImageContent : null;

            if (paths.Count == 0 && text.Length == 0 && image == null) return null;

            var data = new DataObject();
            if (paths.Count > 0) data.SetData(DataFormats.FileDrop, paths.ToArray());
            if (text.Length > 0) data.SetData(DataFormats.UnicodeText, text);

            if (single is { Kind: PouchItemKind.Text })
            {
                if (single.HtmlContent != null) data.SetData(DataFormats.Html, single.HtmlContent);
                if (single.RtfContent != null) data.SetData(DataFormats.Rtf, single.RtfContent);
            }
            if (single is { Kind: PouchItemKind.Link, TextContent: { } url }) AddLink(data, url.Trim(), single.DisplayName);

            if (image != null)
            {
                if (PngBytes(single!) is { } png) data.SetData("PNG", new MemoryStream(png));
                data.SetImage(image);
            }

            // The exported picture is a temporary copy: Explorer should copy it rather than move it,
            // even within the same drive (where moving is its default).
            if (exportedImages) data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes((int)DragDropEffects.Copy)));
            return data;
        }

        /// <summary>The picture as a .png file in <see cref="ExportFolder"/>, named after the item.</summary>
        internal static string? ExportImage(PouchItem item)
        {
            if (ExportFolder == null) return null;
            try
            {
                string folder = Path.Combine(ExportFolder, item.Id.ToString("N"));
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, ImageFileName(item));
                if (item.ImageFilePath != null && File.Exists(item.ImageFilePath))
                {
                    File.Copy(item.ImageFilePath, path, overwrite: true);
                }
                else if (PngBytes(item) is { } png)
                {
                    File.WriteAllBytes(path, png);
                }
                else
                {
                    return null;
                }
                return path;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Log("Could not write the picture for dragging out: " + ex.Message);
                return null;
            }
        }

        /// <summary>"Image 2026-11-17 16.30.05.png" for an unnamed picture, otherwise its name.</summary>
        internal static string ImageFileName(PouchItem item)
        {
            string name = item.DisplayName is { Length: > 0 } n && n != "Image" ? n : $"Image {item.AddedAt:yyyy-MM-dd HH.mm.ss}";
            if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
            return SafeFileName(name, "Image") + ".png";
        }

        private static byte[]? PngBytes(PouchItem item)
        {
            try
            {
                if (item.ImageFilePath != null && File.Exists(item.ImageFilePath)) return File.ReadAllBytes(item.ImageFilePath);
                if (item.ImageContent is not { } image) return null;
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = new MemoryStream();
                encoder.Save(stream);
                return stream.ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                Logger.Log("Could not read the picture: " + ex.Message);
                return null;
            }
        }

        private static void AddLink(DataObject data, string url, string title)
        {
            data.SetData("UniformResourceLocatorW", new MemoryStream(Encoding.Unicode.GetBytes(url + "\0")));
            data.SetData("UniformResourceLocator", new MemoryStream(Encoding.ASCII.GetBytes(url + "\0")));

            // The same Internet shortcut a browser offers, as a file that only exists in the drag.
            byte[] shortcut = Encoding.UTF8.GetBytes($"[InternetShortcut]\r\nURL={url}\r\n");
            string name = SafeFileName(title, LinkPreviewParser.HostName(url)) + ".url";
            data.SetData(VirtualFiles.DescriptorFormat, new MemoryStream(FileDescriptor(name, shortcut.Length)));
            data.SetData(VirtualFiles.ContentsFormat, new MemoryStream(shortcut));
        }

        /// <summary>A FileGroupDescriptorW describing one file of the given size.</summary>
        internal static byte[] FileDescriptor(string name, long size)
        {
            const int FdFileSize = 0x40;
            var block = new byte[4 + 592];
            BitConverter.GetBytes(1).CopyTo(block, 0);
            BitConverter.GetBytes(FdFileSize).CopyTo(block, 4);
            BitConverter.GetBytes((int)(size >> 32)).CopyTo(block, 4 + 64);
            BitConverter.GetBytes((int)(size & 0xFFFFFFFF)).CopyTo(block, 4 + 68);
            var chars = Encoding.Unicode.GetBytes(name.Length > 259 ? name[..259] : name);
            chars.CopyTo(block, 4 + 72);
            return block;
        }

        /// <summary>A name that's safe as a file name: no reserved characters, at most 120 characters.</summary>
        internal static string SafeFileName(string? name, string fallback)
        {
            var invalid = Path.GetInvalidFileNameChars();
            string cleaned = new string((name ?? "").Select(c => invalid.Contains(c) ? ' ' : c).ToArray());
            cleaned = string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim().TrimEnd('.');
            if (cleaned.Length > 120) cleaned = cleaned[..120].Trim();
            return cleaned.Length > 0 ? cleaned : fallback;
        }

        /// <summary>Removes exported pictures older than a day (a copied picture may still be pasted later).</summary>
        public static void CleanExportFolder()
        {
            if (ExportFolder == null || !Directory.Exists(ExportFolder)) return;
            foreach (var folder in Directory.EnumerateDirectories(ExportFolder))
            {
                try
                {
                    if (DateTime.Now - Directory.GetLastWriteTime(folder) > TimeSpan.FromDays(1)) Directory.Delete(folder, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Logger.Log($"Could not remove {folder}: {ex.Message}");
                }
            }
        }
    }
}
