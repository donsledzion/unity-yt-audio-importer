using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SoftAware.YouTubeAudioImporter.Editor.Core;
using UnityEngine;

namespace SoftAware.YouTubeAudioImporter.Editor.Services
{
    public sealed class BinaryDownloaderService : IBinaryDownloaderService
    {
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        public async Task<BinaryUpdateResult> DownloadOrUpdateBinariesAsync(
            bool force = false,
            IProgress<float> progress = null,
            IProgress<string> status = null,
            CancellationToken cancellationToken = default)
        {
            var platformFolder = GetPlatformFolderName();
            var baseThirdPartyDir = Path.GetFullPath("Packages/com.softaware.youtube-audio-importer/Editor/ThirdParty~");
            var tempDir = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "BinaryDownload_" + Guid.NewGuid().ToString("N"));

            var ytDlpFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "yt-dlp.exe" : "yt-dlp";
            var ffmpegFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";

            var ytDlpTargetDir = Path.Combine(baseThirdPartyDir, "yt-dlp", platformFolder);
            var ffmpegTargetDir = Path.Combine(baseThirdPartyDir, "ffmpeg", platformFolder);

            var ytDlpDestination = Path.Combine(ytDlpTargetDir, ytDlpFileName);
            var ffmpegDestination = Path.Combine(ffmpegTargetDir, ffmpegFileName);

            var result = new BinaryUpdateResult();

            // 1. Check ffmpeg status
            var ffmpegNeeded = force || !File.Exists(ffmpegDestination);
            if (!ffmpegNeeded)
            {
                status?.Report("ffmpeg is already installed. Checking yt-dlp version...");
            }

            // 2. Check yt-dlp local vs remote version
            string localYtDlpVersion = null;
            if (File.Exists(ytDlpDestination))
            {
                try
                {
                    var verResult = await ProcessRunner.ExecuteAsync(ytDlpDestination, "--version", cancellationToken: cancellationToken);
                    if (verResult.Success && !string.IsNullOrWhiteSpace(verResult.StandardOutput))
                    {
                        localYtDlpVersion = verResult.StandardOutput.Trim();
                        result.YtDlpVersion = localYtDlpVersion;
                    }
                }
                catch
                {
                    // If local execution fails, re-download is required
                }
            }

            status?.Report("Checking latest yt-dlp release on GitHub...");
            var latestYtDlpTag = await GetLatestYtDlpReleaseTagAsync(cancellationToken);

            var ytDlpNeeded = force || string.IsNullOrEmpty(localYtDlpVersion) || 
                              (!string.IsNullOrEmpty(latestYtDlpTag) && !string.Equals(localYtDlpVersion, latestYtDlpTag, StringComparison.OrdinalIgnoreCase));

            // If neither needs downloading, return immediately
            if (!ffmpegNeeded && !ytDlpNeeded)
            {
                result.AlreadyUpToDate = true;
                result.Message = $"All binaries are already up to date (yt-dlp: {localYtDlpVersion}, ffmpeg: ready). No download needed.";
                status?.Report(result.Message);
                progress?.Report(1f);
                return result;
            }

            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(ytDlpTargetDir);
            Directory.CreateDirectory(ffmpegTargetDir);

            try
            {
                // Download yt-dlp if needed
                if (ytDlpNeeded)
                {
                    status?.Report($"Downloading yt-dlp {(string.IsNullOrEmpty(latestYtDlpTag) ? "" : $"version {latestYtDlpTag}")}...");
                    var ytDlpUrl = GetYtDlpDownloadUrl();
                    await DownloadFileAsync(ytDlpUrl, ytDlpDestination, progress, status, 0.1f, 0.45f, cancellationToken);
                    EnsureUnixExecutable(ytDlpDestination);

                    result.YtDlpUpdated = true;
                    result.YtDlpVersion = latestYtDlpTag ?? "latest";
                }
                else
                {
                    status?.Report($"yt-dlp is already up to date ({localYtDlpVersion}).");
                }

                // Download ffmpeg if needed
                if (ffmpegNeeded)
                {
                    status?.Report("Downloading ffmpeg...");
                    var ffmpegUrl = GetFfmpegDownloadUrl();

                    if (ffmpegUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        var tempZipPath = Path.Combine(tempDir, "ffmpeg_download.zip");
                        await DownloadFileAsync(ffmpegUrl, tempZipPath, progress, status, 0.5f, 0.9f, cancellationToken);

                        status?.Report("Extracting ffmpeg executable from zip...");
                        ExtractBinaryFromZip(tempZipPath, ffmpegFileName, ffmpegDestination);
                    }
                    else
                    {
                        await DownloadFileAsync(ffmpegUrl, ffmpegDestination, progress, status, 0.5f, 0.9f, cancellationToken);
                    }

                    EnsureUnixExecutable(ffmpegDestination);
                    result.FfmpegDownloaded = true;
                }
                else
                {
                    status?.Report("ffmpeg is already installed. Preserving existing binary.");
                }

                progress?.Report(1f);
                result.Message = "Binaries successfully updated!";
                status?.Report(result.Message);
                return result;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[YouTubeAudioImporter] Failed to delete download temp dir: {ex.Message}");
                }
            }
        }

        private static async Task<string> GetLatestYtDlpReleaseTagAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Request headers only from GitHub latest release redirect
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://github.com/yt-dlp/yt-dlp/releases/latest");
                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                var finalUrl = response.RequestMessage?.RequestUri?.ToString();
                if (!string.IsNullOrEmpty(finalUrl) && finalUrl.Contains("/tag/"))
                {
                    var tagIndex = finalUrl.IndexOf("/tag/", StringComparison.Ordinal);
                    var tag = finalUrl.Substring(tagIndex + 5).TrimEnd('/');
                    return tag;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[YouTubeAudioImporter] Could not query latest yt-dlp version tag from GitHub: {ex.Message}");
            }

            return null;
        }

        private static async Task DownloadFileAsync(
            string url,
            string destinationPath,
            IProgress<float> progress,
            IProgress<string> status,
            float progressStart,
            float progressEnd,
            CancellationToken cancellationToken)
        {
            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[81920];
            var totalRead = 0L;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    var fileRatio = (float)totalRead / totalBytes;
                    var scaledProgress = progressStart + (fileRatio * (progressEnd - progressStart));
                    progress?.Report(scaledProgress);
                    status?.Report($"Downloading ({totalRead / 1048576f:F1} MB / {totalBytes / 1048576f:F1} MB)...");
                }
            }
        }

        private static void ExtractBinaryFromZip(string zipPath, string targetBinaryName, string destinationFilePath)
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (entry.Name.Equals(targetBinaryName, StringComparison.OrdinalIgnoreCase))
                {
                    entry.ExtractToFile(destinationFilePath, overwrite: true);
                    return;
                }
            }

            throw new FileNotFoundException($"Could not find '{targetBinaryName}' inside downloaded archive '{zipPath}'.");
        }

        private static string GetYtDlpDownloadUrl()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos";
            }
            return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp";
        }

        private static string GetFfmpegDownloadUrl()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "https://github.com/GyanD/codexffmpeg/releases/download/7.1/ffmpeg-7.1-essentials_build.zip";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "https://evermeet.cx/ffmpeg/getrelease/zip";
            }
            return "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linux64-gpl.tar.xz";
        }

        private static string GetPlatformFolderName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win-x64";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
            }
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        }

        private static void EnsureUnixExecutable(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            try
            {
                var chmod = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{path}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                chmod.Start();
                chmod.WaitForExit();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[YouTubeAudioImporter] Failed to set execute permission: {ex.Message}");
            }
        }
    }
}
