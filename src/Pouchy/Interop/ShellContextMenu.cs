using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Pouchy.Services;

namespace Pouchy.Interop
{
    /// <summary>
    /// Shows Explorer's own right-click menu for files (Windows 11's "Show more options"),
    /// including third-party entries like 7-Zip, Send to and Open with.
    /// </summary>
    internal static class ShellContextMenu
    {
        [ComImport]
        [Guid("000214E6-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellFolder
        {
            void ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, out uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
            void EnumObjects(IntPtr hwnd, int grfFlags, out IntPtr ppenumIDList);
            void BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
            void BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
            [PreserveSig] int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
            void CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);
            void GetAttributesOf(uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref uint rgfInOut);
            void GetUIObjectOf(IntPtr hwndOwner, uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);
            void GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);
            void SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
        }

        [ComImport]
        [Guid("000214E4-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IContextMenu
        {
            [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint iMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
            [PreserveSig] int GetCommandString(UIntPtr idcmd, uint uflags, IntPtr reserved, IntPtr commandstring, uint cch);
        }

        [ComImport]
        [Guid("000214F4-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IContextMenu2
        {
            [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint iMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
            [PreserveSig] int GetCommandString(UIntPtr idcmd, uint uflags, IntPtr reserved, IntPtr commandstring, uint cch);
            [PreserveSig] int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
        }

        [ComImport]
        [Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IContextMenu3
        {
            [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint iMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
            [PreserveSig] int GetCommandString(UIntPtr idcmd, uint uflags, IntPtr reserved, IntPtr commandstring, uint cch);
            [PreserveSig] int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
            [PreserveSig] int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CMINVOKECOMMANDINFOEX
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            public IntPtr lpVerb;
            [MarshalAs(UnmanagedType.LPStr)] public string? lpParameters;
            [MarshalAs(UnmanagedType.LPStr)] public string? lpDirectory;
            public int nShow;
            public uint dwHotKey;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.LPStr)] public string? lpTitle;
            public IntPtr lpVerbW;
            [MarshalAs(UnmanagedType.LPWStr)] public string? lpParametersW;
            [MarshalAs(UnmanagedType.LPWStr)] public string? lpDirectoryW;
            [MarshalAs(UnmanagedType.LPWStr)] public string? lpTitleW;
            public NativeMethods.POINT ptInvoke;
        }

        private const uint CMF_NORMAL = 0x0;
        private const uint CMF_EXTENDEDVERBS = 0x100;
        private const uint CMIC_MASK_UNICODE = 0x4000;
        private const uint CMIC_MASK_PTINVOKE = 0x20000000;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const int SW_SHOWNORMAL = 1;
        private const int WM_INITMENUPOPUP = 0x0117;
        private const int WM_DRAWITEM = 0x002B;
        private const int WM_MEASUREITEM = 0x002C;
        private const int WM_MENUCHAR = 0x0120;
        private const uint FirstCommandId = 1;
        private const uint LastCommandId = 0x7FFF;

        [DllImport("shell32.dll")]
        private static extern int SHBindToParent(IntPtr pidl, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv, out IntPtr ppidlLast);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>True if the shell menu can be shown for these paths (they exist and share a folder).</summary>
        public static bool CanShow(IReadOnlyList<string> paths) =>
            paths.Count > 0 &&
            paths.All(p => File.Exists(p) || Directory.Exists(p)) &&
            paths.Select(p => Path.GetDirectoryName(p.TrimEnd('\\'))).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;

        /// <summary>Shows the menu at a physical-pixel screen point and runs the chosen command.</summary>
        /// <returns>True if a command was run.</returns>
        public static bool Show(IntPtr hwnd, IReadOnlyList<string> paths, int x, int y, bool extended)
        {
            if (!CanShow(paths)) return false;

            var absolutePidls = new List<IntPtr>();
            IntPtr parentPtr = IntPtr.Zero;
            IntPtr menuPtr = IntPtr.Zero;
            IntPtr hMenu = IntPtr.Zero;
            object? menu = null;
            HwndSource? source = null;
            HwndSourceHook? hook = null;

            try
            {
                var childPidls = new List<IntPtr>();
                foreach (var path in paths)
                {
                    if (ShellSelection.SHParseDisplayName(path, IntPtr.Zero, out var pidl, 0, out _) != 0) return false;
                    absolutePidls.Add(pidl);

                    // The child ID points into the absolute PIDL, so it's freed with it.
                    IntPtr parent;
                    if (SHBindToParent(pidl, typeof(IShellFolder).GUID, out parent, out var child) != 0) return false;
                    if (parentPtr == IntPtr.Zero) parentPtr = parent;
                    else Marshal.Release(parent);
                    childPidls.Add(child);
                }

                var folder = (IShellFolder)Marshal.GetObjectForIUnknown(parentPtr);
                var iid = typeof(IContextMenu).GUID;
                folder.GetUIObjectOf(hwnd, (uint)childPidls.Count, childPidls.ToArray(), ref iid, IntPtr.Zero, out menuPtr);
                menu = Marshal.GetObjectForIUnknown(menuPtr);
                var contextMenu = (IContextMenu)menu;

                hMenu = CreatePopupMenu();
                uint flags = CMF_NORMAL | (extended ? CMF_EXTENDEDVERBS : 0);
                if (contextMenu.QueryContextMenu(hMenu, 0, FirstCommandId, LastCommandId, flags) < 0) return false;

                // Submenus like "Send to" and "Open with" are drawn by the handler itself.
                var menu2 = menu as IContextMenu2;
                var menu3 = menu as IContextMenu3;
                source = HwndSource.FromHwnd(hwnd);
                hook = (IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                {
                    if (msg is WM_INITMENUPOPUP or WM_DRAWITEM or WM_MEASUREITEM or WM_MENUCHAR)
                    {
                        if (menu3 != null && menu3.HandleMenuMsg2((uint)msg, wParam, lParam, out var result) == 0)
                        {
                            handled = true;
                            return result;
                        }
                        if (menu2 != null && menu2.HandleMenuMsg((uint)msg, wParam, lParam) == 0)
                        {
                            handled = true;
                            return IntPtr.Zero;
                        }
                    }
                    return IntPtr.Zero;
                };
                source?.AddHook(hook);

                SetForegroundWindow(hwnd);
                uint command = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_RIGHTBUTTON, x, y, hwnd, IntPtr.Zero);
                if (command < FirstCommandId) return false;

                var invoke = new CMINVOKECOMMANDINFOEX
                {
                    cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                    fMask = CMIC_MASK_UNICODE | CMIC_MASK_PTINVOKE,
                    hwnd = hwnd,
                    lpVerb = (IntPtr)(command - FirstCommandId),
                    lpVerbW = (IntPtr)(command - FirstCommandId),
                    nShow = SW_SHOWNORMAL,
                    ptInvoke = new NativeMethods.POINT { x = x, y = y },
                };
                int hr = contextMenu.InvokeCommand(ref invoke);
                if (hr < 0) Logger.Log($"Shell menu command {command} failed (0x{hr:X8}).");
                return hr >= 0;
            }
            catch (Exception ex)
            {
                Logger.Log("Shell context menu failed: " + ex.Message);
                return false;
            }
            finally
            {
                if (source != null && hook != null) source.RemoveHook(hook);
                if (hMenu != IntPtr.Zero) DestroyMenu(hMenu);
                if (menu != null) Marshal.ReleaseComObject(menu);
                if (menuPtr != IntPtr.Zero) Marshal.Release(menuPtr);
                if (parentPtr != IntPtr.Zero) Marshal.Release(parentPtr);
                foreach (var pidl in absolutePidls) Marshal.FreeCoTaskMem(pidl);
            }
        }
    }
}
