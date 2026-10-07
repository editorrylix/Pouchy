using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Pouchy.Services.Actions
{
    /// <summary>
    /// A user script in the actions folder. Pouchy runs it with the file paths as arguments;
    /// any existing file path the script prints on its own line is added to the pouch.
    ///
    /// Optional header lines anywhere in the first 30 lines (any comment style):
    /// <c>Pouchy-Name: Upload to server</c>, <c>Pouchy-Extensions: .png .jpg</c>, <c>Pouchy-Icon: CloudArrowUp24</c>.
    /// </summary>
    public sealed partial class ScriptAction : IPouchAction
    {
        public static readonly string[] SupportedExtensions = { ".ps1", ".bat", ".cmd", ".py", ".exe" };
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

        private readonly string[] _extensions;

        private ScriptAction(string path, string name, string icon, string[] extensions)
        {
            ScriptPath = path;
            Id = "script:" + Path.GetFileName(path).ToLowerInvariant();
            Name = name;
            Icon = icon;
            _extensions = extensions;
        }

        public string ScriptPath { get; }
        public string Id { get; }
        public string Name { get; }
        public string ShortName => Name;
        public string Icon { get; }

        public bool CanRun(IReadOnlyList<string> paths) =>
            paths.Count > 0 && (_extensions.Length == 0 || paths.All(p => _extensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase)));

        /// <summary>The scripts in a folder, sorted by name. Missing folder: none.</summary>
        public static List<ScriptAction> LoadAll(string folder)
        {
            var result = new List<ScriptAction>();
            if (!Directory.Exists(folder)) return result;
            foreach (var file in Directory.EnumerateFiles(folder).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                if (!SupportedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) continue;
                try
                {
                    result.Add(Load(file));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Logger.Log($"Skipping action {Path.GetFileName(file)}: {ex.Message}");
                }
            }
            return result;
        }

        public static ScriptAction Load(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            string icon = "Code24";
            string[] extensions = Array.Empty<string>();

            if (!Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string line in File.ReadLines(path).Take(30))
                {
                    var match = HeaderRegex().Match(line);
                    if (!match.Success) continue;
                    string value = match.Groups[2].Value.Trim();
                    switch (match.Groups[1].Value.ToLowerInvariant())
                    {
                        case "name" when value.Length > 0:
                            name = value;
                            break;
                        case "icon" when value.Length > 0:
                            icon = value;
                            break;
                        case "extensions":
                            extensions = value.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(e => e.StartsWith('.') ? e : "." + e)
                                .ToArray();
                            break;
                    }
                }
            }
            return new ScriptAction(path, name, icon, extensions);
        }

        [GeneratedRegex(@"Pouchy-(Name|Icon|Extensions)\s*:\s*(.*)$", RegexOptions.IgnoreCase)]
        private static partial Regex HeaderRegex();

        /// <summary>How the script is started: the program and its arguments.</summary>
        internal static ProcessStartInfo CreateStartInfo(string script, IReadOnlyList<string> paths)
        {
            var info = new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = paths.Count > 0 ? Path.GetDirectoryName(paths[0].TrimEnd('\\')) ?? "" : "",
            };

            switch (Path.GetExtension(script).ToLowerInvariant())
            {
                case ".ps1":
                    info.FileName = "powershell.exe";
                    foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script }) info.ArgumentList.Add(arg);
                    foreach (var path in paths) info.ArgumentList.Add(path);
                    break;
                case ".bat":
                case ".cmd":
                    // cmd has its own quoting rules: /s /c "<whole command line>".
                    info.FileName = "cmd.exe";
                    info.Arguments = "/d /s /c \"" + string.Join(" ", new[] { script }.Concat(paths).Select(a => $"\"{a}\"")) + "\"";
                    break;
                case ".py":
                    info.FileName = "py.exe"; // The Python launcher; picks the newest installed Python.
                    info.ArgumentList.Add(script);
                    foreach (var path in paths) info.ArgumentList.Add(path);
                    break;
                default:
                    info.FileName = script;
                    foreach (var path in paths) info.ArgumentList.Add(path);
                    break;
            }
            return info;
        }

        public async Task<ActionResult> RunAsync(ActionContext context)
        {
            var info = CreateStartInfo(ScriptPath, context.Paths);
            Process process;
            try
            {
                process = Process.Start(info) ?? throw new IOException("The script didn't start.");
            }
            catch (System.ComponentModel.Win32Exception) when (info.FileName == "py.exe")
            {
                info.FileName = "python.exe";
                process = Process.Start(info) ?? throw new IOException("The script didn't start.");
            }

            using (process)
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(Timeout);
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);
                    throw new TimeoutException($"\"{Name}\" was still running after {Timeout.TotalMinutes:0} minutes and was stopped.");
                }

                string stdout = await output;
                string stderr = await errors;
                Logger.Log($"Action \"{Name}\" exited with {process.ExitCode}.");
                if (process.ExitCode != 0)
                {
                    string detail = LastLines(stderr.Length > 0 ? stderr : stdout, 6);
                    throw new IOException($"\"{Name}\" failed (exit code {process.ExitCode}).{(detail.Length > 0 ? "\n\n" + detail : "")}");
                }
                return new ActionResult(OutputFiles(stdout), $"Ran “{Name}”");
            }
        }

        /// <summary>Lines of script output that are paths of existing files or folders.</summary>
        internal static List<string> OutputFiles(string output) =>
            output.Split('\n')
                .Select(l => l.Trim().Trim('"'))
                .Where(l => l.Length > 3 && Path.IsPathFullyQualified(l) && (File.Exists(l) || Directory.Exists(l)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static string LastLines(string text, int count) =>
            string.Join("\n", text.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).TakeLast(count));
    }
}
