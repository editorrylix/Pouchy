namespace Pouchy.Services.Actions
{
    /// <summary>What an action produced: new files to add to the pouch, text to add as a note, or a message.</summary>
    public sealed record ActionResult(IReadOnlyList<string> NewFiles, string? Message = null, string? Text = null)
    {
        public static ActionResult Done(string? message = null) => new(Array.Empty<string>(), message);
    }

    /// <param name="Paths">The files and folders to work on.</param>
    /// <param name="Owner">Window handle for dialogs (the share sheet).</param>
    public sealed record ActionContext(IReadOnlyList<string> Paths, IntPtr Owner);

    /// <summary>
    /// Something Pouchy can do with files: shown as a tile while files are dragged over the pouch,
    /// in the item menu under "Actions" and in the command palette.
    /// </summary>
    public interface IPouchAction
    {
        /// <summary>Stable id, used to hide actions in Settings.</summary>
        string Id { get; }

        /// <summary>Menu text, e.g. "Compress to ZIP".</summary>
        string Name { get; }

        /// <summary>Tile label, e.g. "Zip".</summary>
        string ShortName { get; }

        /// <summary>A Fluent icon name, e.g. "FolderZip24".</summary>
        string Icon { get; }

        bool CanRun(IReadOnlyList<string> paths);

        /// <summary>Runs on a background thread unless the action needs the UI (see <see cref="NeedsUiThread"/>).</summary>
        Task<ActionResult> RunAsync(ActionContext context);

        /// <summary>True for actions that show Windows UI themselves, like the share sheet.</summary>
        bool NeedsUiThread => false;
    }

    /// <summary>An action defined by delegates; all built-in actions are these.</summary>
    public sealed class DelegateAction : IPouchAction
    {
        private readonly Func<IReadOnlyList<string>, bool> _canRun;
        private readonly Func<ActionContext, Task<ActionResult>> _run;

        public DelegateAction(string id, string name, string shortName, string icon,
            Func<IReadOnlyList<string>, bool> canRun, Func<ActionContext, Task<ActionResult>> run, bool needsUiThread = false)
        {
            Id = id;
            Name = name;
            ShortName = shortName;
            Icon = icon;
            _canRun = canRun;
            _run = run;
            NeedsUiThread = needsUiThread;
        }

        public string Id { get; }
        public string Name { get; }
        public string ShortName { get; }
        public string Icon { get; }
        public bool NeedsUiThread { get; }
        public string? ToolTip { get; init; }

        public bool CanRun(IReadOnlyList<string> paths) => paths.Count > 0 && _canRun(paths);

        public Task<ActionResult> RunAsync(ActionContext context) => _run(context);
    }
}
