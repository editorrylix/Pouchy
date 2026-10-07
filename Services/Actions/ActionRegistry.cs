using System.Diagnostics;
using System.IO;
using Pouchy.Interop;
using Pouchy.Models;

namespace Pouchy.Services.Actions
{
    /// <summary>A built-in action (or group) that can be turned off in Settings.</summary>
    public sealed record ActionInfo(string Id, string Name, string Description);

    /// <summary>
    /// All actions: built-ins, "copy to" one of the recent destinations, and user scripts from the
    /// actions folder (read fresh each time, so new scripts show up without a restart).
    /// </summary>
    public sealed class ActionRegistry
    {
        public const string DestinationsId = "destinations";
        public const string ScriptsId = "scripts";

        private const int MaxPrint = 20;

        private readonly string _scriptsFolder;
        private readonly Func<IReadOnlyList<string>> _recentFolders;
        private readonly Action<string> _recordDestination;
        private readonly List<IPouchAction> _builtIns;

        public ActionRegistry(string scriptsFolder, Func<IReadOnlyList<string>> recentFolders, Action<string> recordDestination)
        {
            _scriptsFolder = scriptsFolder;
            _recentFolders = recentFolders;
            _recordDestination = recordDestination;
            _builtIns = CreateBuiltIns();
        }

        /// <summary>What Settings lists, in tile order.</summary>
        public static IReadOnlyList<ActionInfo> Catalog { get; } = new[]
        {
            new ActionInfo("zip", "Compress to ZIP", "Zips everything into one archive and adds it to the pouch."),
            new ActionInfo(DestinationsId, "Copy to a recent folder", "One tile for each of your two most recent destinations."),
            new ActionInfo("png", "Convert to PNG", "For images. The converted copies are added to the pouch."),
            new ActionInfo("jpg", "Convert to JPG", "For images. The converted copies are added to the pouch."),
            new ActionInfo("resize", "Resize to 50%", "For images. Smaller copies are added to the pouch."),
            new ActionInfo("ocr", "Extract text", "For one image. The text is copied and added as a note."),
            new ActionInfo("paths", "Copy paths", "Copies the full paths as text."),
            new ActionInfo("share", "Share", "Opens the Windows share sheet (Nearby Sharing, Mail, other apps)."),
            new ActionInfo("print", "Print", "Sends files to their default printer app."),
            new ActionInfo(ScriptsId, "Your scripts", "Scripts in the actions folder (PowerShell, batch, Python or .exe)."),
        };

        public string ScriptsFolder => _scriptsFolder;

        /// <summary>Every action that can run on these paths and isn't hidden, in tile order.</summary>
        public List<IPouchAction> For(IReadOnlyList<string> paths, ICollection<string> hidden)
        {
            var result = new List<IPouchAction>();
            if (paths.Count == 0) return result;

            foreach (var info in Catalog)
            {
                if (hidden.Contains(info.Id)) continue;
                switch (info.Id)
                {
                    case DestinationsId:
                        result.AddRange(DestinationActions(paths).Where(a => a.CanRun(paths)));
                        break;
                    case ScriptsId:
                        result.AddRange(ScriptAction.LoadAll(_scriptsFolder).Where(a => a.CanRun(paths)));
                        break;
                    default:
                        if (_builtIns.FirstOrDefault(a => a.Id == info.Id) is { } action && action.CanRun(paths)) result.Add(action);
                        break;
                }
            }
            return result;
        }

