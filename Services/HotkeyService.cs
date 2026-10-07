using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Pouchy.Interop;
using Pouchy.Models;

namespace Pouchy.Services
{
    public enum HotkeyAction
    {
        TogglePouch,
        Screenshot,
    }

    /// <summary>Registers Pouchy's global hotkeys on the UI thread's message queue.</summary>
    public sealed class HotkeyService : IDisposable
    {
        private const int FirstId = 9000;
        private readonly HashSet<HotkeyAction> _registered = new();

        /// <summary>The show/hide hotkey was pressed. Raised on the UI thread.</summary>
        public event EventHandler? Pressed;

        /// <summary>Any hotkey was pressed. Raised on the UI thread.</summary>
        public event EventHandler<HotkeyAction>? Triggered;

        public HotkeyService()
        {
            ComponentDispatcher.ThreadPreprocessMessage += OnThreadMessage;
        }

        /// <summary>Replaces the registration for an action. Returns false if another app owns the combination.</summary>
        public bool Register(HotkeySetting hotkey, HotkeyAction action = HotkeyAction.TogglePouch)
        {
            Unregister(action);
            if (!hotkey.Enabled || hotkey.Key == Key.None) return true;

            uint modifiers = NativeMethods.MOD_NOREPEAT;
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= NativeMethods.MOD_ALT;
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= NativeMethods.MOD_CONTROL;
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= NativeMethods.MOD_SHIFT;
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= NativeMethods.MOD_WIN;

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(hotkey.Key);
            bool ok = NativeMethods.RegisterHotKey(IntPtr.Zero, FirstId + (int)action, modifiers, vk);
            if (ok) _registered.Add(action);
            Logger.Log(ok
                ? $"Hotkey {hotkey} registered for {action}."
                : $"Hotkey {hotkey} for {action} could not be registered (error {Marshal.GetLastWin32Error()}).");
            return ok;
        }

        /// <summary>
        /// Registers the hotkey, or if another app already uses it, the first free one of
        /// <paramref name="alternatives"/>.
        /// </summary>
        /// <returns>The hotkey that is now registered, or null if none was free.</returns>
        public HotkeySetting? RegisterOrFallback(HotkeySetting hotkey, HotkeyAction action, IEnumerable<HotkeySetting> alternatives)
        {
            if (Register(hotkey, action)) return hotkey;
            foreach (var candidate in alternatives)
            {
                if (Register(candidate, action)) return candidate;
            }
            return null;
        }

        public static IReadOnlyList<HotkeySetting> Alternatives(HotkeyAction action) => action switch
        {
            HotkeyAction.Screenshot => new[]
            {
                new HotkeySetting { Modifiers = ModifierKeys.Alt | ModifierKeys.Shift, Key = Key.X },
                new HotkeySetting { Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Key = Key.S },
                new HotkeySetting { Modifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Key = Key.S },
            },
            _ => new[]
            {
                new HotkeySetting { Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Key = Key.Z },
                new HotkeySetting { Modifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Key = Key.Z },
            },
        };

        public void Unregister(HotkeyAction action = HotkeyAction.TogglePouch)
        {
            if (!_registered.Remove(action)) return;
            NativeMethods.UnregisterHotKey(IntPtr.Zero, FirstId + (int)action);
        }

        public void Dispose()
        {
            foreach (var action in _registered.ToList()) Unregister(action);
            ComponentDispatcher.ThreadPreprocessMessage -= OnThreadMessage;
        }

        private void OnThreadMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message != NativeMethods.WM_HOTKEY) return;
            int id = msg.wParam.ToInt32() - FirstId;
            if (!Enum.IsDefined(typeof(HotkeyAction), id)) return;

            handled = true;
            var action = (HotkeyAction)id;
            if (action == HotkeyAction.TogglePouch) Pressed?.Invoke(this, EventArgs.Empty);
            Triggered?.Invoke(this, action);
        }
    }
}
