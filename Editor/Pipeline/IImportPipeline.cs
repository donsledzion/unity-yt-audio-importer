using System;
using System.Threading;
using System.Threading.Tasks;
using SoftAware.YouTubeAudioImporter.Editor.Models;

namespace SoftAware.YouTubeAudioImporter.Editor.Pipeline
{
    public interface IImportPipeline
    {
        Task<ImportResult> ExecuteImportAsync(
            ImportRequest request,
            IProgress<string> progress = null,
            CancellationToken cancellationToken = default);

        Task<ImportResult> TrimAndSaveAsync(
            string sourceAssetPath,
            float startSeconds,
            float endSeconds,
            string destinationFolder = null,
            string customFileName = null,
            IProgress<string> progress = null,
            CancellationToken cancellationToken = default);
    }
}
