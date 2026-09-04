using System;
using System.Threading;
using System.Threading.Tasks;
using SoftAware.YouTubeAudioImporter.Editor.Models;

namespace SoftAware.YouTubeAudioImporter.Editor.Services
{
    public interface IFfmpegService
    {
        Task<string> ConvertAudioAsync(
            string inputFilePath,
            string outputFilePath,
            AudioFormat format,
            int bitrateKbps = 192,
            float? startSeconds = null,
            float? endSeconds = null,
            Action<string> onProgress = null,
            CancellationToken cancellationToken = default);

        Task<string> TrimAudioAsync(
            string sourceFilePath,
            string outputFilePath,
            float startSeconds,
            float endSeconds,
            AudioFormat format,
            int bitrateKbps = 192,
            Action<string> onProgress = null,
            CancellationToken cancellationToken = default);
    }
}
