using System.Runtime.InteropServices;
using static Pouchy.Interop.NativeMethods;

namespace Pouchy.Interop
{
    /// <summary>Physical-pixel geometry and DPI scale of one monitor.</summary>
    internal readonly record struct MonitorInfo(RECT Bounds, RECT WorkArea, double Scale)
    {
        public static MonitorInfo FromPoint(int x, int y) =>
            FromHandle(MonitorFromPoint(new POINT { x = x, y = y }, MONITOR_DEFAULTTONEAREST));

        public static MonitorInfo FromWindow(IntPtr hwnd) =>
            FromHandle(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST));

        private static MonitorInfo FromHandle(IntPtr hMonitor)
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (hMonitor == IntPtr.Zero || !GetMonitorInfo(hMonitor, ref mi))
            {
                // Fall back to the whole virtual screen.
                var rect = new RECT
                {
                    Left = GetSystemMetrics(SM_XVIRTUALSCREEN),
                    Top = GetSystemMetrics(SM_YVIRTUALSCREEN),
                };
                rect.Right = rect.Left + GetSystemMetrics(SM_CXVIRTUALSCREEN);
                rect.Bottom = rect.Top + GetSystemMetrics(SM_CYVIRTUALSCREEN);
                return new MonitorInfo(rect, rect, 1.0);
            }

            double scale = 1.0;
            if (GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0)
            {
                scale = dpiX / 96.0;
            }
            return new MonitorInfo(mi.rcMonitor, mi.rcWork, scale);
        }
    }
}
