using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SoftAware.YouTubeAudioImporter.Editor.Core;
using SoftAware.YouTubeAudioImporter.Editor.Core.Exceptions;
using SoftAware.YouTubeAudioImporter.Editor.Models;

namespace SoftAware.YouTubeAudioImporter.Editor.Services
{
    public sealed class YtDlpService : IYtDlpService
    {
        private readonly IBinaryResolver _binaryResolver;

        public YtDlpService(IBinaryResolver binaryResolver)
        {
            _binaryResolver = binaryResolver ?? throw new ArgumentNullException(nameof(binaryResolver));
        }

        public async Task<YouTubeMetadata> GetMetadataAsync(string videoUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                throw new ArgumentException("Video URL must not be null or empty.", nameof(videoUrl));
            }

            var ytDlpPath = _binaryResolver.GetYtDlpPath();
            var arguments = $"--dump-json --no-playlist --skip-download --no-warnings \"{videoUrl.Trim()}\"";

            var result = await ProcessRunner.ExecuteAsync(
                ytDlpPath,
                arguments,
                cancellationToken: cancellationToken,
                timeout: TimeSpan.FromSeconds(45)
            );

            if (!result.Success)
            {
                throw new YtDlpException($"yt-dlp failed to retrieve metadata (Exit Code: {result.ExitCode}): {result.StandardError}");
            }

            var json = result.StandardOutput.Trim();
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new YtDlpException("yt-dlp returned empty metadata output.");
            }

            // In case multiple lines are returned (e.g. annotations/warnings), find the main JSON line
            var lines = json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var mainJson = lines[0];
            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith("{") && line.TrimEnd().EndsWith("}"))
                {
                    mainJson = line;
                    break;
                }
            }

            try
            {
                return YouTubeMetadata.FromJson(mainJson);
            }
            catch (Exception ex)
            {
                throw new YtDlpException($"Failed to parse YouTube metadata JSON: {ex.Message}", ex);
            }
        }

        public async Task<string> DownloadAudioAsync(
            string videoUrl,
            string targetDirectory,
            Action<string> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                throw new ArgumentException("Video URL must not be null or empty.", nameof(videoUrl));
            }

            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new ArgumentException("Target directory must not be null or empty.", nameof(targetDirectory));
            }

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            var ytDlpPath = _binaryResolver.GetYtDlpPath();
            var outputTemplate = Path.Combine(targetDirectory, "%(id)s_raw.%(ext)s").Replace('\\', '/');

            var ffmpegArgs = string.Empty;
            if (_binaryResolver.TryGetFfmpegPath(out var ffmpegPath))
            {
                var ffmpegDir = Path.GetDirectoryName(ffmpegPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(ffmpegDir))
                {
                    ffmpegArgs = $"--ffmpeg-location \"{ffmpegDir}\" ";
                }
            }

            var arguments = $"{ffmpegArgs}-f \"bestaudio/best\" --no-playlist --no-warnings --print after_move:filepath -o \"{outputTemplate}\" \"{videoUrl.Trim()}\"";

            string downloadedFilePath = null;

            var result = await ProcessRunner.ExecuteAsync(
                ytDlpPath,
                arguments,
                workingDirectory: targetDirectory,
                onOutputLine: line =>
                {
                    if (string.IsNullOrWhiteSpace(line)) return;

                    var trimmed = line.Trim();
                    // yt-dlp --print after_move:filepath outputs the absolute or relative filepath
                    if (File.Exists(trimmed))
                    {
                        downloadedFilePath = Path.GetFullPath(trimmed);
                    }
                    else
                    {
                        var combined = Path.Combine(targetDirectory, trimmed);
                        if (File.Exists(combined))
                        {
                            downloadedFilePath = Path.GetFullPath(combined);
                        }
                    }

                    onProgress?.Invoke(line);
                },
                onErrorLine: onProgress,
                cancellationToken: cancellationToken
            );

            if (!result.Success)
            {
                throw new YtDlpException($"yt-dlp failed during audio download (Exit Code: {result.ExitCode}): {result.StandardError}");
            }

            // If --print output wasn't captured in onOutputLine, scan stdout lines
            if (string.IsNullOrWhiteSpace(downloadedFilePath))
            {
                var stdoutLines = result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in stdoutLines)
                {
                    var trimmed = line.Trim();
                    if (File.Exists(trimmed))
                    {
                        downloadedFilePath = Path.GetFullPath(trimmed);
                        break;
                    }
                    var combined = Path.Combine(targetDirectory, trimmed);
                    if (File.Exists(combined))
                    {
                        downloadedFilePath = Path.GetFullPath(combined);
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(downloadedFilePath) || !File.Exists(downloadedFilePath))
            {
                // Fallback: look for newly downloaded files matching the template pattern
                var rawFiles = Directory.GetFiles(targetDirectory, "*_raw.*");
                if (rawFiles.Length > 0)
                {
                    downloadedFilePath = rawFiles[0];
                }
                else
                {
                    throw new YtDlpException($"yt-dlp finished but output audio file could not be located in '{targetDirectory}'.");
                }
            }

            return downloadedFilePath;
        }
    }
}
