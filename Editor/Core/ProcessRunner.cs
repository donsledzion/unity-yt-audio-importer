using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SoftAware.YouTubeAudioImporter.Editor.Core.Exceptions;

namespace SoftAware.YouTubeAudioImporter.Editor.Core
{
    public static class ProcessRunner
    {
        public static async Task<ProcessResult> ExecuteAsync(
            string executablePath,
            string arguments,
            string workingDirectory = null,
            Action<string> onOutputLine = null,
            Action<string> onErrorLine = null,
            CancellationToken cancellationToken = default,
            TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new ArgumentException("Executable path must not be null or empty.", nameof(executablePath));
            }

            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException($"Executable file was not found at '{executablePath}'.", executablePath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var processExitTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            process.Exited += (_, _) =>
            {
                processExitTcs.TrySetResult(process.ExitCode);
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                outputBuilder.AppendLine(e.Data);
                onOutputLine?.Invoke(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                errorBuilder.AppendLine(e.Data);
                onErrorLine?.Invoke(e.Data);
            };

            var started = process.Start();
            if (!started)
            {
                throw new ProcessExecutionException(executablePath, -1, string.Empty, $"Failed to start process '{executablePath}'.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : null;
            using var linkedCts = timeoutCts != null 
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token) 
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            using (linkedCts.Token.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch
                {
                    // Best-effort process termination on cancellation/timeout
                }
            }))
            {
                try
                {
                    var exitCode = await processExitTcs.Task;
                    // Ensure standard streams flushed
                    process.WaitForExit();

                    var stdout = outputBuilder.ToString();
                    var stderr = errorBuilder.ToString();

                    return new ProcessResult(exitCode, stdout, stderr);
                }
                catch (Exception ex) when (linkedCts.IsCancellationRequested)
                {
                    if (timeoutCts != null && timeoutCts.IsCancellationRequested)
                    {
                        throw new TimeoutException($"Process '{executablePath}' timed out after {timeout.Value.TotalSeconds} seconds.", ex);
                    }

                    throw new OperationCanceledException("Process execution was canceled.", ex, cancellationToken);
                }
            }
        }
    }
}
