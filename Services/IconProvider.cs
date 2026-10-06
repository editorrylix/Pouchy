using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pouchy.Interop;
using Pouchy.Models;

namespace Pouchy.Services
{
    public interface IIconProvider
    {
        /// <summary>Thumbnail for images, shell icon for everything else. Safe to call off the UI thread.</summary>
        ImageSource? GetFileIcon(string path);

        ImageSource? GetStackIcon();
    }

    public sealed class ShellIconProvider : IIconProvider
    {
        private static readonly Lazy<ImageSource> StackIcon = new(CreateStackIcon);

        public ImageSource? GetStackIcon() => StackIcon.Value;

        public ImageSource? GetFileIcon(string path)
        {
            if (PreviewSupport.IsImage(path) && File.Exists(path))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 96;
                    bmp.UriSource = new Uri(path);
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                catch (Exception ex)
                {
                    Logger.Log("Error loading image thumbnail: " + ex.Message);
                }
            }

            return GetShellIcon(path);
        }

        private static ImageSource? GetShellIcon(string path)
        {
            var info = new NativeMethods.SHFILEINFO();
            uint flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON;
            uint attributes = 0;

            // For paths that no longer exist, ask for the generic icon of the extension.
            if (!File.Exists(path) && !Directory.Exists(path))
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
