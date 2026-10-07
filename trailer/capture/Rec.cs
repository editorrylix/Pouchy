// Input and window helpers for recording the trailer footage with the real app.
// Loaded by record.ps1 with Add-Type. Everything is in physical pixels (per-monitor DPI aware).
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public static class Rec
{
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, uint data, IntPtr extra);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
    [DllImport("user32.dll")] static extern short VkKeyScan(char c);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("shell32.dll")] static extern IntPtr SHAppBarMessage(uint message, ref APPBARDATA data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr l);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

    delegate bool EnumProc(IntPtr hwnd, IntPtr l);
    public struct POINT { public int X, Y; }
    public struct RECT { public int Left, Top, Right, Bottom; public int Width { get { return Right - Left; } } public int Height { get { return Bottom - Top; } } }
    [StructLayout(LayoutKind.Sequential)]
    struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam; }

    static readonly Random Jitter = new Random(7);

    public static void Init() { SetProcessDpiAwarenessContext(new IntPtr(-4)); }

    // ------------------------------------------------------------------ Mouse

    public static POINT Cursor() { POINT p; GetCursorPos(out p); return p; }

    static void Put(double x, double y)
    {
        int w = GetSystemMetrics(0), h = GetSystemMetrics(1);
        // MOUSEEVENTF_MOVE | ABSOLUTE: a real input event, so low-level hooks (Pouchy's gestures) see it.
        mouse_event(0x8001, (int)Math.Round(x * 65535.0 / (w - 1)), (int)Math.Round(y * 65535.0 / (h - 1)), 0, IntPtr.Zero);
    }

    /// <summary>A human-looking move: eased, slightly curved, about 120 updates a second.</summary>
    public static void Move(int x, int y, int ms, double arc = 0.08)
    {
        POINT p = Cursor();
        double dx = x - p.X, dy = y - p.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        // Perpendicular bow, alternating side for variety.
        double bow = len * arc * (Jitter.Next(2) == 0 ? 1 : -1);
        double nx = len > 0 ? -dy / len : 0, ny = len > 0 ? dx / len : 0;
        int steps = Math.Max(1, ms / 8);
        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            double e = t < 0.5 ? 16 * t * t * t * t * t : 1 - Math.Pow(-2 * t + 2, 5) / 2; // ease in-out quint
            double b = Math.Sin(Math.PI * e) * bow;
            Put(p.X + dx * e + nx * b, p.Y + dy * e + ny * b);
            Thread.Sleep(8);
        }
        Put(x, y);
    }

    /// <summary>Left and right strokes, like a real "shake" while dragging.</summary>
    public static void Shake(int strokes, int amplitude, int strokeMs)
    {
        POINT c = Cursor();
        for (int i = 0; i < strokes; i++)
        {
            int amp = (int)(amplitude * (0.75 + 0.25 * Math.Min(1.0, i / 2.0)));
            Move(c.X + (i % 2 == 0 ? amp : -amp), c.Y + (i % 2 == 0 ? -6 : 6), strokeMs, 0.0);
        }
        Move(c.X, c.Y, strokeMs, 0.0);
    }

    public static void Down() { mouse_event(0x0002, 0, 0, 0, IntPtr.Zero); }
    public static void Up() { mouse_event(0x0004, 0, 0, 0, IntPtr.Zero); }
    public static void Click() { Down(); Thread.Sleep(70); Up(); }
    public static void RightClick() { mouse_event(0x0008, 0, 0, 0, IntPtr.Zero); Thread.Sleep(70); mouse_event(0x0010, 0, 0, 0, IntPtr.Zero); }
    /// <summary>Mouse wheel, in notches; negative scrolls down.</summary>
    public static void Wheel(int notches) { mouse_event(0x0800, 0, 0, (uint)(notches * 120), IntPtr.Zero); }

    // ------------------------------------------------------------------ Keyboard

    public static void KeyDown(byte vk) { keybd_event(vk, 0, 0, IntPtr.Zero); }
    public static void KeyUp(byte vk) { keybd_event(vk, 0, 2, IntPtr.Zero); }
    public static void Key(byte vk) { KeyDown(vk); Thread.Sleep(40); KeyUp(vk); }

    /// <summary>Presses modifiers (VK codes) plus a key, e.g. Combo(0x11, 0x4B) for Ctrl+K.</summary>
    public static void Combo(byte[] modifiers, byte vk)
    {
        foreach (var m in modifiers) { KeyDown(m); Thread.Sleep(25); }
        Key(vk);
        for (int i = modifiers.Length - 1; i >= 0; i--) { KeyUp(modifiers[i]); Thread.Sleep(15); }
    }

    public static void Type(string text, int msPerChar)
    {
        foreach (char c in text)
        {
            short scan = VkKeyScan(c);
            byte vk = (byte)(scan & 0xFF);
            bool shift = (scan & 0x100) != 0;
            if (shift) KeyDown(0x10);
            Key(vk);
            if (shift) KeyUp(0x10);
            Thread.Sleep(msPerChar + Jitter.Next(-msPerChar / 3, msPerChar / 3 + 1));
        }
    }

    // ------------------------------------------------------------------ Windows

    public static RECT Rect(IntPtr hwnd) { RECT r; GetWindowRect(hwnd, out r); return r; }

    public static void Place(IntPtr hwnd, int x, int y, int w, int h)
    {
        ShowWindow(hwnd, 9); // SW_RESTORE
        SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, 0x0040); // SWP_SHOWWINDOW
    }

    public static void ToBottom(IntPtr hwnd) { SetWindowPos(hwnd, new IntPtr(1), 0, 0, 0, 0, 0x0013); }

    public static void Front(IntPtr hwnd)
    {
        // A background process may only take the foreground right after input. An F24 tap counts and,
        // unlike the usual Alt tap, doesn't make apps like Paint show their access-key badges.
        keybd_event(0x87, 0, 0, IntPtr.Zero); keybd_event(0x87, 0, 2, IntPtr.Zero);
        ShowWindow(hwnd, 9);
        if (SetForegroundWindow(hwnd) && GetForegroundWindow() == hwnd) return;
        // Otherwise borrow the foreground thread's input state for a moment.
        uint pid;
        uint fg = GetWindowThreadProcessId(GetForegroundWindow(), out pid), me = GetCurrentThreadId();
        AttachThreadInput(me, fg, true);
        SetForegroundWindow(hwnd);
        AttachThreadInput(me, fg, false);
    }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

    /// <summary>The largest visible window of a process whose title contains the text ("" = any).</summary>
    public static IntPtr Find(int pid, string title)
    {
        IntPtr best = IntPtr.Zero; int bestArea = 0;
        EnumWindows((h, l) =>
        {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p != pid || !IsWindowVisible(h)) return true;
            var sb = new StringBuilder(256); GetWindowText(h, sb, 256);
            if (title.Length > 0 && sb.ToString().IndexOf(title, StringComparison.OrdinalIgnoreCase) < 0) return true;
            RECT r; GetWindowRect(h, out r);
            int area = r.Width * r.Height;
            if (area > bestArea) { bestArea = area; best = h; }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    /// <summary>The largest visible top-level window with this exact title (any process).</summary>
    public static IntPtr FindTitle(string title)
    {
        IntPtr best = IntPtr.Zero; int bestArea = 0;
        EnumWindows((h, l) =>
        {
            if (!IsWindowVisible(h)) return true;
            var sb = new StringBuilder(256); GetWindowText(h, sb, 256);
            if (sb.ToString() != title) return true;
            RECT r; GetWindowRect(h, out r);
            int area = r.Width * r.Height;
            if (area > bestArea) { bestArea = area; best = h; }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    // ------------------------------------------------------------------ Taskbar

    public static bool TaskbarAutoHide()
    {
        var data = new APPBARDATA { cbSize = Marshal.SizeOf(typeof(APPBARDATA)), hWnd = FindWindow("Shell_TrayWnd", null) };
        return ((long)SHAppBarMessage(4, ref data) & 1) != 0; // ABM_GETSTATE, ABS_AUTOHIDE
    }

    public static void SetTaskbarAutoHide(bool hide)
    {
        var data = new APPBARDATA { cbSize = Marshal.SizeOf(typeof(APPBARDATA)), hWnd = FindWindow("Shell_TrayWnd", null), lParam = new IntPtr(hide ? 1 : 2) }; // ABS_AUTOHIDE / ABS_ALWAYSONTOP
        SHAppBarMessage(10, ref data); // ABM_SETSTATE
    }

    // ------------------------------------------------------------------ Stage checks

    [ComImport, Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IVirtualDesktopManager
    {
        [PreserveSig] int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out bool onCurrentDesktop);
        [PreserveSig] int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);
        [PreserveSig] int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }

    static IVirtualDesktopManager desktops;

    public static bool OnThisDesktop(IntPtr hwnd)
    {
        if (desktops == null) desktops = (IVirtualDesktopManager)Activator.CreateInstance(System.Type.GetTypeFromCLSID(new Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")));
        bool on;
        return desktops.IsWindowOnCurrentVirtualDesktop(hwnd, out on) == 0 && on;
    }

    static bool Cloaked(IntPtr hwnd)
    {
        int cloaked;
        return DwmGetWindowAttribute(hwnd, 14, out cloaked, 4) == 0 && cloaked != 0; // DWMWA_CLOAKED
    }

    /// <summary>
    /// Titles of visible windows on the current virtual desktop that are not part of the shoot.
    /// Recording refuses to start while any exist, so nothing private can end up in the footage.
    /// </summary>
    public static string[] ForeignWindows(string[] allowedTitles)
    {
        var found = new System.Collections.Generic.List<string>();
        EnumWindows((h, l) =>
        {
            if (!IsWindowVisible(h) || Cloaked(h)) return true;
            var sb = new StringBuilder(256); GetWindowText(h, sb, 256);
            string title = sb.ToString();
            var cls = new StringBuilder(128); GetClassName(h, cls, 128);
            string c = cls.ToString();
            if (c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd" || c == "Progman" || c == "WorkerW") return true;
            RECT r; GetWindowRect(h, out r);
            if (r.Width < 40 || r.Height < 40 || title.Length == 0) return true;
            if (!OnThisDesktop(h)) return true;
            foreach (var allowed in allowedTitles)
                if (title == allowed
                    || (allowed.EndsWith("*") && title.StartsWith(allowed.TrimEnd('*')))
                    || (allowed.StartsWith("*") && title.EndsWith(allowed.TrimStart('*')))) return true;
            found.Add(title + " [" + c + "]");
            return true;
        }, IntPtr.Zero);
        return found.ToArray();
    }

    /// <summary>The screen rectangle of the first child window with this class (e.g. a browser's page area).</summary>
    public static RECT ChildRect(IntPtr parent, string className)
    {
        RECT result = new RECT();
        EnumChildWindows(parent, (h, l) =>
        {
            var cls = new StringBuilder(128); GetClassName(h, cls, 128);
            if (cls.ToString() == className && IsWindowVisible(h)) { GetWindowRect(h, out result); return false; }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>The first visible top-level window of a class, e.g. "#32768" for a context menu.</summary>
    public static IntPtr FindClass(string className)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) =>
        {
            if (!IsWindowVisible(h)) return true;
            var cls = new StringBuilder(128); GetClassName(h, cls, 128);
            if (cls.ToString() == className) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern int GetMenuItemCount(IntPtr menu);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetMenuString(IntPtr menu, uint item, StringBuilder text, int max, uint flags);
    [DllImport("user32.dll")] static extern bool GetMenuItemRect(IntPtr hwnd, IntPtr menu, uint item, out RECT r);

    /// <summary>
    /// Where an item of an open classic context menu is, found through the menu itself (Win32), so it
    /// works even when UI Automation hasn't caught up with the menu yet. Empty rect if not open.
    /// </summary>
    public static RECT MenuItemRect(string text)
    {
        RECT result = new RECT();
        EnumWindows((h, l) =>
        {
            if (!IsWindowVisible(h)) return true;
            var cls = new StringBuilder(128); GetClassName(h, cls, 128);
            if (cls.ToString() != "#32768") return true;
            IntPtr menu = SendMessage(h, 0x01E1, IntPtr.Zero, IntPtr.Zero); // MN_GETHMENU
            if (menu == IntPtr.Zero) return true;
            int count = GetMenuItemCount(menu);
            for (uint i = 0; i < count; i++)
            {
                var s = new StringBuilder(256);
                GetMenuString(menu, i, s, 256, 0x400); // MF_BYPOSITION
                if (s.ToString().Replace("&", "") == text && GetMenuItemRect(h, menu, i, out result)) return false;
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>All visible top-level windows of a process (WPF menus are popups of their own).</summary>
    public static IntPtr[] FindPopups(int pid)
    {
        var list = new System.Collections.Generic.List<IntPtr>();
        EnumWindows((h, l) =>
        {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == pid && IsWindowVisible(h)) list.Add(h);
            return true;
        }, IntPtr.Zero);
        return list.ToArray();
    }
}
