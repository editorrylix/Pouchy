using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Pouchy.Services;
using Xunit;

namespace Pouchy.Tests
{
    /// <summary>
    /// Checks against real Windows UI. They open windows on the screen, so they only run with
    /// POUCHY_INTERACTIVE=1 (never in CI).
    /// </summary>
    public class InteractiveTests
    {
        private static bool Enabled => Environment.GetEnvironmentVariable("POUCHY_INTERACTIVE") == "1";

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hwnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left, Top, Right, Bottom;
        }

        [Fact]
        public void DropTarget_FindsTheFolderOfAnExplorerWindow()
        {
            if (!Enabled) return;
            using var folder = new TestFolder();
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder.Path}\"") { UseShellExecute = true })?.Dispose();

            IntPtr hwnd = IntPtr.Zero;
            for (int i = 0; i < 50 && hwnd == IntPtr.Zero; i++)
            {
                Thread.Sleep(200);
                hwnd = FindExplorerWindow(folder.Path);
            }
            Assert.NotEqual(IntPtr.Zero, hwnd);

            try
            {
                SetForegroundWindow(hwnd);
                Thread.Sleep(500);
                GetWindowRect(hwnd, out var rect);
                var candidates = DropTargetLocator.CandidateFolders((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
                Assert.Contains(candidates, c => string.Equals(c, folder.Path, StringComparison.OrdinalIgnoreCase));

                // Only a folder that actually received the dropped file is remembered.
                string source = Path.Combine(Path.GetTempPath(), $"pouchy-drop-{Guid.NewGuid():N}.txt");
                File.WriteAllText(source, "x");
                Assert.Null(DropTargetLocator.Confirm(candidates, new[] { source }));
                File.Copy(source, Path.Combine(folder.Path, Path.GetFileName(source)));
                Assert.Equal(folder.Path, DropTargetLocator.Confirm(candidates, new[] { source }), StringComparer.OrdinalIgnoreCase);
                File.Delete(source);
            }
            finally
            {
                CloseExplorerWindow(hwnd);
            }
        }

        private static IntPtr FindExplorerWindow(string path)
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
            foreach (dynamic window in shell.Windows())
            {
                try
                {
                    if (string.Equals((string)window.Document.Folder.Self.Path, path, StringComparison.OrdinalIgnoreCase)) return new IntPtr((long)window.HWND);
                }
                catch (Exception)
                {
                    // Not a folder window.
                }
            }
            return IntPtr.Zero;
        }

        private static void CloseExplorerWindow(IntPtr hwnd)
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
            foreach (dynamic window in shell.Windows())
            {
                try
                {
                    if (new IntPtr((long)window.HWND) == hwnd) window.Quit();
                }
                catch (Exception)
                {
                    // Already closed.
                }
            }
        }
    }
}
