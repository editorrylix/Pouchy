using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Pouchy.Models
{
    public enum PouchItemKind
    {
        File,
        Folder,
        Stack,
        Text,
        Image,
    }

    /// <summary>How a thumbnail should be drawn on a tile.</summary>
    public enum ThumbnailStyle
    {
        /// <summary>Photo or video frame: fill the tile edge to edge.</summary>
        Fill,

        /// <summary>Document page preview: fit inside the tile with padding.</summary>
        Fit,

        /// <summary>Shell icon: draw small and centred.</summary>
        Icon,
    }

    /// <summary>One card in a stack's fanned preview.</summary>
    public sealed record StackCard(ImageSource Image, double Angle, double OffsetX, double OffsetY);

    public partial class PouchItem : ObservableObject
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public PouchItemKind Kind { get; init; }
        public DateTime AddedAt { get; init; } = DateTime.Now;

        /// <summary>Set for <see cref="PouchItemKind.File"/> and <see cref="PouchItemKind.Folder"/>.</summary>
        public string? FilePath { get; init; }

        /// <summary>Set for <see cref="PouchItemKind.Stack"/>.</summary>
        public IReadOnlyList<string>? StackFiles { get; init; }

        /// <summary>Set for <see cref="PouchItemKind.Text"/>.</summary>
        public string? TextContent { get; init; }

        /// <summary>Set for <see cref="PouchItemKind.Image"/>. Always frozen.</summary>
        public BitmapSource? ImageContent { get; init; }

        /// <summary>Upper-case extension badge, e.g. "PNG", "STACK", "TXT".</summary>
        public string FileExtension { get; init; } = "";

        /// <summary>Thumbnails of the first few stack files, in drawing order (last is on top).</summary>
        public IReadOnlyList<StackCard> StackPreviews { get; init; } = Array.Empty<StackCard>();

        [ObservableProperty]
        private string _displayName = "";

        [ObservableProperty]
        private string _metadata = "";

        /// <summary>Thumbnail or icon. Null for text snippets.</summary>
        [ObservableProperty]
        private ImageSource? _icon;

        [ObservableProperty]
        private ThumbnailStyle _thumbnailStyle = ThumbnailStyle.Icon;

        /// <summary>True when the file(s) this item points to no longer exist.</summary>
        [ObservableProperty]
        private bool _isMissing;

        public bool IsStack => Kind == PouchItemKind.Stack;
        public bool HasFileExtension => FileExtension.Length > 0;
        public int StackCount => StackFiles?.Count ?? 0;

        /// <summary>Start of the text, for the tile preview.</summary>
        public string PreviewText => TextContent is { Length: > 400 } text ? text[..400] : TextContent ?? "";

        /// <summary>Every file system path this item represents.</summary>
        public IEnumerable<string> FilePaths => Kind switch
        {
            PouchItemKind.Stack => StackFiles ?? (IEnumerable<string>)Array.Empty<string>(),
            PouchItemKind.File or PouchItemKind.Folder when FilePath != null => new[] { FilePath },
            _ => Array.Empty<string>(),
        };

        public bool IsQuickLookSupported => Kind switch
        {
            PouchItemKind.Stack or PouchItemKind.Folder or PouchItemKind.Text or PouchItemKind.Image => true,
            PouchItemKind.File when FilePath != null => PreviewSupport.IsPreviewable(FilePath),
            _ => false,
        };
    }

    public static class PreviewSupport
    {
        public static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };
        public static readonly string[] TextExtensions = { ".txt", ".md", ".csv", ".json", ".xml", ".cs", ".log" };
        public static readonly string[] VideoExtensions = { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm" };

        public static bool IsImage(string path) => Has(ImageExtensions, path);
        public static bool IsText(string path) => Has(TextExtensions, path);
        public static bool IsVideo(string path) => Has(VideoExtensions, path);
        public static bool IsPreviewable(string path) => IsImage(path) || IsText(path) || IsVideo(path);

        private static bool Has(string[] list, string path) =>
            list.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }
}
