using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SoftAware.YouTubeAudioImporter.Editor.Models
{
    [Serializable]
    public class YouTubeMetadataDto
    {
        public string id;
        public string title;
        public string uploader;
        public string channel;
        public float duration;
        public string thumbnail;
        public string webpage_url;
    }

    public sealed class YouTubeMetadata
    {
        public string Id { get; }
        public string Title { get; }
        public string Author { get; }
        public float DurationSeconds { get; }
        public string ThumbnailUrl { get; }
        public string WebpageUrl { get; }

        public string FormattedDuration
        {
            get
            {
                var timeSpan = TimeSpan.FromSeconds(Math.Max(0, DurationSeconds));
                return timeSpan.TotalHours >= 1 
                    ? $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}" 
                    : $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
        }

        public YouTubeMetadata(string id, string title, string author, float durationSeconds, string thumbnailUrl, string webpageUrl)
        {
            Id = id ?? string.Empty;
            Title = title ?? "Untitled Audio";
            Author = author ?? "Unknown Author";
            DurationSeconds = durationSeconds;
            ThumbnailUrl = thumbnailUrl ?? string.Empty;
            WebpageUrl = webpageUrl ?? string.Empty;
        }

        public static YouTubeMetadata FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("JSON content must not be null or empty.", nameof(json));
            }

            var dto = JsonUtility.FromJson<YouTubeMetadataDto>(json);
            if (dto == null)
            {
                throw new InvalidOperationException("Failed to deserialize YouTube metadata JSON.");
            }

            var author = !string.IsNullOrWhiteSpace(dto.uploader) ? dto.uploader : dto.channel;
            return new YouTubeMetadata(dto.id, dto.title, author, dto.duration, dto.thumbnail, dto.webpage_url);
        }

        public string GetSanitizedFileName()
        {
            var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            var invalidReg = new Regex($"[{invalidChars}]");
            var cleanTitle = invalidReg.Replace(Title, "_").Trim();

            if (string.IsNullOrWhiteSpace(cleanTitle))
            {
                cleanTitle = !string.IsNullOrWhiteSpace(Id) ? Id : "audio_clip";
            }

            return cleanTitle;
        }
    }
}
