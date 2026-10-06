using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pouchy.Interop
{
    /// <summary>
    /// Thumbnails and icons through IShellItemImageFactory: the same previews Explorer shows,
    /// for any file type that has a thumbnail handler (photos, videos, PDFs, Office docs...).
    /// Call from an STA thread.
    /// </summary>
    internal static class ShellThumbnails
    {
        [Flags]
        public enum Options
        {
            ResizeToFit = 0x0,
            BiggerSizeOk = 0x1,
            IconOnly = 0x4,
            ThumbnailOnly = 0x8,
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(NativeSize size, Options flags, out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DIBSECTION
        {
            public BITMAP dsBm;
            public BITMAPINFOHEADER dsBmih;
            public uint dsBitfield0;
            public uint dsBitfield1;
            public uint dsBitfield2;
            public IntPtr dshSection;
            public uint dsOffset;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            string pszPath,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, ref DIBSECTION lpvObject);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        /// <returns>A frozen bitmap, or null if the shell has nothing for these options.</returns>
        public static BitmapSource? GetImage(string path, int size, Options options)
        {
            IShellItemImageFactory? factory = null;
            try
            {
                SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out factory);
                int hr = factory.GetImage(new NativeSize { cx = size, cy = size }, options, out IntPtr hbitmap);
                if (hr != 0 || hbitmap == IntPtr.Zero) return null;

                try
                {
                    return ToBitmapSource(hbitmap);
                }
                finally
                {
                    DeleteObject(hbitmap);
                }
            }
            catch (Exception)
            {
                // Missing file, no handler, handler crashed: all just mean "no thumbnail".
                return null;
            }
            finally
            {
                if (factory != null) Marshal.ReleaseComObject(factory);
            }
        }

        private static BitmapSource ToBitmapSource(IntPtr hbitmap)
        {
            var dib = new DIBSECTION();
            int size = Marshal.SizeOf<DIBSECTION>();
            if (GetObject(hbitmap, size, ref dib) == size && dib.dsBm.bmBits != IntPtr.Zero && dib.dsBm.bmBitsPixel == 32)
            {
                // Copy the raw pixels: CreateBitmapSourceFromHBitmap drops the alpha channel.
                int width = dib.dsBm.bmWidth;
                int height = dib.dsBm.bmHeight;
                int stride = dib.dsBm.bmWidthBytes;
                var pixels = new byte[stride * height];
                Marshal.Copy(dib.dsBm.bmBits, pixels, 0, pixels.Length);

                if (dib.dsBmih.biHeight > 0) FlipRows(pixels, stride, height);

                bool hasAlpha = false;
                for (int i = 3; i < pixels.Length; i += 4)
                {
                    if (pixels[i] != 0)
                    {
                        hasAlpha = true;
                        break;
                    }
                }

                var format = hasAlpha ? PixelFormats.Pbgra32 : PixelFormats.Bgr32;
                var bitmap = BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
                bitmap.Freeze();
                return bitmap;
            }

            var fallback = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            fallback.Freeze();
            return fallback;
        }

        /// <summary>Bottom-up DIBs store the last row first.</summary>
        private static void FlipRows(byte[] pixels, int stride, int height)
        {
            var row = new byte[stride];
            for (int top = 0, bottom = height - 1; top < bottom; top++, bottom--)
            {
                Buffer.BlockCopy(pixels, top * stride, row, 0, stride);
                Buffer.BlockCopy(pixels, bottom * stride, pixels, top * stride, stride);
                Buffer.BlockCopy(row, 0, pixels, bottom * stride, stride);
            }
        }
    }
}
