using System;

namespace SoftAware.YouTubeAudioImporter.Editor.Models
{
    public enum AudioFormat
    {
        Wav,
        Mp3,
        Ogg
    }

    public static class AudioFormatExtensions
    {
        public static string GetFileExtension(this AudioFormat format) => format switch
        {
            AudioFormat.Wav => ".wav",
            AudioFormat.Mp3 => ".mp3",
            AudioFormat.Ogg => ".ogg",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, $"Unsupported audio format: {format}")
        };

        public static string GetFfmpegCodec(this AudioFormat format) => format switch
        {
            AudioFormat.Wav => "pcm_s16le",
            AudioFormat.Mp3 => "libmp3lame",
            AudioFormat.Ogg => "libvorbis",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, $"Unsupported audio format: {format}")
        };
    }
}
