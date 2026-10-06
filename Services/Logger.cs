using System.Diagnostics;
using System.IO;

namespace Pouchy.Services
{
    public static class Logger
    {
        private const long MaxLogBytes = 1024 * 1024;
        private static readonly object Gate = new();

        public static void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            Debug.WriteLine(line);

            try
            {
                lock (Gate)
                {
                    string logFile = AppPaths.LogFile;
                    Directory.CreateDirectory(AppPaths.DataFolder);

                    // Keep one previous log around instead of growing forever.
                    var info = new FileInfo(logFile);
                    if (info.Exists && info.Length > MaxLogBytes)
                    {
                        File.Move(logFile, Path.ChangeExtension(logFile, ".old.log"), overwrite: true);
                    }

                    File.AppendAllText(logFile, line + Environment.NewLine);
                }
            }
            catch
            {
                // Logging must never take the app down.
            }
        }
    }
}
