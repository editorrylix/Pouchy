using System.IO;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;
using WinBitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;

namespace Pouchy.Services
{
    /// <summary>Text recognition with Windows' built-in OCR engine (no download needed).</summary>
    public static class OcrService
    {
        /// <returns>The recognised text, or null if no OCR language is installed.</returns>
        public static async Task<string?> RecognizeFileAsync(string path)
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            return await RecognizeAsync(stream);
        }

        /// <returns>The recognised text, or null if no OCR language is installed.</returns>
        public static async Task<string?> RecognizeImageAsync(BitmapSource image)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
            using var memory = new MemoryStream();
            encoder.Save(memory);
            memory.Position = 0;
            using var stream = memory.AsRandomAccessStream();
            return await RecognizeAsync(stream);
        }

        private static async Task<string?> RecognizeAsync(IRandomAccessStream stream)
        {
            var engine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (engine == null) return null;

            var decoder = await WinBitmapDecoder.CreateAsync(stream);
            using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            var result = await engine.RecognizeAsync(bitmap);
            return string.Join(Environment.NewLine, result.Lines.Select(l => l.Text));
        }
    }
}
