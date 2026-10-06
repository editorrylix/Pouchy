using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Pouchy.Interop;
using Pouchy.Models;

namespace Pouchy.Services
{
    /// <summary>Registers the global "show pouch" hotkey on the UI thread's message queue.</summary>
    public sealed class HotkeyService : IDisposable
    {
        private const int HotkeyId = 9000;
        private bool _registered;

        /// <summary>Raised on the UI thread.</summary>
        public event EventHandler? Pressed;

        public HotkeyService()
        {
            ComponentDispatcher.ThreadPreprocessMessage += OnThreadMessage;
        }

        /// <summary>Replaces the current registration. Returns false if another app owns the combination.</summary>
        public bool Register(HotkeySetting hotkey)
        {
            Unregister();
            if (!hotkey.Enabled || hotkey.Key == Key.None) return true;

            uint modifiers = NativeMethods.MOD_NOREPEAT;
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= NativeMethods.MOD_ALT;
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= NativeMethods.MOD_CONTROL;
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= NativeMethods.MOD_SHIFT;
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= NativeMethods.MOD_WIN;

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(hotkey.Key);
            _registered = NativeMethods.RegisterHotKey(IntPtr.Zero, HotkeyId, modifiers, vk);
            Logger.Log(_registered
                ? $"Hotkey {hotkey} registered."
                : $"Hotkey {hotkey} could not be registered (error {Marshal.GetLastWin32Error()}).");
            return _registered;
        }

        public void Unregister()
        {
            if (!_registered) return;
            NativeMethods.UnregisterHotKey(IntPtr.Zero, HotkeyId);
            _registered = false;
        }

        public void Dispose()
        {
            Unregister();
            ComponentDispatcher.ThreadPreprocessMessage -= OnThreadMessage;
        }

        private void OnThreadMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message == NativeMethods.WM_HOTKEY && msg.wParam.ToInt32() == HotkeyId)
            {
                handled = true;
                Pressed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
