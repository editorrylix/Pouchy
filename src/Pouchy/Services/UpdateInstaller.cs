using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Pouchy.Services
{
    /// <summary>
    /// Installs an update in place: downloads the release zip for this PC, checks it against the
    /// release's SHA256SUMS.txt, swaps Pouchy.exe and starts the new version.
    ///
    /// Windows lets a running .exe be renamed but not overwritten, so the current file becomes
    /// Pouchy.exe.old and is deleted the next time Pouchy starts.
    /// </summary>
    public static class UpdateInstaller
    {
        public const string ChecksumsFile = "SHA256SUMS.txt";

        /// <summary>The installer's entry in Apps &amp; features (AppId in installer/Pouchy.iss plus "_is1").</summary>
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{6F1D5B8E-3C2A-4F7B-9E21-6B3D2A9C4E10}_is1";

        /// <summary>"Pouchy-1.2.0-win-x64.zip" for this PC's architecture.</summary>
        public static string ZipName(Version version, Architecture architecture) =>
            $"Pouchy-{version.ToString(3)}-win-{(architecture == Architecture.Arm64 ? "arm64" : "x64")}.zip";

        /// <summary>Whether the folder Pouchy runs from can be written to (not, say, Program Files).</summary>
        public static bool CanUpdateInPlace(string exePath)
        {
            try
            {
                string probe = Path.Combine(Path.GetDirectoryName(exePath)!, $".pouchy-write-test-{Guid.NewGuid():N}");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Downloads and verifies the update.</summary>
        /// <returns>The new Pouchy.exe, extracted to a temporary folder.</returns>
        public static async Task<string> DownloadAsync(HttpClient http, UpdateInfo update, IProgress<double>? progress = null,
            CancellationToken cancellation = default)
        {
            string zipName = ZipName(update.Version, RuntimeInformation.ProcessArchitecture);
            var zip = update.Assets.FirstOrDefault(a => a.Name.Equals(zipName, StringComparison.OrdinalIgnoreCase))
                      ?? throw new InvalidOperationException($"The release has no {zipName}.");
            var sums = update.Assets.FirstOrDefault(a => a.Name.Equals(ChecksumsFile, StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidOperationException($"The release has no {ChecksumsFile}, so the download can't be checked.");

            string folder = Path.Combine(Path.GetTempPath(), "Pouchy-update", update.Version.ToString(3));
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            Directory.CreateDirectory(folder);

            string expected = FindChecksum(await http.GetStringAsync(sums.Url, cancellation), zip.Name)
                              ?? throw new InvalidOperationException($"{ChecksumsFile} doesn't list {zip.Name}.");

            string zipPath = Path.Combine(folder, zip.Name);
            using (var response = await http.GetAsync(zip.Url, HttpCompletionOption.ResponseHeadersRead, cancellation))
            {
                response.EnsureSuccessStatusCode();
                long total = response.Content.Headers.ContentLength ?? zip.Size;
                await using var source = await response.Content.ReadAsStreamAsync(cancellation);
                await using var target = File.Create(zipPath);
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellation);
                    done += read;
                    if (total > 0) progress?.Report((double)done / total);
                }
            }

            string actual = await HashFileAsync(zipPath, cancellation);
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The download doesn't match the release's checksum, so it wasn't installed.");
            }

            return ExtractExe(zipPath, folder);
        }

        /// <summary>The SHA-256 for a file in a "hash  name" list, or null.</summary>
        public static string? FindChecksum(string sumsFile, string fileName)
        {
            foreach (string raw in sumsFile.Split('\n'))
            {
                var parts = raw.Trim().TrimStart('﻿').Split(new[] { ' ', '\t', '*' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && parts[1].Trim().TrimStart('*').Equals(fileName, StringComparison.OrdinalIgnoreCase) && parts[0].Length == 64)
                {
                    return parts[0];
                }
            }
            return null;
        }

        public static async Task<string> HashFileAsync(string path, CancellationToken cancellation = default)
        {
            await using var stream = File.OpenRead(path);
            return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellation)).ToLowerInvariant();
        }

        internal static string ExtractExe(string zipPath, string folder)
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.Entries.FirstOrDefault(e => e.Name.Equals("Pouchy.exe", StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidDataException("The download doesn't contain Pouchy.exe.");
            string exe = Path.Combine(folder, "Pouchy.exe");
            entry.ExtractToFile(exe, overwrite: true);
            return exe;
        }

        /// <summary>Puts the new exe where the current one is. Rolls back if anything fails.</summary>
        public static void Swap(string newExe, string currentExe)
        {
            string old = currentExe + ".old";
            if (File.Exists(old)) File.Delete(old);
            File.Move(currentExe, old);
            try
            {
                File.Copy(newExe, currentExe);
            }
            catch
            {
                if (File.Exists(currentExe)) File.Delete(currentExe);
                File.Move(old, currentExe);
                throw;
            }
        }

        /// <summary>Starts the new version, which waits for this process to exit before taking over.</summary>
        public static void Restart(string exe, string? profile)
        {
            var info = new ProcessStartInfo(exe) { UseShellExecute = false };
            if (profile != null)
            {
                info.ArgumentList.Add("--profile");
                info.ArgumentList.Add(profile);
            }
            info.ArgumentList.Add("--wait-for");
            info.ArgumentList.Add(Environment.ProcessId.ToString());
            info.ArgumentList.Add("--updated");
            Process.Start(info)?.Dispose();
        }

        /// <summary>
        /// If Pouchy was installed with the installer, show the new version in Apps &amp; features.
        /// </summary>
        public static void UpdateInstalledVersion(string version, string? exePath)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(UninstallKey, writable: true);
                if (key?.GetValue("InstallLocation") is not string location || exePath == null) return;
                if (!string.Equals(Path.GetFullPath(location).TrimEnd('\\'), Path.GetDirectoryName(exePath)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
                key.SetValue("DisplayVersion", version);
            }
            catch (Exception ex)
            {
                Logger.Log("Could not update the installed version: " + ex.Message);
            }
        }

        /// <summary>Deletes the previous version left behind by an update.</summary>
        public static void CleanUpOldVersion(string? exePath)
        {
            if (exePath == null) return;
            try
            {
                if (File.Exists(exePath + ".old")) File.Delete(exePath + ".old");
                string downloads = Path.Combine(Path.GetTempPath(), "Pouchy-update");
                if (Directory.Exists(downloads)) Directory.Delete(downloads, recursive: true);
            }
            catch (Exception ex)
            {
                Logger.Log("Could not remove the previous version: " + ex.Message);
            }
        }
    }
}
