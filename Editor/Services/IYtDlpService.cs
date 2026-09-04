using System;
using System.Threading;
using System.Threading.Tasks;
using SoftAware.YouTubeAudioImporter.Editor.Models;

namespace SoftAware.YouTubeAudioImporter.Editor.Services
{
    public interface IYtDlpService
    {
        Task<YouTubeMetadata> GetMetadataAsync(string videoUrl, CancellationToken cancellationToken = default);
        Task<string> DownloadAudioAsync(string videoUrl, string targetDirectory, Action<string> onProgress = null, CancellationToken cancellationToken = default);
    }
}
