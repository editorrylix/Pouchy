using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ComIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace Pouchy.Interop
{
    /// <summary>
    /// Attaches a picture to a drag operation through the shell's drag image helper, so
    /// Explorer and other drop targets show the dragged tile under the cursor.
    /// </summary>
    internal static class DragImage
    {
        [ComImport]
        [Guid("DE5BF786-477A-11D2-839D-00C04FD918D0")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDragSourceHelper
        {
            void InitializeFromBitmap(ref SHDRAGIMAGE pshdi, ComIDataObject pDataObject);
            void InitializeFromWindow(IntPtr hwnd, ref NativeMethods.POINT ppt, ComIDataObject pDataObject);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SHDRAGIMAGE
        {
            public SIZE sizeDragImage;
            public NativeMethods.POINT ptOffset;
            public IntPtr hbmpDragImage;
            public int crColorKey;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        private static readonly Guid DragDropHelperClsid = new("4657278A-411B-11D2-839A-00C04FD918D0");

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        /// <summary>Renders <paramref name="element"/> (with a count badge for several items) as the drag image.</summary>
        /// <returns>False if the shell helper isn't available; the drag still works, just without a picture.</returns>
        public static bool Attach(ComIDataObject data, FrameworkElement element, int count, Brush accent, Point cursorInElement)
        {
            IntPtr hbitmap = IntPtr.Zero;
            try
            {
                var source = PresentationSource.FromVisual(element);
                double scale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                var image = Render(element, count, accent, scale);

                hbitmap = CreatePremultipliedBitmap(image);
                var drag = new SHDRAGIMAGE
                {
                    sizeDragImage = new SIZE { cx = image.PixelWidth, cy = image.PixelHeight },
                    ptOffset = new NativeMethods.POINT { x = (int)(cursorInElement.X * scale), y = (int)(cursorInElement.Y * scale) },
                    hbmpDragImage = hbitmap,
                    crColorKey = unchecked((int)0xFFFFFFFF),
                };

                var helper = (IDragSourceHelper)Activator.CreateInstance(Type.GetTypeFromCLSID(DragDropHelperClsid)!)!;
                helper.InitializeFromBitmap(ref drag, data);
                Marshal.ReleaseComObject(helper);
                hbitmap = IntPtr.Zero; // The helper owns the bitmap now.
                return true;
            }
            catch (Exception ex)
            {
                Services.Logger.Log("Drag image unavailable: " + ex.Message);
                return false;
            }
            finally
            {
                if (hbitmap != IntPtr.Zero) DeleteObject(hbitmap);
            }
        }

        private static BitmapSource Render(FrameworkElement element, int count, Brush accent, double scale)
        {
            double width = Math.Max(1, element.ActualWidth);
            double height = Math.Max(1, element.ActualHeight);

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // Several items: a second card peeking out behind the first.
                if (count > 1)
                {
                    dc.PushOpacity(0.55);
                    dc.DrawRectangle(new VisualBrush(element), null, new Rect(6, 6, width, height));
                    dc.Pop();
                }
                dc.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, width, height));

                if (count > 1)
                {
                    var text = new FormattedText(count.ToString(), System.Globalization.CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 12, Brushes.White, scale);
                    double diameter = Math.Max(22, text.Width + 12);
                    var center = new Point(width - 2, 4);
                    dc.DrawEllipse(accent, new Pen(Brushes.White, 2), center, diameter / 2, 11);
                    dc.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
                }
            }

            int pixelWidth = (int)Math.Ceiling((width + 18) * scale);
            int pixelHeight = (int)Math.Ceiling((height + 18) * scale);
            var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            var host = new DrawingVisual();
            using (var dc = host.RenderOpen())
            {
                // Leave room on the top-right for the badge.
                dc.PushTransform(new TranslateTransform(0, 10));
                dc.DrawDrawing(visual.Drawing);
                dc.Pop();
            }
            bitmap.Render(host);
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>A top-down 32-bit DIB section with premultiplied alpha, as the shell expects.</summary>
        private static IntPtr CreatePremultipliedBitmap(BitmapSource image)
        {
            int width = image.PixelWidth, height = image.PixelHeight, stride = width * 4;
            var pixels = new byte[stride * height];
            image.CopyPixels(pixels, stride, 0);

            var header = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
            };
            IntPtr hbitmap = CreateDIBSection(IntPtr.Zero, ref header, 0, out IntPtr bits, IntPtr.Zero, 0);
            if (hbitmap == IntPtr.Zero) throw new InvalidOperationException("CreateDIBSection failed.");
            Marshal.Copy(pixels, 0, bits, pixels.Length);
            return hbitmap;
        }
    }
}
