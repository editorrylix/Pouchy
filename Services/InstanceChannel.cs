using System.IO;
using System.IO.Pipes;
using System.Text;

namespace Pouchy.Services
{
    /// <summary>
    /// Lets a second launch of Pouchy hand its work to the copy that's already running, over a named pipe.
    /// Explorer starts Pouchy once per selected file for "Add to Pouchy", so this gets used a lot.
    /// </summary>
    public sealed class InstanceChannel : IDisposable
    {
        /// <summary>First line of a message that only asks to show the pouch.</summary>
        public const string ShowCommand = "show";
        private const string AddCommand = "add";

        private readonly string _pipeName;
        private readonly CancellationTokenSource _stop = new();

        /// <summary>Raised on a background thread with the paths another launch asked to add (empty: just show the pouch).</summary>
        public event EventHandler<IReadOnlyList<string>>? Received;

        public InstanceChannel(string? profile)
        {
            _pipeName = PipeName(profile);
        }

        public static string PipeName(string? profile) =>
            $"Pouchy.{Environment.UserDomainName}.{Environment.UserName}.{System.Diagnostics.Process.GetCurrentProcess().SessionId}" + (profile == null ? "" : "." + profile);

        public void Start() => _ = Task.Run(ListenAsync);

        private async Task ListenAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    // CurrentUserOnly: other accounts on the PC can't talk to this pipe.
                    await using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(_stop.Token);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var lines = new List<string>();
                    while (await reader.ReadLineAsync(_stop.Token) is { } line) lines.Add(line);

                    var paths = lines.Count > 0 && lines[0] == AddCommand
                        ? lines.Skip(1).Where(l => l.Length > 0).ToList()
                        : new List<string>();
                    Received?.Invoke(this, paths);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Log("Instance channel error: " + ex.Message);
                    try { await Task.Delay(500, _stop.Token); } catch (OperationCanceledException) { return; }
                }
            }
        }

        /// <summary>Sends paths (or, with none, a request to show the pouch) to the running copy.</summary>
        /// <returns>False if it couldn't be reached.</returns>
        public static bool Send(string? profile, IReadOnlyList<string> paths)
        {
            // The first copy may still be starting, and with many files selected several launches queue up.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", PipeName(profile), PipeDirection.Out, PipeOptions.CurrentUserOnly);
                    client.Connect(1000);
                    using var writer = new StreamWriter(client, new UTF8Encoding(false));
                    writer.WriteLine(paths.Count > 0 ? AddCommand : ShowCommand);
                    foreach (string path in paths) writer.WriteLine(path);
                    writer.Flush();
                    return true;
                }
                catch (Exception ex) when (ex is TimeoutException or IOException)
                {
                    Thread.Sleep(100);
                }
            }
            return false;
        }

        public void Dispose()
        {
            _stop.Cancel();
            _stop.Dispose();
        }
    }
}
