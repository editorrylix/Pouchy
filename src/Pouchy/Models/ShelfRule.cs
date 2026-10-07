using System.IO;

namespace Pouchy.Models
{
    /// <summary>What a smart shelf collects. New items that match go to that shelf instead of the open one.</summary>
    public enum ShelfRule
    {
        None,
        Images,
        Documents,
        Videos,
        Audio,
        Archives,
        Folders,
        Links,
        Text,
        Colors,
        /// <summary>Everything copied to the Windows clipboard (clipboard history).</summary>
        Clipboard,
    }

    public static class ShelfRules
    {
        private static readonly string[] Images = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".heic", ".avif", ".tif", ".tiff", ".svg", ".ico", ".raw", ".psd" };
        private static readonly string[] Documents = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp", ".rtf", ".txt", ".md", ".csv", ".epub", ".pages", ".key", ".numbers" };
        private static readonly string[] Videos = { ".mp4", ".mov", ".mkv", ".avi", ".webm", ".wmv", ".m4v", ".flv", ".mpg", ".mpeg" };
        private static readonly string[] Audio = { ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wma", ".aiff" };
        private static readonly string[] Archives = { ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".iso", ".cab" };

        /// <summary>Rules the user can pick for a shelf, in menu order.</summary>
        public static IReadOnlyList<ShelfRule> Choices { get; } = Enum.GetValues<ShelfRule>().Where(r => r != ShelfRule.None).ToList();

        public static string Describe(ShelfRule rule) => rule switch
        {
            ShelfRule.Images => "collects images and screenshots",
            ShelfRule.Documents => "collects documents",
            ShelfRule.Videos => "collects videos",
            ShelfRule.Audio => "collects audio",
            ShelfRule.Archives => "collects archives",
            ShelfRule.Folders => "collects folders",
            ShelfRule.Links => "collects links",
            ShelfRule.Text => "collects notes and text",
            ShelfRule.Colors => "collects colours",
            ShelfRule.Clipboard => "keeps everything you copy",
            _ => "",
        };

        public static string MenuName(ShelfRule rule) => rule switch
        {
            ShelfRule.Text => "Notes and text",
            ShelfRule.Colors => "Colours",
            ShelfRule.Clipboard => "Everything I copy (clipboard history)",
            _ => rule.ToString(),
        };

        /// <summary>Does a new item belong on a shelf with this rule? Clipboard shelves are filled separately.</summary>
        public static bool Matches(ShelfRule rule, PouchItem item) => rule switch
        {
            ShelfRule.Images => item.Kind == PouchItemKind.Image || FilesMatch(item, Images),
            ShelfRule.Documents => FilesMatch(item, Documents),
            ShelfRule.Videos => FilesMatch(item, Videos),
            ShelfRule.Audio => FilesMatch(item, Audio),
            ShelfRule.Archives => FilesMatch(item, Archives),
            ShelfRule.Folders => item.Kind == PouchItemKind.Folder,
            ShelfRule.Links => item.Kind == PouchItemKind.Link,
            ShelfRule.Text => item.Kind == PouchItemKind.Text,
            ShelfRule.Colors => item.Kind == PouchItemKind.Color,
            _ => false,
        };

        public static bool IsImageFile(string path) => Has(Images, path);

        /// <summary>A file, or a stack whose files all have one of the extensions.</summary>
        private static bool FilesMatch(PouchItem item, string[] extensions) => item.Kind switch
        {
            PouchItemKind.File => item.FilePath != null && Has(extensions, item.FilePath),
            PouchItemKind.Stack => item.StackFiles is { Count: > 0 } files && files.All(f => Has(extensions, f)),
            _ => false,
        };

        private static bool Has(string[] extensions, string path) =>
            extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }
}
