using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SoftAware.YouTubeAudioImporter.Editor.Models;
using SoftAware.YouTubeAudioImporter.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace SoftAware.YouTubeAudioImporter.Editor.Pipeline
{
    public sealed class ImportPipeline : IImportPipeline
    {
        private readonly IYtDlpService _ytDlpService;
        private readonly IFfmpegService _ffmpegService;

        public ImportPipeline(IYtDlpService ytDlpService, IFfmpegService ffmpegService)
        {
            _ytDlpService = ytDlpService ?? throw new ArgumentNullException(nameof(ytDlpService));
            _ffmpegService = ffmpegService ?? throw new ArgumentNullException(nameof(ffmpegService));
        }

        public async Task<ImportResult> ExecuteImportAsync(
            ImportRequest request,
            IProgress<string> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var tempDir = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "YouTubeAudioImporter", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                progress?.Report("Fetching video metadata...");
                var metadata = await _ytDlpService.GetMetadataAsync(request.VideoUrl, cancellationToken);

                progress?.Report($"Downloading raw audio for '{metadata.Title}'...");
                var rawAudioPath = await _ytDlpService.DownloadAudioAsync(
                    request.VideoUrl,
                    tempDir,
                    line => progress?.Report(line),
                    cancellationToken
                );

                progress?.Report("Transcoding audio...");
                var extension = request.TargetFormat.GetFileExtension();
                var convertedFileName = "converted" + extension;
                var convertedFilePath = Path.Combine(tempDir, convertedFileName);

                await _ffmpegService.ConvertAudioAsync(
                    rawAudioPath,
                    convertedFilePath,
                    request.TargetFormat,
                    request.AudioBitrateKbps,
                    request.TrimStartSeconds,
                    request.TrimEndSeconds,
                    line => progress?.Report(line),
                    cancellationToken
                );

                progress?.Report("Importing asset into Unity project...");
                var targetDir = string.IsNullOrWhiteSpace(request.TargetFolder) ? "Assets/Audio" : request.TargetFolder.Trim();
                if (!targetDir.StartsWith("Assets"))
                {
                    targetDir = Path.Combine("Assets", targetDir);
                }
                targetDir = targetDir.Replace('\\', '/');

                var absoluteTargetDir = Path.GetFullPath(targetDir);
                if (!Directory.Exists(absoluteTargetDir))
                {
                    Directory.CreateDirectory(absoluteTargetDir);
                }

                var baseFileName = !string.IsNullOrWhiteSpace(request.CustomFileName)
                    ? SanitizeFileName(request.CustomFileName)
                    : metadata.GetSanitizedFileName();

                var finalAssetRelativePath = GetUniqueAssetPath(targetDir, baseFileName, extension);
                var finalAbsoluteFilePath = Path.GetFullPath(finalAssetRelativePath);

                File.Copy(convertedFilePath, finalAbsoluteFilePath, overwrite: true);

                AudioClip clip = null;
                await RunOnMainThreadAsync(() =>
                {
                    AssetDatabase.ImportAsset(finalAssetRelativePath, ImportAssetOptions.ForceUpdate);
                    AssetDatabase.Refresh();
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(finalAssetRelativePath);
                });

                if (clip == null)
                {
                    throw new InvalidOperationException($"Asset at '{finalAssetRelativePath}' could not be loaded as AudioClip.");
                }

                progress?.Report($"Successfully imported '{clip.name}'!");
                return ImportResult.Succeeded(finalAssetRelativePath, finalAbsoluteFilePath, clip);
            }
            catch (Exception ex)
            {
                progress?.Report($"Import failed: {ex.Message}");
                return ImportResult.Failed(ex.Message);
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
                catch (Exception cleanupEx)
                {
                    Debug.LogWarning($"[YouTubeAudioImporter] Failed to clean temporary folder '{tempDir}': {cleanupEx.Message}");
                }
            }
        }

        public async Task<ImportResult> TrimAndSaveAsync(
            string sourceAssetPath,
            float startSeconds,
            float endSeconds,
            string destinationFolder = null,
            string customFileName = null,
            IProgress<string> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourceAssetPath))
            {
                throw new ArgumentException("Source asset path must not be null or empty.", nameof(sourceAssetPath));
            }

            var absoluteSourcePath = Path.GetFullPath(sourceAssetPath);
            if (!File.Exists(absoluteSourcePath))
            {
                throw new FileNotFoundException($"Source asset file was not found at '{absoluteSourcePath}'.", absoluteSourcePath);
            }

            var extension = Path.GetExtension(sourceAssetPath).ToLowerInvariant();
            var format = extension switch
            {
                ".mp3" => AudioFormat.Mp3,
                ".ogg" => AudioFormat.Ogg,
                _ => AudioFormat.Wav
            };

            var targetDir = string.IsNullOrWhiteSpace(destinationFolder) 
                ? Path.GetDirectoryName(sourceAssetPath)?.Replace('\\', '/') ?? "Assets/Audio"
                : destinationFolder.Trim().Replace('\\', '/');

            var originalBaseName = Path.GetFileNameWithoutExtension(sourceAssetPath);
            var baseFileName = !string.IsNullOrWhiteSpace(customFileName)
                ? SanitizeFileName(customFileName)
                : $"{originalBaseName}_trimmed";

            var uniqueAssetPath = GetUniqueAssetPath(targetDir, baseFileName, extension);
            var uniqueAbsolutePath = Path.GetFullPath(uniqueAssetPath);

            progress?.Report($"Trimming audio from {startSeconds:F2}s to {endSeconds:F2}s...");

            await _ffmpegService.TrimAudioAsync(
                absoluteSourcePath,
                uniqueAbsolutePath,
                startSeconds,
                endSeconds,
                format,
                onProgress: line => progress?.Report(line),
                cancellationToken: cancellationToken
            );

            AudioClip clip = null;
            await RunOnMainThreadAsync(() =>
            {
                AssetDatabase.ImportAsset(uniqueAssetPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(uniqueAssetPath);
            });

            if (clip == null)
            {
                throw new InvalidOperationException($"Trimmed asset at '{uniqueAssetPath}' could not be loaded as AudioClip.");
            }

            progress?.Report($"Trimmed asset saved as '{clip.name}'!");
            return ImportResult.Succeeded(uniqueAssetPath, uniqueAbsolutePath, clip);
        }

        private static string GetUniqueAssetPath(string directory, string fileName, string extension)
        {
            var candidate = $"{directory}/{fileName}{extension}";
            var counter = 1;
            while (File.Exists(candidate))
            {
                candidate = $"{directory}/{fileName}_{counter}{extension}";
                counter++;
            }
            return candidate;
        }

        private static string SanitizeFileName(string input)
        {
            var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            var invalidReg = new Regex($"[{invalidChars}]");
            var result = invalidReg.Replace(input, "_").Trim();
            return string.IsNullOrWhiteSpace(result) ? "audio_clip" : result;
        }

        private static Task RunOnMainThreadAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            EditorApplication.delayCall += () =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            };
            return tcs.Task;
        }
    }
}
