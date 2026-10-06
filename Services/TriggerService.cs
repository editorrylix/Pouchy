using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Pouchy.Interop;
using Pouchy.Models;

namespace Pouchy.Services
{
    public enum TriggerSource
    {
        Shake,
        EdgeBump,
        ModifierDrag,
        Hotkey,
        Tray,
    }

    public sealed class TriggerEventArgs : EventArgs
    {
        public TriggerEventArgs(int x, int y, TriggerSource source)
        {
            X = x;
            Y = y;
            Source = source;
        }

        /// <summary>Cursor position in physical screen pixels.</summary>
        public int X { get; }
        public int Y { get; }
        public TriggerSource Source { get; }
    }

    /// <summary>
    /// Watches the mouse through a low-level hook and raises <see cref="Triggered"/>
    /// when a gesture is recognised and nothing suppresses it.
    /// <see cref="Triggered"/> is raised on a thread-pool thread.
    /// </summary>
    public sealed class TriggerService : IDisposable
    {
        // OLE's DoDragDrop captures the mouse with a hidden window of this class.
        private static readonly HashSet<string> OleDragCaptureClasses = new(StringComparer.Ordinal) { "CLIPBRDWNDCLASS" };
        private static readonly HashSet<string> DesktopClasses = new(StringComparer.Ordinal) { "Progman", "WorkerW" };

        private readonly SettingsService _settings;
        private readonly GestureDetector _detector;
        private readonly NativeMethods.LowLevelMouseProc _proc; // Held so the delegate isn't collected.
        private readonly uint _ownProcessId;
        private IntPtr _hookId = IntPtr.Zero;
        private bool _modifierTriggerFired;

        public event EventHandler<TriggerEventArgs>? Triggered;

        /// <summary>Extra suppression check, e.g. while Pouchy itself is dragging items out.</summary>
        public Func<bool>? IsSuppressed { get; set; }

        public TriggerService(SettingsService settings)
        {
            _settings = settings;
            _ownProcessId = (uint)Environment.ProcessId;
            _proc = HookCallback;
            _detector = new GestureDetector(() => _settings.Current, 0, 1);
            UpdateScreenBounds();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        public void Start()
        {
            if (_hookId != IntPtr.Zero) return;

            using var process = Process.GetCurrentProcess();
            using var module = process.MainModule!;
            _hookId = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_MOUSE_LL, _proc, NativeMethods.GetModuleHandle(module.ModuleName), 0);

            if (_hookId == IntPtr.Zero)
            {
                Logger.Log($"Failed to install mouse hook (error {Marshal.GetLastWin32Error()}).");
            }
        }

        public void Stop()
        {
            if (_hookId == IntPtr.Zero) return;
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        public void Dispose()
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            Stop();
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e) => UpdateScreenBounds();

