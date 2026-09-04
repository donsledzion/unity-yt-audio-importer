using UnityEngine;

namespace SoftAware.YouTubeAudioImporter.Editor.Models
{
    public sealed class ImportRequest
    {
        public string VideoUrl { get; }
        public AudioFormat TargetFormat { get; }
        public string TargetFolder { get; }
        public string CustomFileName { get; }
        public float? TrimStartSeconds { get; }
        public float? TrimEndSeconds { get; }
        public int AudioBitrateKbps { get; }

        public ImportRequest(
            string videoUrl,
            AudioFormat targetFormat = AudioFormat.Wav,
            string targetFolder = "Assets/Audio",
            string customFileName = null,
            float? trimStartSeconds = null,
            float? trimEndSeconds = null,
            int audioBitrateKbps = 192)
        {
            VideoUrl = videoUrl;
            TargetFormat = targetFormat;
            TargetFolder = targetFolder;
            CustomFileName = customFileName;
            TrimStartSeconds = trimStartSeconds;
            TrimEndSeconds = trimEndSeconds;
            AudioBitrateKbps = audioBitrateKbps;
        }
    }

    public sealed class ImportResult
    {
        public bool Success { get; }
        public string AssetPath { get; }
        public string AbsoluteFilePath { get; }
        public AudioClip LoadedAudioClip { get; }
        public string ErrorMessage { get; }

        private ImportResult(bool success, string assetPath, string absoluteFilePath, AudioClip loadedAudioClip, string errorMessage)
        {
            Success = success;
            AssetPath = assetPath ?? string.Empty;
            AbsoluteFilePath = absoluteFilePath ?? string.Empty;
            LoadedAudioClip = loadedAudioClip;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public static ImportResult Succeeded(string assetPath, string absoluteFilePath, AudioClip loadedAudioClip)
        {
            return new ImportResult(true, assetPath, absoluteFilePath, loadedAudioClip, null);
        }

        public static ImportResult Failed(string errorMessage)
        {
            return new ImportResult(false, null, null, null, errorMessage);
        }
    }
}
