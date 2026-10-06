using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Pouchy.Services
{
    public sealed record UpdateInfo(Version Version, string Tag, string Url);

    /// <summary>
    /// Checks GitHub for a newer release. One small request to the GitHub API, nothing else is sent.
    /// </summary>
    public sealed class UpdateService : IDisposable
    {
        public const string Repository = "editorrylix/Pouchy";
        public const string RepositoryUrl = "https://github.com/" + Repository;
        public const string ReleasesUrl = RepositoryUrl + "/releases";

        private readonly HttpClient _http;

        public UpdateService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Pouchy/{CurrentVersionText}");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }

        /// <summary>"1.0.0" (from the assembly's informational version, without build metadata).</summary>
        public static string CurrentVersionText
        {
            get
            {
                string? version = typeof(UpdateService).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                return string.IsNullOrEmpty(version) ? "0.0.0" : version.Split('+')[0];
            }
        }

        public static Version CurrentVersion => TryParseVersion(CurrentVersionText, out var v) ? v : new Version(0, 0, 0);

        /// <returns>The newer release, or null if up to date or the check failed.</returns>
        public async Task<UpdateInfo?> CheckAsync()
        {
            try
            {
                using var response = await _http.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest");
                if (!response.IsSuccessStatusCode) return null;

                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                string tag = json.RootElement.GetProperty("tag_name").GetString() ?? "";
                string url = json.RootElement.TryGetProperty("html_url", out var html) ? html.GetString() ?? ReleasesUrl : ReleasesUrl;

                if (!TryParseVersion(tag, out var latest)) return null;
                return latest > CurrentVersion ? new UpdateInfo(latest, tag, url) : null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
            {
                Logger.Log("Update check failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>Parses "v1.2.3", "1.2.3" or "1.2.3-beta.1" (pre-release suffixes are ignored).</summary>
        public static bool TryParseVersion(string text, out Version version)
        {
            string core = text.Trim().TrimStart('v', 'V').Split('-', '+')[0];
            if (Version.TryParse(core, out var parsed) && parsed.Major >= 0)
            {
                version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
                return true;
            }
            version = new Version(0, 0, 0);
            return false;
        }

        public void Dispose() => _http.Dispose();
    }
}
