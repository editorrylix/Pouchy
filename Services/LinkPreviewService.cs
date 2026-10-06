using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace Pouchy.Services
{
    public sealed record LinkPreview(string? Title, BitmapSource? Icon);

    /// <summary>Fetches a web page's title and icon for link items.</summary>
    public sealed class LinkPreviewService : IDisposable
    {
        private const int MaxPageBytes = 512 * 1024;
        private const int MaxIconBytes = 512 * 1024;

        private readonly HttpClient _http;

        public LinkPreviewService()
        {
            _http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
            {
                Timeout = TimeSpan.FromSeconds(8),
            };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Pouchy/1.0");
        }

        /// <returns>The preview, or null if the page couldn't be reached.</returns>
        public async Task<LinkPreview?> FetchAsync(string url)
        {
            try
            {
                var uri = new Uri(url);
                string html = await ReadLimitedStringAsync(uri);
                var (title, iconUrl) = LinkPreviewParser.Parse(html, uri);
                var icon = await TryDownloadIconAsync(iconUrl) ?? await TryDownloadIconAsync(new Uri(uri, "/favicon.ico"));
                return new LinkPreview(title, icon);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException or IOException)
            {
                Logger.Log($"Link preview failed for {url}: {ex.Message}");
                return null;
            }
        }

        public void Dispose() => _http.Dispose();

        private async Task<string> ReadLimitedStringAsync(Uri uri)
        {
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentType?.MediaType is string type && !type.Contains("html")) return "";

            await using var stream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[MaxPageBytes];
            int total = 0, read;
            while (total < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(total))) > 0) total += read;
            return System.Text.Encoding.UTF8.GetString(buffer, 0, total);
        }

        private async Task<BitmapSource?> TryDownloadIconAsync(Uri? iconUri)
        {
            if (iconUri == null) return null;
            try
            {
                using var response = await _http.GetAsync(iconUri);
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxIconBytes) return null;
                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                return DecodeLargestFrame(bytes);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException or FileFormatException or InvalidOperationException or ArgumentException)
            {
                return null; // SVG icons and broken files land here; the tile falls back to a globe.
            }
        }

        private static BitmapSource? DecodeLargestFrame(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).FirstOrDefault();
            if (frame == null) return null;
            frame.Freeze();
            return frame;
        }
    }

    /// <summary>Pulls a title and icon URL out of HTML. Pure, so it's unit-tested.</summary>
    public static partial class LinkPreviewParser
    {
        [GeneratedRegex(@"<meta\s[^>]*(?:property|name)\s*=\s*[""']og:title[""'][^>]*>", RegexOptions.IgnoreCase)]
        private static partial Regex OgTitleTag();

        [GeneratedRegex(@"<title[^>]*>(?<t>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
        private static partial Regex TitleTag();

        [GeneratedRegex(@"<link\s[^>]*rel\s*=\s*[""'](?<rel>[^""']*icon[^""']*)[""'][^>]*>", RegexOptions.IgnoreCase)]
        private static partial Regex IconTag();

        [GeneratedRegex(@"content\s*=\s*[""'](?<v>[^""']*)[""']", RegexOptions.IgnoreCase)]
        private static partial Regex ContentAttribute();

        [GeneratedRegex(@"href\s*=\s*[""'](?<v>[^""']*)[""']", RegexOptions.IgnoreCase)]
        private static partial Regex HrefAttribute();

        [GeneratedRegex(@"\s+")]
        private static partial Regex Whitespace();

        public static (string? Title, Uri? IconUrl) Parse(string html, Uri pageUri)
        {
            string? title = null;
            if (OgTitleTag().Match(html) is { Success: true } og && ContentAttribute().Match(og.Value) is { Success: true } content)
            {
                title = content.Groups["v"].Value;
            }
            else if (TitleTag().Match(html) is { Success: true } tag)
            {
                title = tag.Groups["t"].Value;
            }
            title = title == null ? null : Whitespace().Replace(WebUtility.HtmlDecode(title), " ").Trim();
            if (string.IsNullOrEmpty(title)) title = null;

            // Prefer the large apple-touch-icon, then any other icon link.
            Uri? icon = null;
            foreach (Match link in IconTag().Matches(html))
            {
                if (HrefAttribute().Match(link.Value) is not { Success: true } href) continue;
                if (!Uri.TryCreate(pageUri, WebUtility.HtmlDecode(href.Groups["v"].Value), out var candidate)) continue;
                if (candidate.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) continue;

                bool isTouchIcon = link.Groups["rel"].Value.Contains("apple-touch-icon", StringComparison.OrdinalIgnoreCase);
                if (icon == null || isTouchIcon) icon = candidate;
                if (isTouchIcon) break;
            }
            return (title, icon);
        }

        /// <summary>"https://www.example.com/a" → "example.com".</summary>
        public static string HostName(string url) =>
            Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
                ? (uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host)
                : url;
    }
}
