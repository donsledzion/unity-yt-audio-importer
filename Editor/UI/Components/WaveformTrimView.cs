using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SoftAware.YouTubeAudioImporter.Editor.UI.Components
{
    [UxmlElement]
    public partial class WaveformTrimView : VisualElement
    {

        private AudioClip _clip;
        private float[] _peaks = Array.Empty<float>();
        private float _trimStart;
        private float _trimEnd;
        private float? _playheadTime;

        private bool _isDraggingStart;
        private bool _isDraggingEnd;

        public float? PlayheadTime
        {
            get => _playheadTime;
            set
            {
                if (_playheadTime != value)
                {
                    _playheadTime = value;
                    MarkDirtyRepaint();
                }
            }
        }

        public event Action<float, float> OnTrimChanged;

        public float TrimStart
        {
            get => _trimStart;
            set
            {
                var clamped = Mathf.Clamp(value, 0f, _clip != null ? _clip.length : 0f);
                if (Math.Abs(_trimStart - clamped) > 0.001f)
                {
                    _trimStart = clamped;
                    if (_trimEnd < _trimStart) _trimEnd = _trimStart;
                    MarkDirtyRepaint();
                    OnTrimChanged?.Invoke(_trimStart, _trimEnd);
                }
            }
        }

        public float TrimEnd
        {
            get => _trimEnd;
            set
            {
                var maxLen = _clip != null ? _clip.length : 0f;
                var clamped = Mathf.Clamp(value, _trimStart, maxLen);
                if (Math.Abs(_trimEnd - clamped) > 0.001f)
                {
                    _trimEnd = clamped;
                    MarkDirtyRepaint();
                    OnTrimChanged?.Invoke(_trimStart, _trimEnd);
                }
            }
        }

        public WaveformTrimView()
        {
            style.height = 100;
            style.minHeight = 80;
            style.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            style.borderBottomLeftRadius = 6;
            style.borderBottomRightRadius = 6;
            style.borderTopLeftRadius = 6;
            style.borderTopRightRadius = 6;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftColor = new Color(0.25f, 0.25f, 0.28f);
            style.borderRightColor = new Color(0.25f, 0.25f, 0.28f);
            style.borderTopColor = new Color(0.25f, 0.25f, 0.28f);
            style.borderBottomColor = new Color(0.25f, 0.25f, 0.28f);

            generateVisualContent += OnGenerateVisualContent;

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        public void SetAudioClip(AudioClip clip)
        {
            _clip = clip;
            if (_clip == null)
            {
                _peaks = Array.Empty<float>();
                _trimStart = 0f;
                _trimEnd = 0f;
                MarkDirtyRepaint();
                return;
            }

            _trimStart = 0f;
            _trimEnd = _clip.length;
            _playheadTime = null;
            ComputePeaks();
            MarkDirtyRepaint();
            OnTrimChanged?.Invoke(_trimStart, _trimEnd);
        }

        private void ComputePeaks()
        {
            if (_clip == null || _clip.samples == 0)
            {
                _peaks = Array.Empty<float>();
                return;
            }

            const int barCount = 180;
            _peaks = new float[barCount];

            var totalSamples = _clip.samples * _clip.channels;
            var step = Math.Max(1, totalSamples / barCount);
            var buffer = new float[Math.Min(totalSamples, 44100 * 2)];

            // Sample in chunks to handle large files without massive allocations
            var samplesRead = 0;
            var currentBar = 0;
            var samplesSinceLastBar = 0;
            var maxPeakInBar = 0f;

            while (samplesRead < totalSamples && currentBar < barCount)
            {
                var toRead = Math.Min(buffer.Length, totalSamples - samplesRead);
                _clip.GetData(buffer, samplesRead / _clip.channels);

                for (var i = 0; i < toRead; i++)
                {
                    var abs = Mathf.Abs(buffer[i]);
                    if (abs > maxPeakInBar)
                    {
                        maxPeakInBar = abs;
                    }

                    samplesSinceLastBar++;
                    if (samplesSinceLastBar >= step && currentBar < barCount)
                    {
                        _peaks[currentBar] = maxPeakInBar;
                        currentBar++;
                        samplesSinceLastBar = 0;
                        maxPeakInBar = 0f;
                    }
                }

                samplesRead += toRead;
            }
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var width = contentRect.width;
            var height = contentRect.height;

            if (width <= 0 || height <= 0) return;

            var painter = mgc.painter2D;

            if (_peaks == null || _peaks.Length == 0 || _clip == null)
            {
                // Empty placeholder
                return;
            }

            var midY = height / 2f;
            var barWidth = width / _peaks.Length;

            // Draw waveform bars
            painter.strokeColor = new Color(0.24f, 0.65f, 0.95f, 0.85f);
            painter.lineWidth = Math.Max(1f, barWidth - 1f);

            for (var i = 0; i < _peaks.Length; i++)
            {
                var x = i * barWidth + barWidth * 0.5f;
                var peakHeight = Mathf.Clamp(_peaks[i] * (height * 0.85f), 2f, height);
                var halfPeak = peakHeight * 0.5f;

                painter.BeginPath();
                painter.MoveTo(new Vector2(x, midY - halfPeak));
                painter.LineTo(new Vector2(x, midY + halfPeak));
                painter.Stroke();
            }

            // Draw dim overlays outside trimmed region
            var totalDuration = _clip.length;
            if (totalDuration > 0f)
            {
                var startX = (_trimStart / totalDuration) * width;
                var endX = (_trimEnd / totalDuration) * width;

                // Left dimmed area
                if (startX > 0)
                {
                    painter.fillColor = new Color(0f, 0f, 0f, 0.5f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(0, 0));
                    painter.LineTo(new Vector2(startX, 0));
                    painter.LineTo(new Vector2(startX, height));
                    painter.LineTo(new Vector2(0, height));
                    painter.ClosePath();
                    painter.Fill();
                }

                // Right dimmed area
                if (endX < width)
                {
                    painter.fillColor = new Color(0f, 0f, 0f, 0.5f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(endX, 0));
                    painter.LineTo(new Vector2(width, 0));
                    painter.LineTo(new Vector2(width, height));
                    painter.LineTo(new Vector2(endX, height));
                    painter.ClosePath();
                    painter.Fill();
                }

                // Start Handle (Green marker)
                painter.strokeColor = new Color(0.2f, 0.85f, 0.3f, 1f);
                painter.lineWidth = 3f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(startX, 0));
                painter.LineTo(new Vector2(startX, height));
                painter.Stroke();

                // End Handle (Red/Coral marker)
                painter.strokeColor = new Color(0.95f, 0.3f, 0.3f, 1f);
                painter.lineWidth = 3f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(endX, 0));
                painter.LineTo(new Vector2(endX, height));
                painter.Stroke();

                // Playhead Needle (Vibrant Yellow)
                if (_playheadTime.HasValue && _playheadTime.Value >= 0f && _playheadTime.Value <= totalDuration)
                {
                    var playheadX = (_playheadTime.Value / totalDuration) * width;
                    painter.strokeColor = new Color(1f, 0.92f, 0.23f, 1f);
                    painter.lineWidth = 2f;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(playheadX, 0));
                    painter.LineTo(new Vector2(playheadX, height));
                    painter.Stroke();
                }
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (_clip == null || _clip.length <= 0f) return;

            var width = contentRect.width;
            if (width <= 0) return;

            var clickTime = (evt.localPosition.x / width) * _clip.length;
            var distStart = Math.Abs(clickTime - _trimStart);
            var distEnd = Math.Abs(clickTime - _trimEnd);

            // Determine if closer to start or end handle
            if (distStart < distEnd)
            {
                _isDraggingStart = true;
                TrimStart = clickTime;
            }
            else
            {
                _isDraggingEnd = true;
                TrimEnd = clickTime;
            }

            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_clip == null || (!this.HasPointerCapture(evt.pointerId))) return;

            var width = contentRect.width;
            if (width <= 0) return;

            var time = Mathf.Clamp((evt.localPosition.x / width) * _clip.length, 0f, _clip.length);

            if (_isDraggingStart)
            {
                TrimStart = Mathf.Min(time, _trimEnd - 0.05f);
            }
            else if (_isDraggingEnd)
            {
                TrimEnd = Mathf.Max(time, _trimStart + 0.05f);
            }

            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (this.HasPointerCapture(evt.pointerId))
            {
                this.ReleasePointer(evt.pointerId);
            }
            _isDraggingStart = false;
            _isDraggingEnd = false;
            evt.StopPropagation();
        }
    }

    public static class EditorAudioPreviewPlayer
    {
        private static MethodInfo _playPreviewClipMethod;
        private static MethodInfo _stopAllPreviewClipsMethod;
        private static bool _reflectionInitialized;

        private static void EnsureInitialized()
        {
            if (_reflectionInitialized) return;
            _reflectionInitialized = true;

            try
            {
                var unityEditorAssembly = typeof(AudioImporter).Assembly;
                var audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
                if (audioUtilClass != null)
                {
                    _playPreviewClipMethod = audioUtilClass.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
                    _stopAllPreviewClipsMethod = audioUtilClass.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[YouTubeAudioImporter] AudioUtil reflection init failed: {ex.Message}");
            }
        }

        public static void Play(AudioClip clip, int startSample = 0, bool loop = false)
        {
            if (clip == null) return;
            EnsureInitialized();

            try
            {
                _playPreviewClipMethod?.Invoke(null, new object[] { clip, startSample, loop });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[YouTubeAudioImporter] Failed to play audio preview: {ex.Message}");
            }
        }

        public static void StopAll()
        {
            EnsureInitialized();

            try
            {
                _stopAllPreviewClipsMethod?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[YouTubeAudioImporter] Failed to stop audio preview: {ex.Message}");
            }
        }
    }
}
