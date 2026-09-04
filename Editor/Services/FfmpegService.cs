using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SoftAware.YouTubeAudioImporter.Editor.Core;
using SoftAware.YouTubeAudioImporter.Editor.Core.Exceptions;
using SoftAware.YouTubeAudioImporter.Editor.Models;

namespace SoftAware.YouTubeAudioImporter.Editor.Services
{
    public sealed class FfmpegService : IFfmpegService
    {
        private readonly IBinaryResolver _binaryResolver;

        public FfmpegService(IBinaryResolver binaryResolver)
        {
            _binaryResolver = binaryResolver ?? throw new ArgumentNullException(nameof(binaryResolver));
        }

        public async Task<string> ConvertAudioAsync(
            string inputFilePath,
            string outputFilePath,
            AudioFormat format,
            int bitrateKbps = 192,
            float? startSeconds = null,
            float? endSeconds = null,
            Action<string> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(inputFilePath))
            {
                throw new ArgumentException("Input file path must not be null or empty.", nameof(inputFilePath));
            }

            if (!File.Exists(inputFilePath))
            {
                throw new FileNotFoundException($"Input audio file was not found at '{inputFilePath}'.", inputFilePath);
            }

            if (string.IsNullOrWhiteSpace(outputFilePath))
            {
                throw new ArgumentException("Output file path must not be null or empty.", nameof(outputFilePath));
            }

            var outputDir = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var ffmpegPath = _binaryResolver.GetFfmpegPath();
            var argsBuilder = new StringBuilder();

            // -y overwrite without prompting, -v error / warning
            argsBuilder.Append("-y ");

            // Input
            argsBuilder.Append($"-i \"{inputFilePath}\" ");

            // Accurate trimming (placed after -i)
            if (startSeconds.HasValue && startSeconds.Value > 0f)
            {
                argsBuilder.Append($"-ss {startSeconds.Value.ToString("F3", CultureInfo.InvariantCulture)} ");
            }

            if (endSeconds.HasValue && endSeconds.Value > 0f)
            {
                argsBuilder.Append($"-to {endSeconds.Value.ToString("F3", CultureInfo.InvariantCulture)} ");
            }

            // Disable video stream
            argsBuilder.Append("-vn ");

            // Audio codec and bitrate
            switch (format)
            {
                case AudioFormat.Wav:
                    argsBuilder.Append("-c:a pcm_s16le ");
                    break;
                case AudioFormat.Mp3:
                    argsBuilder.Append($"-c:a libmp3lame -b:a {Math.Max(64, bitrateKbps)}k ");
                    break;
                case AudioFormat.Ogg:
                    argsBuilder.Append($"-c:a libvorbis -b:a {Math.Max(64, bitrateKbps)}k ");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, $"Unsupported audio format: {format}");
            }

            // Output destination
            argsBuilder.Append($"\"{outputFilePath}\"");

            var result = await ProcessRunner.ExecuteAsync(
                ffmpegPath,
                argsBuilder.ToString(),
                onOutputLine: onProgress,
                onErrorLine: onProgress,
                cancellationToken: cancellationToken
            );

            if (!result.Success)
            {
                throw new FfmpegException($"ffmpeg failed during audio conversion (Exit Code: {result.ExitCode}): {result.StandardError}");
            }

            if (!File.Exists(outputFilePath))
            {
                throw new FfmpegException($"ffmpeg process completed successfully but output file was not found at '{outputFilePath}'.");
            }

            return outputFilePath;
        }

        public async Task<string> TrimAudioAsync(
            string sourceFilePath,
            string outputFilePath,
            float startSeconds,
            float endSeconds,
            AudioFormat format,
            int bitrateKbps = 192,
            Action<string> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            if (endSeconds <= startSeconds)
            {
                throw new ArgumentException($"End point ({endSeconds}s) must be strictly greater than start point ({startSeconds}s).");
            }

            return await ConvertAudioAsync(
                sourceFilePath,
                outputFilePath,
                format,
                bitrateKbps,
                startSeconds,
                endSeconds,
                onProgress,
                cancellationToken
            );
        }
    }
}
