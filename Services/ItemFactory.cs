using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pouchy.Helpers;
using Pouchy.Models;

namespace Pouchy.Services
{
    /// <summary>
    /// Builds <see cref="PouchItem"/>s from dropped data or saved state.
    /// File system methods are safe to call off the UI thread.
    /// </summary>
    public sealed class ItemFactory
    {
        private const int MaxFolderEntriesCounted = 10_000;
        private const int TextNameLength = 25;
        private const int StackPreviewCount = 3;

        private readonly IThumbnailProvider _thumbnails;

        public ItemFactory(IThumbnailProvider thumbnails)
        {
            _thumbnails = thumbnails;
        }

        public PouchItem CreateFromPath(string path, Guid? id = null, DateTime? addedAt = null)
        {
            var thumbnail = _thumbnails.GetThumbnail(path);

            if (Directory.Exists(path))
            {
                return new PouchItem
                {
                    Id = id ?? Guid.NewGuid(),
                    AddedAt = addedAt ?? DateTime.Now,
                    Kind = PouchItemKind.Folder,
                    FilePath = path,
                    DisplayName = GetName(path),
                    Metadata = $"Folder · {DescribeFolderContents(path)}",
                    Icon = thumbnail?.Image,
                    ThumbnailStyle = thumbnail?.Style ?? ThumbnailStyle.Icon,
                };
            }

            string ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
            var item = new PouchItem
            {
                Id = id ?? Guid.NewGuid(),
                AddedAt = addedAt ?? DateTime.Now,
                Kind = PouchItemKind.File,
                FilePath = path,
                FileExtension = ext,
                DisplayName = Path.GetFileNameWithoutExtension(path) is { Length: > 0 } name ? name : GetName(path),
                Icon = thumbnail?.Image,
                ThumbnailStyle = thumbnail?.Style ?? ThumbnailStyle.Icon,
            };
            Refresh(item);
            return item;
        }

        public PouchItem CreateStack(IReadOnlyList<string> paths, Guid? id = null, DateTime? addedAt = null)
        {
            var item = new PouchItem
            {
                Id = id ?? Guid.NewGuid(),
                AddedAt = addedAt ?? DateTime.Now,
                Kind = PouchItemKind.Stack,
                StackFiles = paths.ToList(),
                FileExtension = "STACK",
                DisplayName = $"Stack of {Format.Plural(paths.Count, "item")}",
                Icon = _thumbnails.GetStackIcon(),
                StackPreviews = CreateStackCards(paths),
            };
            Refresh(item);
            return item;
        }

        public PouchItem CreateText(string text, Guid? id = null, DateTime? addedAt = null)
        {
            string singleLine = text.ReplaceLineEndings(" ").Trim();
            return new PouchItem
            {
                Id = id ?? Guid.NewGuid(),
                AddedAt = addedAt ?? DateTime.Now,
                Kind = PouchItemKind.Text,
                TextContent = text,
                FileExtension = "TXT",
                DisplayName = singleLine.Length > TextNameLength ? singleLine[..TextNameLength] + "..." : singleLine,
                Metadata = $"Text Snippet · {text.Length} chars",
            };
        }

        public PouchItem CreateImage(BitmapSource image, Guid? id = null, DateTime? addedAt = null)
        {
            var frozen = EnsureFrozen(image);
            return new PouchItem
            {
                Id = id ?? Guid.NewGuid(),
                AddedAt = addedAt ?? DateTime.Now,
                Kind = PouchItemKind.Image,
                ImageContent = frozen,
                FileExtension = "IMG",
                DisplayName = "Image",
                Metadata = $"Image · {frozen.PixelWidth}×{frozen.PixelHeight}",
                Icon = frozen,
                ThumbnailStyle = ThumbnailStyle.Fill,
            };
        }

        /// <summary>Recreates an item from saved state. Returns null if it can't be restored.</summary>
        public PouchItem? Restore(PersistedItem saved, BitmapSource? image)
        {
            var kind = saved.Kind ?? InferKind(saved);
            var id = saved.Id == Guid.Empty ? Guid.NewGuid() : saved.Id;
            PouchItem? item = kind switch
            {
                PouchItemKind.File or PouchItemKind.Folder when !string.IsNullOrEmpty(saved.FilePath)
                    => CreateFromPath(saved.FilePath, id, saved.AddedAt),
                PouchItemKind.Stack when saved.StackFiles is { Count: > 0 }
                    => CreateStack(saved.StackFiles, id, saved.AddedAt),
                PouchItemKind.Text when saved.TextContent != null
                    => CreateText(saved.TextContent, id, saved.AddedAt),
                PouchItemKind.Image when image != null
                    => CreateImage(image, id, saved.AddedAt),
                _ => null,
            };

            if (item != null && !string.IsNullOrEmpty(saved.DisplayName) && kind != PouchItemKind.Stack)
            {
                item.DisplayName = saved.DisplayName;
            }
            return item;
        }

