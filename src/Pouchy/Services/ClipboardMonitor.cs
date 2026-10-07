using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Pouchy.Interop;

namespace Pouchy.Services
{
    /// <summary>
    /// Tells Pouchy when something new is copied, for clipboard history and Screenshot → Pouchy.
    /// It only listens while one of those needs it, ignores Pouchy's own copies, and skips
    /// anything a password manager marks as private.
    /// </summary>
    public sealed class ClipboardMonitor : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _settle;
        private HwndSource? _window;

        /// <summary>Raised on the UI thread once the clipboard has settled after a change.</summary>
        public event EventHandler? Changed;

        public ClipboardMonitor(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            // Apps often write several formats in a row; wait for them to finish.
            _settle = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, OnSettled, dispatcher);
            _settle.Stop();
        }

        public bool IsListening => _window != null;

        public void SetListening(bool listen)
        {
            if (listen == IsListening) return;
            if (listen) Start();
            else Stop();
        }

        private void Start()
        {
            // A message-only window: invisible, never in Alt+Tab.
            var parameters = new HwndSourceParameters("Pouchy clipboard") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0, WindowStyle = 0 };
            _window = new HwndSource(parameters);
            _window.AddHook(WndProc);
            if (!NativeMethods.AddClipboardFormatListener(_window.Handle))
            {
                Logger.Log("Could not listen to the clipboard.");
                Stop();
                return;
            }
            Logger.Log("Listening to the clipboard.");
        }

        private void Stop()
        {
            _settle.Stop();
            if (_window == null) return;
            NativeMethods.RemoveClipboardFormatListener(_window.Handle);
            _window.RemoveHook(WndProc);
            _window.Dispose();
            _window = null;
            Logger.Log("Stopped listening to the clipboard.");
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
            {
                _settle.Stop();
                _settle.Start();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnSettled(object? sender, EventArgs e)
        {
            _settle.Stop();
            if (IsFromPouchy() || IsPrivate()) return;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private static bool IsFromPouchy()
        {
            IntPtr owner = NativeMethods.GetClipboardOwner();
            if (owner == IntPtr.Zero) return false;
            NativeMethods.GetWindowThreadProcessId(owner, out uint pid);
            return pid == (uint)Environment.ProcessId;
        }

        /// <summary>The formats password managers and other apps use to keep copies out of clipboard history.</summary>
        internal static bool IsPrivate()
        {
            if (Has("ExcludeClipboardContentFromMonitorProcessing") || Has("Clipboard Viewer Ignore")) return true;
            try
            {
                // "CanIncludeInClipboardHistory" = 0 means don't keep it.
                if (Has("CanIncludeInClipboardHistory") &&
                    Clipboard.GetData("CanIncludeInClipboardHistory") is MemoryStream { Length: >= 4 } stream)
                {
                    var bytes = stream.ToArray();
                    return BitConverter.ToInt32(bytes, 0) == 0;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Could not read clipboard history flag: " + ex.Message);
                return true; // When unsure, don't keep it.
            }
            return false;

            static bool Has(string format) => NativeMethods.IsClipboardFormatAvailable(NativeMethods.RegisterClipboardFormat(format));
        }

        public void Dispose()
        {
            if (_dispatcher.CheckAccess()) Stop();
            else _dispatcher.Invoke(Stop);
        }
    }
}
