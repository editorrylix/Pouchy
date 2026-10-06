using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pouchy.Services;
using Xunit;

namespace Pouchy.Tests
{
    /// <summary>Shell thumbnails come back as raw DIBs; make sure they aren't upside down.</summary>
    public class ThumbnailOrientationTests : IDisposable
    {
        private readonly TestFolder _folder = new();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void PhotoThumbnail_IsUpright()
        {
            // Red top half, blue bottom half.
            string path = Path.Combine(_folder.Path, "orientation.png");
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Red, null, new Rect(0, 0, 200, 100));
                dc.DrawRectangle(Brushes.Blue, null, new Rect(0, 100, 200, 100));
            }
            var bitmap = new RenderTargetBitmap(200, 200, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);

            using var provider = new ShellThumbnailProvider();
            var thumbnail = provider.GetThumbnail(path);

            Assert.NotNull(thumbnail);
            var image = (BitmapSource)thumbnail!.Image;
            var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            var top = new byte[4];
            var bottom = new byte[4];
            converted.CopyPixels(new Int32Rect(converted.PixelWidth / 2, 2, 1, 1), top, 4, 0);
            converted.CopyPixels(new Int32Rect(converted.PixelWidth / 2, converted.PixelHeight - 3, 1, 1), bottom, 4, 0);

            // BGRA: red has a high R (index 2), blue a high B (index 0).
            Assert.True(top[2] > 200 && top[0] < 60, $"Top pixel should be red, was B{top[0]} G{top[1]} R{top[2]}");
            Assert.True(bottom[0] > 200 && bottom[2] < 60, $"Bottom pixel should be blue, was B{bottom[0]} G{bottom[1]} R{bottom[2]}");
        }
    }
}
