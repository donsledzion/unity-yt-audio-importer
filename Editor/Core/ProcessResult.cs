namespace SoftAware.YouTubeAudioImporter.Editor.Core
{
    public sealed class ProcessResult
    {
        public int ExitCode { get; }
        public string StandardOutput { get; }
        public string StandardError { get; }
        public bool Success => ExitCode == 0;

        public ProcessResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput ?? string.Empty;
            StandardError = standardError ?? string.Empty;
        }
    }
}
