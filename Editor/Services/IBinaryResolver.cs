namespace SoftAware.YouTubeAudioImporter.Editor.Services
{
    public interface IBinaryResolver
    {
        string GetYtDlpPath();
        string GetFfmpegPath();
        bool TryGetYtDlpPath(out string path);
        bool TryGetFfmpegPath(out string path);
        void SetCustomYtDlpPath(string path);
        void SetCustomFfmpegPath(string path);
    }
}