        private void UpdateScreenBounds()
        {
            _detector.SetScreenBounds(
                NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN),
                NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN));
        }

        // Runs on the UI thread for every mouse event system-wide: keep it cheap.
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_MOUSEMOVE)
            {
                try
                {
                    var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    OnMouseMove(data.pt.x, data.pt.y);
                }
                catch (Exception ex)
                {
                    Logger.Log("Error in mouse hook: " + ex);
                }
            }
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void OnMouseMove(int x, int y)
        {
            if (!NativeMethods.IsKeyDown(NativeMethods.VK_LBUTTON))
            {
                _modifierTriggerFired = false;
                if (_detector.HasState) _detector.Reset();
                return;
            }

            var s = _settings.Current;
            if (s.ModifierDragEnabled && !_modifierTriggerFired && NativeMethods.IsKeyDown(ToVirtualKey(s.ModifierDragKey)))
            {
                _modifierTriggerFired = true;
                Queue(x, y, TriggerSource.ModifierDrag);
            }

            switch (_detector.Process(x, y, Environment.TickCount64))
            {
                case GestureKind.Shake:
                    Queue(x, y, TriggerSource.Shake);
                    break;
                case GestureKind.EdgeBump:
                    Queue(x, y, TriggerSource.EdgeBump);
                    break;
            }
        }

        private void Queue(int x, int y, TriggerSource source)
        {
            // Window/process inspection is too slow for the hook thread.
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (CanTrigger(source))
                    {
                        Logger.Log($"{source} detected at {x}, {y}.");
                        Triggered?.Invoke(this, new TriggerEventArgs(x, y, source));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log("Error evaluating trigger: " + ex);
                }
            });
        }

        private bool CanTrigger(TriggerSource source)
        {
            if (IsSuppressed?.Invoke() == true) return false;

            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return true;

            uint threadId = NativeMethods.GetWindowThreadProcessId(foreground, out uint processId);
            if (processId == _ownProcessId)
            {
                Logger.Log($"{source} suppressed: Pouchy is the active window.");
                return false;
            }

            var s = _settings.Current;
            if (s.SuppressInFullscreen && IsFullscreen(foreground, out string className))
            {
                Logger.Log($"{source} suppressed: fullscreen app ({className}).");
                return false;
            }

            if (GetProcessName(processId) is string processName &&
                s.Blacklist.Contains(processName, StringComparer.OrdinalIgnoreCase))
            {
                Logger.Log($"{source} suppressed: {processName} is blacklisted.");
                return false;
            }

            return PassesDragDetection(threadId, source, s.DragDetection);
        }

        /// <summary>Checks that the button-held movement looks like a real drag-and-drop.</summary>
        private static bool PassesDragDetection(uint threadId, TriggerSource source, DragDetectionMode mode)
        {
            if (mode == DragDetectionMode.Off) return true;

            var info = new NativeMethods.GUITHREADINFO { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
            if (!NativeMethods.GetGUIThreadInfo(threadId, ref info))
            {
                return mode != DragDetectionMode.Strict;
            }

            if ((info.flags & (NativeMethods.GUI_INMOVESIZE | NativeMethods.GUI_INMENUMODE)) != 0)
            {
                Logger.Log($"{source} suppressed: window move/resize or menu in progress.");
                return false;
            }

            string captureClass = NativeMethods.GetClassName(info.hwndCapture);
            bool isOleDrag = OleDragCaptureClasses.Contains(captureClass);
            if (isOleDrag) return true;

            if (mode == DragDetectionMode.Strict)
            {
                Logger.Log($"{source} suppressed (strict): no drag-and-drop in progress (capture: '{captureClass}').");
                return false;
            }

            // Selecting text: the control showing the caret is also capturing the mouse.
            if (info.hwndCapture != IntPtr.Zero && info.hwndCapture == info.hwndCaret)
            {
                Logger.Log($"{source} suppressed: looks like text selection ({captureClass}).");
                return false;
            }

            return true;
        }

        private static bool IsFullscreen(IntPtr hwnd, out string className)
        {
            className = "";
            if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return false;

            var monitor = MonitorInfo.FromWindow(hwnd).Bounds;
            bool coversMonitor = rect.Left == monitor.Left && rect.Top == monitor.Top &&
                                 rect.Right == monitor.Right && rect.Bottom == monitor.Bottom;
            if (!coversMonitor) return false;

            className = NativeMethods.GetClassName(hwnd);
            return !DesktopClasses.Contains(className);
        }

        private static string? GetProcessName(uint processId)
        {
            IntPtr process = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (process == IntPtr.Zero) return null;
            try
            {
                uint size = 1024;
                var sb = new StringBuilder((int)size);
                return NativeMethods.QueryFullProcessImageName(process, 0, sb, ref size)
                    ? Path.GetFileName(sb.ToString())
                    : null;
            }
            finally
            {
                NativeMethods.CloseHandle(process);
            }
        }

        private static int ToVirtualKey(DragModifier modifier) => modifier switch
        {
            DragModifier.Shift => NativeMethods.VK_SHIFT,
            DragModifier.Alt => NativeMethods.VK_MENU,
            _ => NativeMethods.VK_CONTROL,
        };
    }
}
