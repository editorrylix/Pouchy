using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Pouchy.Interop;

namespace Pouchy.Services
{
    public enum ImageFileFormat
    {
        Png,
        Jpeg,
    }

    /// <summary>
    /// File system operations behind the item context menu. Methods that create files
    /// never overwrite: they pick "name (2).ext" style names instead.
    /// </summary>
    public static class FileActions
    {
        public static void Open(string path) =>
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

        /// <summary>Shows Windows' "Open with" app picker.</summary>
        public static void OpenWith(string path) =>
            Process.Start(new ProcessStartInfo("rundll32.exe", $"shell32.dll,OpenAs_RunDLL {path}") { UseShellExecute = false });

        /// <summary>Opens Explorer with the items selected (all items must be in one folder to select them all).</summary>
        public static void ShowInExplorer(IReadOnlyList<string> paths)
        {
            if (paths.Count == 0) return;
            if (!ShellSelection.OpenFolderAndSelect(paths))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{paths[0]}\"") { UseShellExecute = true });
            }
        }

        /// <summary>Returns <paramref name="path"/>, or "name (2).ext", "name (3).ext"... if it exists.</summary>
        public static string UniquePath(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return path;

            string folder = Path.GetDirectoryName(path) ?? "";
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            for (int i = 2; ; i++)
            {
                string candidate = Path.Combine(folder, $"{name} ({i}){ext}");
                if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
            }
        }

        /// <exception cref="ArgumentException">The name is empty or contains characters Windows doesn't allow.</exception>
        public static void ValidateFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("The name can't be empty.");
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("A name can't contain any of these characters: \\ / : * ? \" < > |");
            if (name.Trim() is "." or "..") throw new ArgumentException("That name isn't allowed.");
        }

        /// <returns>The new path.</returns>
        public static string Rename(string path, string newName)
        {
            newName = newName.Trim();
            ValidateFileName(newName);

            string target = Path.Combine(Path.GetDirectoryName(path)!, newName);
            if (string.Equals(target, path, StringComparison.Ordinal)) return path;

            bool caseOnlyChange = string.Equals(target, path, StringComparison.OrdinalIgnoreCase);
            if (!caseOnlyChange && (File.Exists(target) || Directory.Exists(target)))
                throw new IOException($"\"{newName}\" already exists in this folder.");

            if (Directory.Exists(path))
            {
                if (caseOnlyChange)
                {
                    // Directory.Move refuses case-only renames; go through a temporary name.
                    string temp = path + ".pouchy-rename";
                    Directory.Move(path, temp);
                    Directory.Move(temp, target);
                }
                else
                {
                    Directory.Move(path, target);
                }
            }
            else
            {
                File.Move(path, target);
            }
            return target;
        }

        /// <returns>The new paths, in the same order.</returns>
        public static List<string> MoveTo(IReadOnlyList<string> paths, string folder) =>
            paths.Select(p =>
            {
                string target = UniquePath(Path.Combine(folder, Path.GetFileName(p.TrimEnd('\\'))));
                if (Directory.Exists(p)) Directory.Move(p, target);
                else File.Move(p, target);
                return target;
            }).ToList();

        /// <returns>The new paths, in the same order.</returns>
        public static List<string> CopyTo(IReadOnlyList<string> paths, string folder) =>
            paths.Select(p =>
            {
                string target = UniquePath(Path.Combine(folder, Path.GetFileName(p.TrimEnd('\\'))));
                if (Directory.Exists(p)) CopyDirectory(p, target);
                else File.Copy(p, target);
                return target;
            }).ToList();

        /// <summary>Zips files and folders into one archive next to the first item.</summary>
        /// <returns>The zip file's path.</returns>
        public static string CompressToZip(IReadOnlyList<string> paths)
        {
            if (paths.Count == 0) throw new ArgumentException("Nothing to compress.");

            string first = paths[0].TrimEnd('\\');
            string folder = Path.GetDirectoryName(first)!;
            string baseName = paths.Count == 1 ? Path.GetFileNameWithoutExtension(first) : "Archive";
            string zipPath = UniquePath(Path.Combine(folder, baseName + ".zip"));

            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in paths)
            {
                string path = raw.TrimEnd('\\');
                string entryRoot = UniqueEntryName(Path.GetFileName(path), usedNames);

                if (Directory.Exists(path))
                {
                    var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).ToList();
                    if (files.Count == 0) zip.CreateEntry(entryRoot + "/");
                    foreach (var file in files)
                    {
                        string relative = Path.GetRelativePath(path, file).Replace('\\', '/');
                        zip.CreateEntryFromFile(file, $"{entryRoot}/{relative}", CompressionLevel.Optimal);
                    }
                }
                else if (File.Exists(path))
                {
                    zip.CreateEntryFromFile(path, entryRoot, CompressionLevel.Optimal);
                }
            }
            return zipPath;
        }

        /// <summary>Extracts a zip into a new folder next to it.</summary>
        /// <returns>The folder's path.</returns>
        public static string ExtractZip(string zipPath)
        {
            string target = UniquePath(Path.Combine(Path.GetDirectoryName(zipPath)!, Path.GetFileNameWithoutExtension(zipPath)));
            ZipFile.ExtractToDirectory(zipPath, target);
            return target;
        }

        public static bool IsZip(string path) => Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase);

        /// <summary>Saves a converted copy next to the original.</summary>
        /// <returns>The new file's path.</returns>
        public static string ConvertImage(string path, ImageFileFormat format)
        {
            var image = LoadImage(path);
            string ext = format == ImageFileFormat.Png ? ".png" : ".jpg";
            string target = UniquePath(Path.ChangeExtension(path, ext));
            SaveImage(image, target, format);
            return target;
        }

        /// <summary>Saves a scaled copy next to the original, e.g. "photo (50%).jpg".</summary>
        public static string ResizeImage(string path, double scale)
        {
            var image = LoadImage(path);
            var scaled = new TransformedBitmap(image, new System.Windows.Media.ScaleTransform(scale, scale));
            scaled.Freeze();

            string folder = Path.GetDirectoryName(path)!;
            string name = $"{Path.GetFileNameWithoutExtension(path)} ({scale:P0})";
            var format = Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" ? ImageFileFormat.Jpeg : ImageFileFormat.Png;
            string target = UniquePath(Path.Combine(folder, name + (format == ImageFileFormat.Jpeg ? ".jpg" : ".png")));
            SaveImage(scaled, target, format);
            return target;
        }

        public static void SaveImage(BitmapSource image, string path, ImageFileFormat format)
        {
            BitmapEncoder encoder = format == ImageFileFormat.Jpeg
                ? new JpegBitmapEncoder { QualityLevel = 92 }
                : new PngBitmapEncoder();

            // JPEG has no alpha channel; flatten onto white instead of black.
            BitmapSource frame = format == ImageFileFormat.Jpeg ? FlattenOnWhite(image) : image;
            encoder.Frames.Add(BitmapFrame.Create(frame));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }

        public static BitmapSource LoadImage(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        public static void SetWallpaper(string imagePath)
        {
            const uint SPI_SETDESKWALLPAPER = 0x14;
            const uint SPIF_UPDATEINIFILE_SENDCHANGE = 0x3;
            if (!NativeMethods.SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, SPIF_UPDATEINIFILE_SENDCHANGE))
            {
                throw new IOException($"Windows refused to set the wallpaper (error {Marshal.GetLastWin32Error()}).");
            }
        }

        /// <summary>Sends files and folders to the Recycle Bin.</summary>
        /// <returns>False if the user cancelled or something failed.</returns>
        public static bool DeleteToRecycleBin(IReadOnlyList<string> paths, IntPtr owner)
        {
            if (paths.Count == 0) return true;
            var op = new NativeMethods.SHFILEOPSTRUCT
            {
                hwnd = owner,
                wFunc = NativeMethods.FO_DELETE,
                // Double-null-terminated list of paths.
                pFrom = string.Join('\0', paths) + "\0\0",
                fFlags = NativeMethods.FOF_ALLOWUNDO | NativeMethods.FOF_NOCONFIRMATION | NativeMethods.FOF_WANTNUKEWARNING,
            };
            return NativeMethods.SHFileOperation(ref op) == 0 && !op.fAnyOperationsAborted;
        }

        /// <summary>Reads a text file for "Copy contents", capped so huge files don't freeze the app.</summary>
        public static string ReadText(string path, int maxChars = 1_000_000)
        {
            using var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
            var buffer = new char[maxChars];
            int read = reader.ReadBlock(buffer, 0, maxChars);
            return new string(buffer, 0, read);
        }

        private static string UniqueEntryName(string name, HashSet<string> used)
        {
            string candidate = name;
            for (int i = 2; !used.Add(candidate); i++)
            {
                candidate = $"{Path.GetFileNameWithoutExtension(name)} ({i}){Path.GetExtension(name)}";
            }
            return candidate;
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, dir)));
            }
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
            }
        }

        private static BitmapSource FlattenOnWhite(BitmapSource image)
        {
            var visual = new System.Windows.Media.DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var rect = new System.Windows.Rect(0, 0, image.PixelWidth, image.PixelHeight);
                dc.DrawRectangle(System.Windows.Media.Brushes.White, null, rect);
                dc.DrawImage(image, rect);
            }
            var flat = new RenderTargetBitmap(image.PixelWidth, image.PixelHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            flat.Render(visual);
            flat.Freeze();
            return flat;
        }
    }

    /// <summary>Small text helpers for snippet actions.</summary>
    public static class TextTools
    {
        public static bool IsUrl(string text) =>
            Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
            !text.Trim().Contains(' ');

        public static string ToTitleCase(string text) =>
            System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower());

        /// <summary>Trims every line and collapses runs of blank lines.</summary>
        public static string TidyWhitespace(string text)
        {
            var lines = text.ReplaceLineEndings("\n").Split('\n').Select(l => l.Trim());
            var result = new List<string>();
            foreach (var line in lines)
            {
                if (line.Length == 0 && (result.Count == 0 || result[^1].Length == 0)) continue;
                result.Add(line);
            }
            while (result.Count > 0 && result[^1].Length == 0) result.RemoveAt(result.Count - 1);
            return string.Join(Environment.NewLine, result);
        }
    }
}
