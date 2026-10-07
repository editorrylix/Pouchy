using System.IO;
using System.Runtime.InteropServices;

namespace Pouchy.Interop
{
    /// <summary>Item ID list (PIDL) helpers shared by the Explorer integrations.</summary>
    internal static class ShellSelection
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHParseDisplayName(string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

        [DllImport("shell32.dll")]
        private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, uint dwFlags);

        /// <summary>Opens the items' folder in Explorer with all of them selected.</summary>
        /// <returns>False if the items aren't in a single folder or the shell call failed.</returns>
        public static bool OpenFolderAndSelect(IReadOnlyList<string> paths)
        {
            var folders = paths.Select(p => Path.GetDirectoryName(p.TrimEnd('\\'))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (folders.Count != 1 || folders[0] == null) return false;

            var pidls = new List<IntPtr>();
            IntPtr folderPidl = IntPtr.Zero;
            try
            {
                if (SHParseDisplayName(folders[0]!, IntPtr.Zero, out folderPidl, 0, out _) != 0) return false;
                foreach (var path in paths)
                {
                    if (SHParseDisplayName(path, IntPtr.Zero, out var pidl, 0, out _) == 0) pidls.Add(pidl);
                }
                if (pidls.Count == 0) return false;
                return SHOpenFolderAndSelectItems(folderPidl, (uint)pidls.Count, pidls.ToArray(), 0) == 0;
            }
            finally
            {
                if (folderPidl != IntPtr.Zero) Marshal.FreeCoTaskMem(folderPidl);
                foreach (var pidl in pidls) Marshal.FreeCoTaskMem(pidl);
            }
        }
    }
}
