using System.IO;
using System.Runtime.InteropServices;
using Pouchy.Interop;

namespace Pouchy.Services
{
    /// <summary>
    /// Works out which folder items were just dropped into, so it can be offered again as a
    /// recent destination. Knows the desktop and Explorer windows; anything else is ignored.
    /// </summary>
    public static class DropTargetLocator
    {
        /// <summary>The folder under a screen point, if it's the desktop or an Explorer window.</summary>
        public static IReadOnlyList<string> CandidateFolders(int x, int y)
        {
            try
            {
                IntPtr hwnd = NativeMethods.WindowFromPoint(new NativeMethods.POINT { x = x, y = y });
                IntPtr root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
                if (root == IntPtr.Zero) return Array.Empty<string>();

                string cls = NativeMethods.GetClassName(root);
                if (cls is "Progman" or "WorkerW")
                {
                    return new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) };
                }
                return cls == "CabinetWClass" ? ExplorerFolders(root) : Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Logger.Log("Could not find the drop target: " + ex.Message);
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// The candidate folder that now contains one of the dropped items. Explorer tabs share
        /// one window, so the files themselves decide which tab it was.
        /// </summary>
        public static string? Confirm(IReadOnlyList<string> candidates, IReadOnlyList<string> droppedPaths)
        {
            var names = droppedPaths.Select(p => Path.GetFileName(p.TrimEnd('\\'))).Where(n => n.Length > 0).ToList();
            return candidates.FirstOrDefault(folder =>
                !droppedPaths.Any(p => string.Equals(Path.GetDirectoryName(p.TrimEnd('\\')), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) &&
                names.Any(n => File.Exists(Path.Combine(folder, n)) || Directory.Exists(Path.Combine(folder, n))));
        }

        /// <summary>Folders shown in the Explorer window with this handle (one per tab).</summary>
        private static List<string> ExplorerFolders(IntPtr root)
        {
            var result = new List<string>();
            var type = Type.GetTypeFromProgID("Shell.Application");
            if (type == null) return result;

            object shell = Activator.CreateInstance(type)!;
            try
            {
                dynamic windows = ((dynamic)shell).Windows();
                int count = windows.Count;
                for (int i = 0; i < count; i++)
                {
                    dynamic? window = windows.Item(i);
                    if (window == null) continue;
                    try
                    {
                        if (new IntPtr((long)window.HWND) != root) continue;
                        string? path = window.Document?.Folder?.Self?.Path;
                        if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) result.Add(path);
                    }
                    catch (Exception)
                    {
                        // Internet Explorer windows and special folders don't have these properties.
                    }
                    finally
                    {
                        Marshal.FinalReleaseComObject(window);
                    }
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
            return result;
        }
    }
}
