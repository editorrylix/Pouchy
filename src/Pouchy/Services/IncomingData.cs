using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Pouchy.Interop;
using ComIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace Pouchy.Services
{
    public enum IncomingKind
    {
        None,

        /// <summary>Paths on disk: from Explorer, or virtual files Pouchy has just saved.</summary>
        Files,

        /// <summary>Text, maybe with its formatting.</summary>
        Text,

        /// <summary>A picture without a file.</summary>
        Image,
    }

    /// <summary>
    /// What a drop or the clipboard offers, read the way Pouchy prefers it. Apps offer the same thing
    /// in several formats at once, so the order matters:
    /// <list type="number">
    /// <item>Files (paths on disk).</item>
    /// <item>Virtual files: an Outlook attachment, a file inside a zip, a picture dragged from a browser.
    /// They're saved under <see cref="AppPaths.ReceivedFolder"/> and then treated as files. Internet
    /// shortcuts alone don't count: a browser offers one with every dragged link, and the link is better.</item>
    /// <item>Text, with its HTML and RTF formatting. Text comes before a picture because Excel and Word
    /// offer a picture of what was copied as well, and the text is what people want back. The exception
    /// is text that is just a web address next to a picture: that's a browser's "Copy image".</item>
    /// <item>A picture, preferring PNG (which keeps transparency) over a bitmap (which doesn't).</item>
    /// </list>
    /// Everything is read immediately, because a dropped data object only lives during the drop.
    /// </summary>
    public sealed class IncomingData
    {
        public IncomingKind Kind { get; private init; }
        public IReadOnlyList<string> Files { get; private init; } = Array.Empty<string>();
        public bool FromVirtualFiles { get; private init; }
        public string? Text { get; private init; }
        public string? Html { get; private init; }
        public string? Rtf { get; private init; }
        public BitmapSource? Image { get; private init; }

        public static readonly IncomingData Nothing = new() { Kind = IncomingKind.None };

        private static readonly string[] PngFormats = { "PNG", "image/png" };

        /// <param name="receivedFolder">Where virtual files are saved; null to ignore virtual files.</param>
        public static IncomingData Read(IDataObject data, string? receivedFolder)
        {
            if (TryGet(data, DataFormats.FileDrop) is string[] { Length: > 0 } files)
            {
                return new IncomingData { Kind = IncomingKind.Files, Files = files };
            }

            if (receivedFolder != null && data is ComIDataObject com && VirtualFiles.IsPresent(com))
            {
                var described = VirtualFiles.Describe(com);
                if (described.Count > 0 && !described.All(IsInternetShortcut))
                {
                    string folder = Path.Combine(receivedFolder, DateTime.Now.ToString("yyyy-MM-dd HHmmss-fff"));
                    var saved = VirtualFiles.SaveAll(com, folder);
                    if (saved.Count > 0)
                    {
                        Logger.Log($"Saved {saved.Count} virtual file(s) to {folder}.");
                        return new IncomingData { Kind = IncomingKind.Files, Files = saved, FromVirtualFiles = true };
                    }
                    TryDeleteEmpty(folder);
                }
            }

            string? text = (TryGet(data, DataFormats.UnicodeText) ?? TryGet(data, DataFormats.Text)) as string;
            var image = ReadImage(data);

            if (!string.IsNullOrWhiteSpace(text) && !(image != null && TextTools.IsUrl(text)))
            {
                return new IncomingData
                {
                    Kind = IncomingKind.Text,
                    Text = text,
                    Html = TryGet(data, DataFormats.Html) as string,
                    Rtf = TryGet(data, DataFormats.Rtf) as string,
                };
            }
            if (image != null) return new IncomingData { Kind = IncomingKind.Image, Image = image };
            return Nothing;
        }

        /// <summary>The PNG an app offers (with transparency), or else its bitmap.</summary>
        internal static BitmapSource? ReadImage(IDataObject data)
        {
            foreach (string format in PngFormats)
            {
                if (TryGet(data, format) is Stream stream && DecodePng(stream) is { } png) return png;
            }
            return TryGet(data, DataFormats.Bitmap) as BitmapSource;
        }

        internal static BitmapSource? DecodePng(Stream stream)
        {
            try
            {
                if (stream.CanSeek) stream.Position = 0;
                var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                frame.Freeze();
                return frame;
            }
            catch (Exception ex) when (ex is NotSupportedException or FileFormatException or IOException or ArgumentException)
            {
                Logger.Log("Couldn't read the offered PNG: " + ex.Message);
                return null;
            }
        }

        internal static bool IsInternetShortcut(VirtualFileInfo file) =>
            !file.IsFolder && (file.Name.EndsWith(".url", StringComparison.OrdinalIgnoreCase) ||
                               file.Name.EndsWith(".website", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Gets one format, or null. Some apps advertise formats they then fail to render, which WPF
        /// reports as an exception; that's treated as "not offered".
        /// </summary>
        private static object? TryGet(IDataObject data, string format)
        {
            try
            {
                return data.GetDataPresent(format) ? data.GetData(format) : null;
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or OutOfMemoryException or InvalidOperationException or ArgumentException)
            {
                Logger.Log($"The {format} data couldn't be read: {ex.Message}");
                return null;
            }
        }

        private static void TryDeleteEmpty(string folder)
        {
            try
            {
                if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            }
            catch (IOException)
            {
            }
        }
    }
}
