using System.Runtime.InteropServices;
using ComIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace Pouchy.Interop
{
    /// <summary>
    /// Creates the same data object Explorer uses when you drag files. Unlike WPF's DataObject
    /// it accepts the extra formats the shell's drag image helper stores.
    /// </summary>
    internal static class ShellDataObject
    {
        private static readonly Guid IidDataObject = new("0000010E-0000-0000-C000-000000000046");

        [DllImport("shell32.dll")]
        private static extern int SHCreateDataObject(
            IntPtr pidlFolder,
            uint cidl,
            [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
            ComIDataObject? pdtInner,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out ComIDataObject ppv);

        [DllImport("shell32.dll")]
        private static extern IntPtr ILFindLastID(IntPtr pidl);

        /// <returns>
        /// The data object, or null if the paths aren't all in one folder or can't be resolved
        /// (callers fall back to a plain WPF data object).
        /// </returns>
        public static ComIDataObject? Create(IReadOnlyList<string> paths)
        {
            if (paths.Count == 0) return null;
            var folders = paths.Select(p => System.IO.Path.GetDirectoryName(p.TrimEnd('\\')))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (folders.Count != 1 || folders[0] == null) return null;

            var pidls = new List<IntPtr>();
            IntPtr folderPidl = IntPtr.Zero;
            try
            {
                if (ShellSelection.SHParseDisplayName(folders[0]!, IntPtr.Zero, out folderPidl, 0, out _) != 0) return null;
                foreach (var path in paths)
                {
                    if (ShellSelection.SHParseDisplayName(path, IntPtr.Zero, out var pidl, 0, out _) != 0) return null;
                    pidls.Add(pidl);
                }

                // Like Explorer: the folder, plus each item's ID relative to it.
                var children = pidls.Select(ILFindLastID).ToArray();
                var iid = IidDataObject;
                int hr = SHCreateDataObject(folderPidl, (uint)children.Length, children, null, ref iid, out var data);
                return hr == 0 ? data : null;
            }
            catch (Exception ex)
            {
                Services.Logger.Log("Could not create shell data object: " + ex.Message);
                return null;
            }
            finally
            {
                // The data object keeps its own copies.
                if (folderPidl != IntPtr.Zero) Marshal.FreeCoTaskMem(folderPidl);
                foreach (var pidl in pidls) Marshal.FreeCoTaskMem(pidl);
            }
        }
    }
}