        /// <summary>Re-checks whether the item's files still exist and updates its metadata.</summary>
        public void Refresh(PouchItem item)
        {
            switch (item.Kind)
            {
                case PouchItemKind.File:
                    var info = new FileInfo(item.FilePath!);
                    if (info.Exists)
                    {
                        item.IsMissing = false;
                        item.Metadata = $"{(item.FileExtension.Length > 0 ? item.FileExtension : "File")} · {Format.Size(info.Length)}";
                    }
                    else if (Directory.Exists(item.FilePath))
                    {
                        item.IsMissing = false;
                    }
                    else
                    {
                        item.IsMissing = true;
                        item.Metadata = "Missing · moved or deleted";
                    }
                    break;

                case PouchItemKind.Folder:
                    item.IsMissing = !Directory.Exists(item.FilePath);
                    if (item.IsMissing) item.Metadata = "Missing · moved or deleted";
                    break;

                case PouchItemKind.Stack:
                    var files = item.StackFiles ?? Array.Empty<string>();
                    long total = 0;
                    int present = 0;
                    foreach (var path in files)
                    {
                        if (File.Exists(path))
                        {
                            present++;
                            total += SafeLength(path);
                        }
                        else if (Directory.Exists(path))
                        {
                            present++;
                        }
                    }
                    item.IsMissing = present == 0;
                    int missing = files.Count - present;
                    item.Metadata = $"{Format.Plural(files.Count, "item")} · {Format.Size(total)}"
                        + (missing > 0 ? $" · {missing} missing" : "");
                    break;
            }
        }

        /// <summary>Fans the first few files out like a pile of cards, first file on top.</summary>
        private IReadOnlyList<StackCard> CreateStackCards(IReadOnlyList<string> paths)
        {
            var images = paths
                .Take(StackPreviewCount)
                .Select(p => _thumbnails.GetThumbnail(p)?.Image)
                .OfType<ImageSource>()
                .ToList();

            // (angle, x, y) per card, bottom to top.
            (double, double, double)[] layout = images.Count switch
            {
                1 => new[] { (0.0, 0.0, 0.0) },
                2 => new[] { (-10.0, -7.0, 2.0), (6.0, 4.0, -1.0) },
                _ => new[] { (-15.0, -11.0, 4.0), (12.0, 11.0, 2.0), (0.0, 0.0, -3.0) },
            };

            var cards = new List<StackCard>();
            for (int i = 0; i < images.Count; i++)
            {
                var (angle, x, y) = layout[i];
                cards.Add(new StackCard(images[images.Count - 1 - i], angle, x, y));
            }
            return cards;
        }

        private static PouchItemKind? InferKind(PersistedItem saved)
        {
            // State files written before item kinds existed.
            if (saved.StackFiles is { Count: > 0 }) return PouchItemKind.Stack;
            if (!string.IsNullOrEmpty(saved.FilePath))
                return Directory.Exists(saved.FilePath) ? PouchItemKind.Folder : PouchItemKind.File;
            if (saved.TextContent != null) return PouchItemKind.Text;
            if (saved.ImageFile != null) return PouchItemKind.Image;
            return null;
        }

        private static string GetName(string path)
        {
            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(trimmed);
            return name.Length > 0 ? name : path; // Drive roots like "C:\"
        }

        private static string DescribeFolderContents(string path)
        {
            try
            {
                int count = Directory.EnumerateFileSystemEntries(path).Take(MaxFolderEntriesCounted + 1).Count();
                return count > MaxFolderEntriesCounted
                    ? $"{MaxFolderEntriesCounted:N0}+ items"
                    : Format.Plural(count, "item");
            }
            catch (Exception)
            {
                return "contents unavailable";
            }
        }

        private static long SafeLength(string path)
        {
            try
            {
                return new FileInfo(path).Length;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static BitmapSource EnsureFrozen(BitmapSource image)
        {
            if (image.IsFrozen) return image;

            // Copy the pixels so the item doesn't depend on the drag source's memory.
            var copy = new WriteableBitmap(image);
            copy.Freeze();
            return copy;
        }
    }
}
