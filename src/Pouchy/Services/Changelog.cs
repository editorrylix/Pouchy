using System.IO;
using System.Text.RegularExpressions;

namespace Pouchy.Services
{
    /// <summary>One version's section of CHANGELOG.md.</summary>
    public sealed record ReleaseNotes(Version Version, string? Date, string Markdown);

    /// <summary>Reads CHANGELOG.md (embedded in the app) for the "What's new" window.</summary>
    public static partial class Changelog
    {
        [GeneratedRegex(@"^## \[(?<version>\d+\.\d+\.\d+)[^\]]*\](\s*-\s*(?<date>.+))?$")]
        private static partial Regex VersionHeading();

        /// <summary>The changelog built into the app, or "" if it's missing.</summary>
        public static string Embedded()
        {
            using var stream = typeof(Changelog).Assembly.GetManifestResourceStream("Pouchy.CHANGELOG.md");
            if (stream == null) return "";
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        /// <summary>Every released version, newest first. "Unreleased" and the link list at the end are skipped.</summary>
        public static List<ReleaseNotes> Parse(string changelog)
        {
            var result = new List<ReleaseNotes>();
            Version? version = null;
            string? date = null;
            var body = new List<string>();

            void Flush()
            {
                if (version != null) result.Add(new ReleaseNotes(version, date, string.Join("\n", body).Trim()));
                version = null;
                body.Clear();
            }

            foreach (string raw in changelog.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.TrimEnd();
                if (line.StartsWith("## "))
                {
                    Flush();
                    var match = VersionHeading().Match(line);
                    if (match.Success && Version.TryParse(match.Groups["version"].Value, out var parsed))
                    {
                        version = parsed;
                        date = match.Groups["date"].Success ? match.Groups["date"].Value.Trim() : null;
                    }
                    continue;
                }
                if (line.StartsWith('[') && line.Contains("]: http")) continue; // Link references at the bottom.
                if (version != null) body.Add(line);
            }
            Flush();
            return result.OrderByDescending(r => r.Version).ToList();
        }

        /// <summary>
        /// What to show after an update: every version newer than <paramref name="lastSeen"/> up to
        /// <paramref name="current"/>. Without a last seen version, just the current one.
        /// </summary>
        public static List<ReleaseNotes> Since(IEnumerable<ReleaseNotes> releases, Version? lastSeen, Version current) =>
            releases
                .Where(r => r.Version <= current && (lastSeen == null ? r.Version == current : r.Version > lastSeen))
                .OrderByDescending(r => r.Version)
                .ToList();
    }
}
