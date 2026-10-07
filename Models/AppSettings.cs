using System.Windows.Input;

namespace Pouchy.Models
{
    /// <summary>How strictly gesture triggers require a real drag-and-drop operation.</summary>
    public enum DragDetectionMode
    {
        /// <summary>Any left-button drag can trigger (original behaviour).</summary>
        Off,

        /// <summary>Ignore window moves/resizes, menus and text selection.</summary>
        Balanced,

        /// <summary>Only trigger while an OLE drag-and-drop is in progress.</summary>
        Strict,
    }

    public enum PouchViewMode
    {
        Grid,
        List,
        Compact,
    }

    public enum TileSize
    {
        Small,
        Medium,
        Large,
    }

    public enum SpawnAnimation
    {
        Pop,
        Slide,
        Fade,
        None,
    }

    public enum DragOutAction
    {
        /// <summary>Dropping files somewhere copies them; the originals stay put (Shift moves).</summary>
        Copy,

        /// <summary>Dropping files somewhere moves them, like Explorer on the same drive (Ctrl copies).</summary>
        Move,
    }

    /// <summary>How long unpinned items stay in the pouch.</summary>
    public enum ItemExpiry
    {
        Never,
        OneHour,
        OneDay,
        OneWeek,
        OneMonth,
    }

    public enum SoundPack
    {
        Off,
        Soft,
        Bubbly,
        Clicky,
        /// <summary>WAV files from the sounds folder.</summary>
        Custom,
    }

    public enum DragModifier
    {
        Control,
        Shift,
        Alt,
    }

    public class HotkeySetting
    {
        public bool Enabled { get; set; } = true;
        public ModifierKeys Modifiers { get; set; } = ModifierKeys.Alt | ModifierKeys.Shift;
        public Key Key { get; set; } = Key.Z;

        public override string ToString()
        {
            var parts = new List<string>();
            if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(Key.ToString());
            return string.Join(" + ", parts);
        }
    }

    public class AppSettings
    {
        public int Version { get; set; } = 1;

        // Behaviour
        public bool ClearOnStartup { get; set; }
        /// <summary>Fetch page titles and icons for links dropped into the pouch.</summary>
        public bool FetchLinkPreviews { get; set; } = true;
        /// <summary>Temporarily ignore shake/edge/modifier gestures (the hotkey still works).</summary>
        public bool GesturesPaused { get; set; }
        /// <summary>Unpinned items older than this are removed.</summary>
        public ItemExpiry AutoClear { get; set; } = ItemExpiry.Never;
        /// <summary>Folders items were recently copied, moved or dropped into, newest first.</summary>
        public List<string> RecentDestinations { get; set; } = new();
        /// <summary>How many unpinned items a clipboard history shelf keeps.</summary>
        public int ClipboardHistoryLimit { get; set; } = 50;

        // Drop actions
        /// <summary>Show action tiles (Zip, Convert...) while files are dragged over the pouch.</summary>
        public bool ShowDropActions { get; set; } = true;
        /// <summary>Ids of actions the user turned off.</summary>
        public List<string> HiddenActions { get; set; } = new();

        // Sounds
        public SoundPack SoundPack { get; set; } = SoundPack.Off;
        /// <summary>0–1.</summary>
        public double SoundVolume { get; set; } = 0.6;

        // Appearance
        public string ThemeId { get; set; } = "midnight";
        public PouchViewMode ViewMode { get; set; } = PouchViewMode.Grid;
        public TileSize TileSize { get; set; } = TileSize.Medium;
        public int GridColumns { get; set; } = 3;
        public bool ShowItemNames { get; set; } = true;
        public bool ShowItemDetails { get; set; }
        /// <summary>Multiplier for the theme's background opacity (0.4–1).</summary>
        public double BackgroundOpacity { get; set; } = 1.0;
        public SpawnAnimation SpawnAnimation { get; set; } = SpawnAnimation.Pop;
        /// <summary>Animation speed multiplier (0.5 = half speed, 2 = twice as fast).</summary>
        public double AnimationSpeed { get; set; } = 1.0;
        public bool ReduceMotion { get; set; }
        /// <summary>Show Pouchy the mascot in the pouch.</summary>
        public bool ShowMascot { get; set; } = true;
        /// <summary>Shelf tabs other than the active one show only their icon.</summary>
        public bool CompactShelfTabs { get; set; } = true;

        // Dragging out
        public DragOutAction DragOutAction { get; set; } = DragOutAction.Copy;
        /// <summary>Remove items from the pouch once they've been dropped somewhere.</summary>
        public bool RemoveAfterDragOut { get; set; }

        // Updates
        /// <summary>Check GitHub for a newer release about once a day.</summary>
        public bool CheckForUpdates { get; set; } = true;
        public DateTime? LastUpdateCheck { get; set; }
        /// <summary>A version the user chose to skip notifications for.</summary>
        public string? DismissedUpdateVersion { get; set; }

        // Shake trigger
        public bool ShakeEnabled { get; set; } = true;
        /// <summary>Minimum horizontal travel (px) between direction changes.</summary>
        public int ShakeMinDistance { get; set; } = 20;
        /// <summary>Direction changes needed to count as a shake.</summary>
        public int ShakeReversals { get; set; } = 4;
        public int ShakeWindowMs { get; set; } = 1000;

        // Edge bump trigger
        public bool EdgeBumpEnabled { get; set; } = true;
        public int EdgeBumpCount { get; set; } = 2;
        public int EdgeBumpWindowMs { get; set; } = 1000;

        // Modifier-while-dragging trigger. Off by default: Ctrl/Shift/Alt+drag
        // are Explorer's own copy/move/link gestures.
        public bool ModifierDragEnabled { get; set; }
        public DragModifier ModifierDragKey { get; set; } = DragModifier.Control;

        public DragDetectionMode DragDetection { get; set; } = DragDetectionMode.Balanced;

        public HotkeySetting Hotkey { get; set; } = new();
        /// <summary>Opens Windows' screen snip; the picture lands in the pouch.</summary>
        public HotkeySetting ScreenshotHotkey { get; set; } = new() { Key = Key.S };
        /// <summary>The user picked their own hotkeys, so Pouchy must not swap in a free one.</summary>
        public bool HotkeyCustomized { get; set; }
        public bool ScreenshotHotkeyCustomized { get; set; }

        // Suppression
        public bool SuppressInFullscreen { get; set; } = true;
        public List<string> Blacklist { get; set; } = DefaultBlacklist();

        public static List<string> DefaultBlacklist() =>
            new() { "csgo.exe", "photoshop.exe", "illustrator.exe", "valkyrie.exe" };
    }
}