        private IEnumerable<IPouchAction> DestinationActions(IReadOnlyList<string> paths)
        {
            // Skip the folder the files already live in.
            var sourceFolders = paths.Select(p => Path.GetDirectoryName(p.TrimEnd('\\')) ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
            return _recentFolders()
                .Where(f => !sourceFolders.Contains(Path.TrimEndingDirectorySeparator(f)) && !sourceFolders.Contains(f))
                .Take(2)
                .Select(folder => (IPouchAction)new DelegateAction(
                    "copyto:" + folder,
                    $"Copy to {FolderName(folder)}",
                    FolderName(folder),
                    "FolderArrowRight24",
                    p => p.All(x => File.Exists(x) || Directory.Exists(x)),
                    ctx => Task.Run(() =>
                    {
                        FileActions.CopyTo(ctx.Paths, folder);
                        _recordDestination(folder);
                        return ActionResult.Done($"Copied {Helpers.Format.Plural(ctx.Paths.Count, "item")} to {FolderName(folder)}");
                    }))
                { ToolTip = $"Copy to {folder}" });
        }

        public static string FolderName(string folder)
        {
            string trimmed = Path.TrimEndingDirectorySeparator(folder);
            string name = Path.GetFileName(trimmed);
            return name.Length > 0 ? name : folder; // Drive roots.
        }

        private static List<IPouchAction> CreateBuiltIns()
        {
            static bool AllExist(IReadOnlyList<string> p) => p.All(x => File.Exists(x) || Directory.Exists(x));
            static bool AllImages(IReadOnlyList<string> p) => p.All(x => File.Exists(x) && PreviewSupport.IsImage(x));
            static bool AllFiles(IReadOnlyList<string> p) => p.All(File.Exists);

            return new List<IPouchAction>
            {
                new DelegateAction("zip", "Compress to ZIP", "Zip", "FolderZip24", AllExist,
                    ctx => Task.Run(() => new ActionResult(new[] { FileActions.CompressToZip(ctx.Paths) }))),

                new DelegateAction("png", "Convert to PNG", "PNG", "Image24",
                    p => AllImages(p) && p.Any(x => !Path.GetExtension(x).Equals(".png", StringComparison.OrdinalIgnoreCase)),
                    ctx => Task.Run(() => new ActionResult(ctx.Paths.Select(x => FileActions.ConvertImage(x, ImageFileFormat.Png)).ToList()))),

                new DelegateAction("jpg", "Convert to JPG", "JPG", "Image24",
                    p => AllImages(p) && p.Any(x => Path.GetExtension(x).ToLowerInvariant() is not (".jpg" or ".jpeg")),
                    ctx => Task.Run(() => new ActionResult(ctx.Paths.Select(x => FileActions.ConvertImage(x, ImageFileFormat.Jpeg)).ToList()))),

                new DelegateAction("resize", "Resize to 50%", "50%", "ResizeImage24", AllImages,
                    ctx => Task.Run(() => new ActionResult(ctx.Paths.Select(x => FileActions.ResizeImage(x, 0.5)).ToList()))),

                new DelegateAction("ocr", "Extract text", "Text", "ScanText24",
                    p => p.Count == 1 && AllImages(p),
                    async ctx =>
                    {
                        string? text = await OcrService.RecognizeFileAsync(ctx.Paths[0]);
                        if (text == null) throw new InvalidOperationException("Windows has no text recognition language installed. Add one in Settings → Time & language → Language & region.");
                        if (string.IsNullOrWhiteSpace(text)) return ActionResult.Done("No text found in the image");
                        return new ActionResult(Array.Empty<string>(), "Text copied and added as a note", text);
                    }, needsUiThread: true),

                new DelegateAction("paths", "Copy paths", "Paths", "DocumentCopy24", _ => true,
                    ctx =>
                    {
                        System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, ctx.Paths));
                        return Task.FromResult(ActionResult.Done(ctx.Paths.Count == 1 ? "Path copied" : $"{ctx.Paths.Count} paths copied"));
                    }, needsUiThread: true),

                new DelegateAction("share", "Share", "Share", "Share24", AllExist,
                    ctx =>
                    {
                        ShareSheet.Show(ctx.Owner, ctx.Paths, null, ctx.Paths.Count == 1 ? Path.GetFileName(ctx.Paths[0]) : $"{ctx.Paths.Count} items from Pouchy");
                        return Task.FromResult(ActionResult.Done());
                    }, needsUiThread: true),

                new DelegateAction("print", "Print", "Print", "Print24", p => p.Count <= MaxPrint && AllFiles(p),
                    ctx => Task.Run(() =>
                    {
                        foreach (var path in ctx.Paths)
                        {
                            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "print" })?.Dispose();
                        }
                        return ActionResult.Done($"Sent {Helpers.Format.Plural(ctx.Paths.Count, "file")} to print");
                    })),
            };
        }
    }
}
