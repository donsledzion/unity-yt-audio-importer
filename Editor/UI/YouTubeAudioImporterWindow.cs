using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SoftAware.YouTubeAudioImporter.Editor.Models;
using SoftAware.YouTubeAudioImporter.Editor.Pipeline;
using SoftAware.YouTubeAudioImporter.Editor.Services;
using SoftAware.YouTubeAudioImporter.Editor.UI.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SoftAware.YouTubeAudioImporter.Editor.UI
{
    public sealed class YouTubeAudioImporterWindow : EditorWindow
    {
        [MenuItem("Tools/YouTube Audio Importer", false, 2000)]
        public static void ShowWindow()
        {
            var window = GetWindow<YouTubeAudioImporterWindow>();
            window.titleContent = new GUIContent("YT Audio Importer", EditorGUIUtility.IconContent("d_AudioClip Icon").image);
            window.minSize = new Vector2(460, 580);
            window.Show();
        }

        private IBinaryResolver _binaryResolver;
        private IYtDlpService _ytDlpService;
        private IFfmpegService _ffmpegService;
        private IThumbnailLoader _thumbnailLoader;
        private IImportPipeline _importPipeline;
        private IBinaryDownloaderService _binaryDownloaderService;

        private CancellationTokenSource _cts;
        private YouTubeMetadata _currentMetadata;
        private AudioClip _currentLoadedClip;
        private string _currentAssetPath;
        private bool _isPlayingPreview;

        // Visual Elements
        private Label _binaryStatusBadge;
        private VisualElement _binaryMissingBanner;
        private Button _btnBannerDownloadBinaries;
        private Button _btnDownloadBinaries;
        private TextField _inputYtDlpPath;
        private Button _btnBrowseYtDlp;
        private TextField _inputFfmpegPath;
        private Button _btnBrowseFfmpeg;

        private TextField _inputVideoUrl;
        private Button _btnFetchMetadata;

        private VisualElement _metadataCard;
        private Image _metadataThumbnail;
        private Label _metadataTitle;
        private Label _metadataAuthor;
        private Label _metadataDuration;

        private VisualElement _sectionImportOptions;
        private DropdownField _dropdownFormat;
        private DropdownField _dropdownBitrate;
        private TextField _inputTargetFolder;
        private Button _btnBrowseTargetFolder;
        private TextField _inputCustomFilename;
        private Button _btnImportAudio;

        private VisualElement _sectionTrimming;
        private WaveformTrimView _waveformTrimView;
        private FloatField _inputTrimStart;
        private FloatField _inputTrimEnd;
        private Label _labelTrimDuration;
        private Toggle _toggleLoopPreview;
        private Button _btnPlayPreview;
        private Button _btnSaveTrimmed;

        private double _previewStartTime;
        private float _previewStartOffset;
        private float _previewDuration;

        private VisualElement _sectionProgress;
        private ProgressBar _progressBar;
        private Label _labelStatus;
        private Button _btnCancelOperation;

        private void OnEnable()
        {
            _binaryResolver = new BinaryResolver();
            _ytDlpService = new YtDlpService(_binaryResolver);
            _ffmpegService = new FfmpegService(_binaryResolver);
            _thumbnailLoader = new ThumbnailLoader();
            _importPipeline = new ImportPipeline(_ytDlpService, _ffmpegService);
            _binaryDownloaderService = new BinaryDownloaderService();

            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            CancelCurrentOperation();
            StopAudioPreview();
            _thumbnailLoader?.ClearCache();
        }

        public void CreateGUI()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.softaware.youtube-audio-importer/Editor/UI/YouTubeAudioImporterWindow.uxml"
            );

            if (visualTree == null)
            {
                Debug.LogError("[YouTubeAudioImporter] Failed to load YouTubeAudioImporterWindow.uxml from package.");
                return;
            }

            visualTree.CloneTree(rootVisualElement);

            QueryVisualElements();
            InitializeInputs();
            RegisterEventHandlers();
            RefreshBinaryStatus();
        }

        private void QueryVisualElements()
        {
            _binaryStatusBadge = rootVisualElement.Q<Label>("binary-status-badge");
            _binaryMissingBanner = rootVisualElement.Q<VisualElement>("binary-missing-banner");
            _btnBannerDownloadBinaries = rootVisualElement.Q<Button>("btn-banner-download-binaries");
            _btnDownloadBinaries = rootVisualElement.Q<Button>("btn-download-binaries");
            _inputYtDlpPath = rootVisualElement.Q<TextField>("input-ytdlp-path");
            _btnBrowseYtDlp = rootVisualElement.Q<Button>("btn-browse-ytdlp");
            _inputFfmpegPath = rootVisualElement.Q<TextField>("input-ffmpeg-path");
            _btnBrowseFfmpeg = rootVisualElement.Q<Button>("btn-browse-ffmpeg");

            _inputVideoUrl = rootVisualElement.Q<TextField>("input-video-url");
            _btnFetchMetadata = rootVisualElement.Q<Button>("btn-fetch-metadata");

            _metadataCard = rootVisualElement.Q<VisualElement>("metadata-card");
            _metadataThumbnail = rootVisualElement.Q<Image>("metadata-thumbnail");
            _metadataTitle = rootVisualElement.Q<Label>("metadata-title");
            _metadataAuthor = rootVisualElement.Q<Label>("metadata-author");
            _metadataDuration = rootVisualElement.Q<Label>("metadata-duration");

            _sectionImportOptions = rootVisualElement.Q<VisualElement>("section-import-options");
            _dropdownFormat = rootVisualElement.Q<DropdownField>("dropdown-format");
            _dropdownBitrate = rootVisualElement.Q<DropdownField>("dropdown-bitrate");
            _inputTargetFolder = rootVisualElement.Q<TextField>("input-target-folder");
            _btnBrowseTargetFolder = rootVisualElement.Q<Button>("btn-browse-target-folder");
            _inputCustomFilename = rootVisualElement.Q<TextField>("input-custom-filename");
            _btnImportAudio = rootVisualElement.Q<Button>("btn-import-audio");

            _sectionTrimming = rootVisualElement.Q<VisualElement>("section-trimming");
            _waveformTrimView = rootVisualElement.Q<WaveformTrimView>("waveform-trim-view");
            _inputTrimStart = rootVisualElement.Q<FloatField>("input-trim-start");
            _inputTrimEnd = rootVisualElement.Q<FloatField>("input-trim-end");
            _labelTrimDuration = rootVisualElement.Q<Label>("label-trim-duration");
            _toggleLoopPreview = rootVisualElement.Q<Toggle>("toggle-loop-preview");
            _btnPlayPreview = rootVisualElement.Q<Button>("btn-play-preview");
            _btnSaveTrimmed = rootVisualElement.Q<Button>("btn-save-trimmed");

            _sectionProgress = rootVisualElement.Q<VisualElement>("section-progress");
            _progressBar = rootVisualElement.Q<ProgressBar>("import-progress-bar");
            _labelStatus = rootVisualElement.Q<Label>("label-status");
            _btnCancelOperation = rootVisualElement.Q<Button>("btn-cancel-operation");
        }

        private void InitializeInputs()
        {
            _dropdownFormat.choices = new List<string> { "WAV (Uncompressed)", "MP3 (Standard)", "OGG (Vorbis)" };
            _dropdownFormat.value = _dropdownFormat.choices[0];

            _dropdownBitrate.choices = new List<string> { "128 kbps", "192 kbps (Recommended)", "256 kbps", "320 kbps (Highest)" };
            _dropdownBitrate.value = _dropdownBitrate.choices[1];

            _dropdownFormat.RegisterValueChangedCallback(evt =>
            {
                var isCompressed = evt.newValue.StartsWith("MP3") || evt.newValue.StartsWith("OGG");
                _dropdownBitrate.style.display = isCompressed ? DisplayStyle.Flex : DisplayStyle.None;
            });
            _dropdownBitrate.style.display = DisplayStyle.None;

            if (_binaryResolver.TryGetYtDlpPath(out var ytdlpPath))
            {
                _inputYtDlpPath.value = ytdlpPath;
            }
            if (_binaryResolver.TryGetFfmpegPath(out var ffmpegPath))
            {
                _inputFfmpegPath.value = ffmpegPath;
            }
        }

        private void RegisterEventHandlers()
        {
            _btnBrowseYtDlp.clicked += () =>
            {
                var selected = EditorUtility.OpenFilePanel("Select yt-dlp Executable", "", "exe,*");
                if (!string.IsNullOrEmpty(selected))
                {
                    _inputYtDlpPath.value = selected;
                    _binaryResolver.SetCustomYtDlpPath(selected);
                    RefreshBinaryStatus();
                }
            };

            _btnBrowseFfmpeg.clicked += () =>
            {
                var selected = EditorUtility.OpenFilePanel("Select ffmpeg Executable", "", "exe,*");
                if (!string.IsNullOrEmpty(selected))
                {
                    _inputFfmpegPath.value = selected;
                    _binaryResolver.SetCustomFfmpegPath(selected);
                    RefreshBinaryStatus();
                }
            };

            _btnBannerDownloadBinaries.clicked += async () => await DownloadBinariesAsync();
            _btnDownloadBinaries.clicked += async () => await DownloadBinariesAsync();

            _inputYtDlpPath.RegisterValueChangedCallback(evt =>
            {
                if (File.Exists(evt.newValue))
                {
                    _binaryResolver.SetCustomYtDlpPath(evt.newValue);
                    RefreshBinaryStatus();
                }
            });

            _inputFfmpegPath.RegisterValueChangedCallback(evt =>
            {
                if (File.Exists(evt.newValue))
                {
                    _binaryResolver.SetCustomFfmpegPath(evt.newValue);
                    RefreshBinaryStatus();
                }
            });

            _btnBrowseTargetFolder.clicked += () =>
            {
                var folder = EditorUtility.OpenFolderPanel("Select Target Project Folder", "Assets", "");
                if (!string.IsNullOrEmpty(folder))
                {
                    var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), folder).Replace('\\', '/');
                    _inputTargetFolder.value = relative;
                }
            };

            _btnFetchMetadata.clicked += async () => await FetchMetadataAsync();
            _btnImportAudio.clicked += async () => await ImportAudioAsync();
            _btnSaveTrimmed.clicked += async () => await SaveTrimmedAudioAsync();
            _btnCancelOperation.clicked += CancelCurrentOperation;

            _waveformTrimView.OnTrimChanged += (start, end) =>
            {
                _inputTrimStart.SetValueWithoutNotify(start);
                _inputTrimEnd.SetValueWithoutNotify(end);
                UpdateTrimDurationLabel();
            };

            _inputTrimStart.RegisterValueChangedCallback(evt =>
            {
                _waveformTrimView.TrimStart = evt.newValue;
                UpdateTrimDurationLabel();
            });

            _inputTrimEnd.RegisterValueChangedCallback(evt =>
            {
                _waveformTrimView.TrimEnd = evt.newValue;
                UpdateTrimDurationLabel();
            });

            _btnPlayPreview.clicked += ToggleAudioPreview;
        }

        private void RefreshBinaryStatus()
        {
            var hasYtDlp = _binaryResolver.TryGetYtDlpPath(out _);
            var hasFfmpeg = _binaryResolver.TryGetFfmpegPath(out _);

            _binaryStatusBadge.ClearClassList();
            _binaryStatusBadge.AddToClassList("status-badge");

            if (hasYtDlp && hasFfmpeg)
            {
                _binaryStatusBadge.text = "Binaries Ready (yt-dlp + ffmpeg)";
                _binaryStatusBadge.AddToClassList("status-badge--ready");
                if (_binaryMissingBanner != null) _binaryMissingBanner.style.display = DisplayStyle.None;
            }
            else if (hasYtDlp || hasFfmpeg)
            {
                _binaryStatusBadge.text = hasYtDlp ? "Missing ffmpeg" : "Missing yt-dlp";
                _binaryStatusBadge.AddToClassList("status-badge--warning");
                if (_binaryMissingBanner != null) _binaryMissingBanner.style.display = DisplayStyle.Flex;
            }
            else
            {
                _binaryStatusBadge.text = "Binaries Not Found";
                _binaryStatusBadge.AddToClassList("status-badge--error");
                if (_binaryMissingBanner != null) _binaryMissingBanner.style.display = DisplayStyle.Flex;
            }
        }

        private async Task FetchMetadataAsync()
        {
            var url = _inputVideoUrl.value;
            if (string.IsNullOrWhiteSpace(url))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a valid YouTube video URL.", "OK");
                return;
            }

            SetBusyState(true, "Fetching video metadata via yt-dlp...");

            try
            {
                _currentMetadata = await _ytDlpService.GetMetadataAsync(url, _cts.Token);

                _metadataTitle.text = _currentMetadata.Title;
                _metadataAuthor.text = $"Author: {_currentMetadata.Author}";
                _metadataDuration.text = $"Duration: {_currentMetadata.FormattedDuration}";
                _metadataCard.style.display = DisplayStyle.Flex;
                _sectionImportOptions.style.display = DisplayStyle.Flex;

                if (!string.IsNullOrWhiteSpace(_currentMetadata.ThumbnailUrl))
                {
                    try
                    {
                        var texture = await _thumbnailLoader.LoadThumbnailAsync(_currentMetadata.ThumbnailUrl, _cts.Token);
                        _metadataThumbnail.image = texture;
                    }
                    catch (Exception thumbEx)
                    {
                        Debug.LogWarning($"[YouTubeAudioImporter] Failed to load thumbnail image: {thumbEx.Message}");
                    }
                }

                SetStatus("Metadata loaded successfully.");
            }
            catch (OperationCanceledException)
            {
                SetStatus("Operation canceled by user.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[YouTubeAudioImporter] {ex}");
                EditorUtility.DisplayDialog("Metadata Fetch Error", ex.Message, "OK");
                SetStatus($"Error: {ex.Message}");
            }
            finally
            {
                SetBusyState(false);
            }
        }

        private async Task ImportAudioAsync()
        {
            if (_currentMetadata == null)
            {
                EditorUtility.DisplayDialog("No Metadata", "Please fetch video metadata first.", "OK");
                return;
            }

            if (!LegalDisclaimerModal.PromptUserIfNecessary())
            {
                SetStatus("Import canceled: legal disclaimer not accepted.");
                return;
            }

            var format = _dropdownFormat.value switch
            {
                var s when s.StartsWith("MP3") => AudioFormat.Mp3,
                var s when s.StartsWith("OGG") => AudioFormat.Ogg,
                _ => AudioFormat.Wav
            };

            var bitrate = _dropdownBitrate.value switch
            {
                var s when s.StartsWith("128") => 128,
                var s when s.StartsWith("256") => 256,
                var s when s.StartsWith("320") => 320,
                _ => 192
            };

            var request = new ImportRequest(
                _inputVideoUrl.value,
                format,
                _inputTargetFolder.value,
                _inputCustomFilename.value,
                audioBitrateKbps: bitrate
            );

            SetBusyState(true, "Downloading and importing audio...");
            var progressReporter = new Progress<string>(msg => SetStatus(msg));

            try
            {
                var result = await _importPipeline.ExecuteImportAsync(request, progressReporter, _cts.Token);
                if (result.Success)
                {
                    _currentLoadedClip = result.LoadedAudioClip;
                    _currentAssetPath = result.AssetPath;

                    _sectionTrimming.style.display = DisplayStyle.Flex;
                    _waveformTrimView.SetAudioClip(_currentLoadedClip);
                    _inputTrimStart.value = 0f;
                    _inputTrimEnd.value = _currentLoadedClip.length;
                    UpdateTrimDurationLabel();

                    EditorGUIUtility.PingObject(_currentLoadedClip);
                    SetStatus($"Successfully imported '{_currentLoadedClip.name}'!");
                }
                else
                {
                    EditorUtility.DisplayDialog("Import Error", result.ErrorMessage, "OK");
                    SetStatus($"Import failed: {result.ErrorMessage}");
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("Import operation canceled.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[YouTubeAudioImporter] {ex}");
                EditorUtility.DisplayDialog("Import Error", ex.Message, "OK");
                SetStatus($"Import error: {ex.Message}");
            }
            finally
            {
                SetBusyState(false);
            }
        }

        private async Task SaveTrimmedAudioAsync()
        {
            if (_currentLoadedClip == null || string.IsNullOrWhiteSpace(_currentAssetPath))
            {
                EditorUtility.DisplayDialog("Error", "No imported asset loaded to trim.", "OK");
                return;
            }

            var start = _waveformTrimView.TrimStart;
            var end = _waveformTrimView.TrimEnd;

            if (end <= start)
            {
                EditorUtility.DisplayDialog("Invalid Range", "Trim end point must be greater than start point.", "OK");
                return;
            }

            SetBusyState(true, $"Trimming audio ({start:F2}s - {end:F2}s)...");
            var progressReporter = new Progress<string>(msg => SetStatus(msg));

            try
            {
                var result = await _importPipeline.TrimAndSaveAsync(
                    _currentAssetPath,
                    start,
                    end,
                    _inputTargetFolder.value,
                    progress: progressReporter,
                    cancellationToken: _cts.Token
                );

                if (result.Success)
                {
                    EditorGUIUtility.PingObject(result.LoadedAudioClip);
                    SetStatus($"Trimmed asset saved as '{result.LoadedAudioClip.name}'!");
                }
                else
                {
                    EditorUtility.DisplayDialog("Trim Error", result.ErrorMessage, "OK");
                    SetStatus($"Trim failed: {result.ErrorMessage}");
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("Trimming canceled.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[YouTubeAudioImporter] {ex}");
                EditorUtility.DisplayDialog("Trim Error", ex.Message, "OK");
                SetStatus($"Trim error: {ex.Message}");
            }
            finally
            {
                SetBusyState(false);
            }
        }

        private void ToggleAudioPreview()
        {
            if (_currentLoadedClip == null) return;

            if (_isPlayingPreview)
            {
                StopAudioPreview();
            }
            else
            {
                StartAudioPreview();
            }
        }

        private void StartAudioPreview()
        {
            if (_currentLoadedClip == null) return;

            var start = _waveformTrimView.TrimStart;
            var end = _waveformTrimView.TrimEnd;
            var duration = end - start;

            if (duration <= 0.01f) return;

            var startSample = Mathf.RoundToInt(start * _currentLoadedClip.frequency);
            EditorAudioPreviewPlayer.StopAll();
            EditorAudioPreviewPlayer.Play(_currentLoadedClip, startSample, loop: false);

            _previewStartTime = EditorApplication.timeSinceStartup;
            _previewStartOffset = start;
            _previewDuration = duration;
            _isPlayingPreview = true;
            _btnPlayPreview.text = "Stop Preview";
            _waveformTrimView.PlayheadTime = start;
        }

        private void StopAudioPreview()
        {
            EditorAudioPreviewPlayer.StopAll();
            _isPlayingPreview = false;
            if (_btnPlayPreview != null)
            {
                _btnPlayPreview.text = "Play Preview";
            }
            if (_waveformTrimView != null)
            {
                _waveformTrimView.PlayheadTime = null;
            }
        }

        private void OnEditorUpdate()
        {
            if (!_isPlayingPreview || _currentLoadedClip == null) return;

            var elapsed = (float)(EditorApplication.timeSinceStartup - _previewStartTime);

            if (elapsed >= _previewDuration)
            {
                if (_toggleLoopPreview != null && _toggleLoopPreview.value)
                {
                    StartAudioPreview();
                }
                else
                {
                    StopAudioPreview();
                }
            }
            else
            {
                _waveformTrimView.PlayheadTime = _previewStartOffset + elapsed;
            }
        }

        private void UpdateTrimDurationLabel()
        {
            var duration = Mathf.Max(0f, _waveformTrimView.TrimEnd - _waveformTrimView.TrimStart);
            _labelTrimDuration.text = $"Trimmed: {duration:F2}s";
        }

        private void SetBusyState(bool busy, string initialStatus = null)
        {
            if (busy)
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
            }

            _btnFetchMetadata.SetEnabled(!busy);
            _btnImportAudio.SetEnabled(!busy);
            _btnSaveTrimmed.SetEnabled(!busy);
            _sectionProgress.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;

            if (!string.IsNullOrWhiteSpace(initialStatus))
            {
                SetStatus(initialStatus);
            }
        }

        private void SetStatus(string message)
        {
            if (_labelStatus != null)
            {
                _labelStatus.text = message;
            }
            if (_progressBar != null)
            {
                _progressBar.title = message;
            }
        }

        private void CancelCurrentOperation()
        {
            try
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                    SetStatus("Canceling operation...");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[YouTubeAudioImporter] Error canceling operation: {ex.Message}");
            }
        }

        private async Task DownloadBinariesAsync()
        {
            SetBusyState(true, "Checking and updating binaries...");
            var progressReporter = new Progress<float>(p =>
            {
                if (_progressBar != null)
                {
                    _progressBar.value = p * 100f;
                }
            });
            var statusReporter = new Progress<string>(msg => SetStatus(msg));

            try
            {
                var result = await _binaryDownloaderService.DownloadOrUpdateBinariesAsync(force: false, progressReporter, statusReporter, _cts.Token);
                RefreshBinaryStatus();

                if (_binaryResolver.TryGetYtDlpPath(out var ytdlpPath))
                {
                    _inputYtDlpPath.value = ytdlpPath;
                }
                if (_binaryResolver.TryGetFfmpegPath(out var ffmpegPath))
                {
                    _inputFfmpegPath.value = ffmpegPath;
                }

                var title = result.AlreadyUpToDate ? "Up to Date" : "Success";
                EditorUtility.DisplayDialog(title, result.Message, "OK");
            }
            catch (OperationCanceledException)
            {
                SetStatus("Binary download canceled.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[YouTubeAudioImporter] Binary download failed: {ex}");
                EditorUtility.DisplayDialog("Download Error", $"Failed to download binaries: {ex.Message}", "OK");
                SetStatus($"Download error: {ex.Message}");
            }
            finally
            {
                SetBusyState(false);
            }
        }
    }
}
