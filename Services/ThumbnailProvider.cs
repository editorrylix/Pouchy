using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pouchy.Helpers;
using Pouchy.Interop;
using Pouchy.Models;

namespace Pouchy.Services
{
    public sealed record Thumbnail(ImageSource Image, ThumbnailStyle Style);

    public interface IThumbnailProvider
    {
        /// <summary>Best available preview for a path. Call off the UI thread; may be slow.</summary>
        Thumbnail? GetThumbnail(string path);

        /// <summary>Generic icon for stacks in list view.</summary>
        ImageSource? GetStackIcon();
    }

    /// <summary>Explorer-quality thumbnails via the Windows Shell, with icon fallbacks.</summary>
    public sealed class ShellThumbnailProvider : IThumbnailProvider, IDisposable
    {
        private const int ThumbnailPixels = 256;
        private const int IconPixels = 128;
        private static readonly Lazy<ImageSource> StackIcon = new(CreateStackIcon);

        private readonly StaWorker _sta = new("Pouchy thumbnails");

        public ImageSource? GetStackIcon() => StackIcon.Value;

        public Thumbnail? GetThumbnail(string path)
        {
            bool exists = File.Exists(path) || Directory.Exists(path);
            if (exists)
            {
                var thumbnail = _sta.Invoke(() =>
                    ShellThumbnails.GetImage(path, ThumbnailPixels, ShellThumbnails.Options.ThumbnailOnly));
                if (thumbnail != null)
                {
                    bool photoLike = PreviewSupport.IsImage(path) || PreviewSupport.IsVideo(path);
                    return new Thumbnail(thumbnail, photoLike ? ThumbnailStyle.Fill : ThumbnailStyle.Fit);
                }

                var icon = _sta.Invoke(() =>
                    ShellThumbnails.GetImage(path, IconPixels, ShellThumbnails.Options.IconOnly));
                if (icon != null) return new Thumbnail(icon, ThumbnailStyle.Icon);
            }

            // Missing files (or a shell failure): the generic icon for the extension.
            return GetFileTypeIcon(path, exists) is { } fallback ? new Thumbnail(fallback, ThumbnailStyle.Icon) : null;
        }

        public void Dispose() => _sta.Dispose();

        private static ImageSource? GetFileTypeIcon(string path, bool exists)
        {
            var info = new NativeMethods.SHFILEINFO();
            uint flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON;
            uint attributes = 0;
            if (!exists)
            {
                flags |= NativeMethods.SHGFI_USEFILEATTRIBUTES;
                attributes = NativeMethods.FILE_ATTRIBUTE_NORMAL;
            }

            NativeMethods.SHGetFileInfo(path, attributes, ref info, (uint)Marshal.SizeOf(info), flags);
            if (info.hIcon == IntPtr.Zero) return null;

            try
            {
                var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            catch (Exception ex)
            {
                Logger.Log("Error converting shell icon: " + ex.Message);
                return null;
            }
            finally
            {
                NativeMethods.DestroyIcon(info.hIcon);
            }
        }

        private static ImageSource CreateStackIcon()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(Brush(60, 60, 60), new Pen(Brush(120, 120, 120), 1), new Rect(4, 4, 24, 24), 4, 4);
                dc.DrawRoundedRectangle(Brush(80, 80, 80), new Pen(Brush(140, 140, 140), 1), new Rect(2, 2, 24, 24), 4, 4);
                dc.DrawRoundedRectangle(Brush(230, 230, 230), new Pen(Brush(255, 255, 255), 1), new Rect(0, 0, 24, 24), 4, 4);
            }
            var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;

            static SolidColorBrush Brush(byte r, byte g, byte b)
            {
                var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
                brush.Freeze();
                return brush;
            }
        }
    }
}
