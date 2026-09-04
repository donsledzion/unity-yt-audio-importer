using System;
using System.Threading;
using System.Threading.Tasks;

namespace SoftAware.YouTubeAudioImporter.Editor.Services
{
    public sealed class BinaryUpdateResult
    {
        public bool YtDlpUpdated { get; set; }
        public string YtDlpVersion { get; set; }
        public bool FfmpegDownloaded { get; set; }
        public bool AlreadyUpToDate { get; set; }
        public string Message { get; set; }
    }

    public interface IBinaryDownloaderService
    {
        Task<BinaryUpdateResult> DownloadOrUpdateBinariesAsync(
            bool force = false,
            IProgress<float> progress = null,
            IProgress<string> status = null,
            CancellationToken cancellationToken = default);
    }
}
