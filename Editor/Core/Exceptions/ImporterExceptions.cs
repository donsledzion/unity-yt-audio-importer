using System;

namespace SoftAware.YouTubeAudioImporter.Editor.Core.Exceptions
{
    public class ImporterException : Exception
    {
        public ImporterException(string message) : base(message)
        {
        }

        public ImporterException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class BinaryNotFoundException : ImporterException
    {
        public string BinaryName { get; }

        public BinaryNotFoundException(string binaryName, string message) : base(message)
        {
            BinaryName = binaryName;
        }
    }

    public class ProcessExecutionException : ImporterException
    {
        public string ExecutablePath { get; }
        public int ExitCode { get; }
        public string StandardError { get; }

        public ProcessExecutionException(string executablePath, int exitCode, string standardError, string message) 
            : base(message)
        {
            ExecutablePath = executablePath;
            ExitCode = exitCode;
            StandardError = standardError;
        }
    }

    public class YtDlpException : ImporterException
    {
        public YtDlpException(string message) : base(message)
        {
        }

        public YtDlpException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class FfmpegException : ImporterException
    {
        public FfmpegException(string message) : base(message)
        {
        }

        public FfmpegException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
