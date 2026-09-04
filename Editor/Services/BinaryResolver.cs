using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using SoftAware.YouTubeAudioImporter.Editor.Core.Exceptions;
using UnityEditor;
using UnityEngine;

namespace SoftAware.YouTubeAudioImporter.Editor.Services
{
    public sealed class BinaryResolver : IBinaryResolver
    {
        private const string PrefsKeyYtDlp = "SoftAware_YtAudioImporter_CustomYtDlp";
        private const string PrefsKeyFfmpeg = "SoftAware_YtAudioImporter_CustomFfmpeg";

        public string GetYtDlpPath()
        {
            if (TryGetYtDlpPath(out var path))
            {
                return path;
            }

            throw new BinaryNotFoundException(
                "yt-dlp",
                "Could not find 'yt-dlp' executable in custom path, package ThirdParty~ directory, or system PATH. " +
                "Please place it under ThirdParty~/yt-dlp/<platform>/ or install it in system PATH."
            );
        }

        public string GetFfmpegPath()
        {
            if (TryGetFfmpegPath(out var path))
            {
                return path;
            }

            throw new BinaryNotFoundException(
                "ffmpeg",
                "Could not find 'ffmpeg' executable in custom path, package ThirdParty~ directory, or system PATH. " +
                "Please place it under ThirdParty~/ffmpeg/<platform>/ or install it in system PATH."
            );
        }

        public bool TryGetYtDlpPath(out string path)
        {
            var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "yt-dlp.exe" : "yt-dlp";
            return TryResolveBinary("yt-dlp", binaryName, PrefsKeyYtDlp, out path);
        }

        public bool TryGetFfmpegPath(out string path)
        {
            var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
            return TryResolveBinary("ffmpeg", binaryName, PrefsKeyFfmpeg, out path);
        }

        public void SetCustomYtDlpPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                EditorPrefs.DeleteKey(PrefsKeyYtDlp);
            }
            else
            {
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"Specified yt-dlp file does not exist: '{path}'", path);
                }
                EditorPrefs.SetString(PrefsKeyYtDlp, path);
            }
        }

        public void SetCustomFfmpegPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                EditorPrefs.DeleteKey(PrefsKeyFfmpeg);
            }
            else
            {
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"Specified ffmpeg file does not exist: '{path}'", path);
                }
                EditorPrefs.SetString(PrefsKeyFfmpeg, path);
            }
        }

        private bool TryResolveBinary(string toolFolder, string executableName, string prefsKey, out string resolvedPath)
        {
            // 1. Check custom path saved in EditorPrefs
            if (EditorPrefs.HasKey(prefsKey))
            {
                var customPath = EditorPrefs.GetString(prefsKey);
                if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
                {
                    resolvedPath = customPath;
                    EnsureUnixExecutable(resolvedPath);
                    return true;
                }
            }

            // 2. Search candidate directories within ThirdParty~ structures
            var platformSubdir = GetPlatformFolderName();
            var candidateDirectories = GetThirdPartySearchRoots();

            foreach (var root in candidateDirectories)
            {
                var candidate = Path.Combine(root, toolFolder, platformSubdir, executableName);
                if (File.Exists(candidate))
                {
                    resolvedPath = Path.GetFullPath(candidate);
                    EnsureUnixExecutable(resolvedPath);
                    return true;
                }

                // Also check without platformSubdir if placed directly under toolFolder
                var directCandidate = Path.Combine(root, toolFolder, executableName);
                if (File.Exists(directCandidate))
                {
                    resolvedPath = Path.GetFullPath(directCandidate);
                    EnsureUnixExecutable(resolvedPath);
                    return true;
                }
            }

            // 3. Search system PATH
            if (TryFindInSystemPath(executableName, out var systemPath))
            {
                resolvedPath = systemPath;
                return true;
            }

            resolvedPath = null;
            return false;
        }

        private static IEnumerable<string> GetThirdPartySearchRoots()
        {
            var roots = new List<string>
            {
                Path.GetFullPath("Packages/com.softaware.youtube-audio-importer/Editor/ThirdParty~"),
                Path.GetFullPath("Packages/com.softaware.youtube-audio-importer/ThirdParty~"),
                Path.GetFullPath("ThirdParty~"),
                Path.GetFullPath("Assets/ThirdParty~")
            };
            return roots;
        }

        private static string GetPlatformFolderName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win-x64";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 
                    ? "osx-arm64" 
                    : "osx-x64";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 
                    ? "linux-arm64" 
                    : "linux-x64";
            }

            return "unknown";
        }

        private static bool TryFindInSystemPath(string executableName, out string foundPath)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(pathEnv))
            {
                var searchPaths = pathEnv.Split(Path.PathSeparator);
                foreach (var folder in searchPaths)
                {
                    if (string.IsNullOrWhiteSpace(folder)) continue;

                    try
                    {
                        var candidate = Path.Combine(folder.Trim(), executableName);
                        if (File.Exists(candidate))
                        {
                            foundPath = Path.GetFullPath(candidate);
                            return true;
                        }
                    }
                    catch
                    {
                        // Ignore path formatting errors in invalid PATH entries
                    }
                }
            }

            foundPath = null;
            return false;
        }

        private static void EnsureUnixExecutable(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            try
            {
                var chmod = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{path}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                chmod.Start();
                chmod.WaitForExit();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[YouTubeAudioImporter] Failed to set execute permission for '{path}': {ex.Message}");
            }
        }
    }
}
