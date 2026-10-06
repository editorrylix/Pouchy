using System.IO;
using System.Windows;
using Pouchy.Models;

namespace Pouchy.Helpers
{
    /// <summary>Builds the <see cref="DataObject"/> used for both drag-out and copy.</summary>
    public static class DataObjectBuilder
    {
        /// <returns>The data object, or null if none of the items has anything to transfer.</returns>
        public static DataObject? Build(IReadOnlyCollection<PouchItem> items)
        {
            var paths = items
                .SelectMany(i => i.FilePaths)
                .Where(p => File.Exists(p) || Directory.Exists(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            string text = string.Join(Environment.NewLine,
                items.Where(i => i.Kind == PouchItemKind.Text && !string.IsNullOrEmpty(i.TextContent))
                     .Select(i => i.TextContent));

            // A single bitmap only makes sense when it's the only thing being transferred.
            var image = items.Count == 1 ? items.First().ImageContent : null;

            if (paths.Length == 0 && text.Length == 0 && image == null) return null;

            var data = new DataObject();
            if (paths.Length > 0) data.SetData(DataFormats.FileDrop, paths);
            if (text.Length > 0) data.SetData(DataFormats.UnicodeText, text);
            if (image != null) data.SetImage(image);
            return data;
        }
    }
}
