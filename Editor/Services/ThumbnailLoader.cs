using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SoftAware.YouTubeAudioImporter.Editor.Services
{
    public interface IThumbnailLoader
    {
        Task<Texture2D> LoadThumbnailAsync(string url, CancellationToken cancellationToken = default);
        void ClearCache();
    }

    public sealed class ThumbnailLoader : IThumbnailLoader
    {
        private readonly Dictionary<string, Texture2D> _memoryCache = new();

        public async Task<Texture2D> LoadThumbnailAsync(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("Thumbnail URL must not be null or empty.", nameof(url));
            }

            if (_memoryCache.TryGetValue(url, out var cachedTexture) && cachedTexture != null)
            {
                return cachedTexture;
            }

            using var webRequest = UnityWebRequestTexture.GetTexture(url);
            var tcs = new TaskCompletionSource<Texture2D>(TaskCreationOptions.RunContinuationsAsynchronously);

            var op = webRequest.SendWebRequest();
            op.completed += _ =>
            {
                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    tcs.TrySetException(new InvalidOperationException($"Failed to load thumbnail from '{url}': {webRequest.error}"));
                }
                else
                {
                    var texture = DownloadHandlerTexture.GetContent(webRequest);
                    tcs.TrySetResult(texture);
                }
            };

            using (cancellationToken.Register(() =>
            {
                if (!op.isDone)
                {
                    webRequest.Abort();
                    tcs.TrySetCanceled(cancellationToken);
                }
            }))
            {
                var texture = await tcs.Task;
                if (texture != null)
                {
                    _memoryCache[url] = texture;
                }
                return texture;
            }
        }

        public void ClearCache()
        {
            foreach (var texture in _memoryCache.Values)
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            _memoryCache.Clear();
        }
    }
}
