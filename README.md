# YouTube Audio Importer for Unity

An Editor-only extension for Unity that allows game developers and sound designers to download, trim, and import audio clips directly from YouTube into their project assets.

Powered by `yt-dlp` and `ffmpeg`, featuring an interactive UI Toolkit waveform trimmer with live loop preview.

---

## Features

- **Direct YouTube Import**: Paste any YouTube link to fetch metadata (title, duration, channel, thumbnail).
- **Interactive Waveform Editor**:
  - Visual waveform rendering powered by UI Toolkit Painter2D.
  - Draggable **Trim Start** and **Trim End** handles.
  - Live audio preview bounded by trim handles.
  - **Loop Preview** toggle to easily test seamless audio loops before importing.
- **Multiple Audio Formats**: Export audio clips as **WAV**, **MP3**, **OGG**, or **FLAC**.
- **Smart Binary Management**:
  - One-click binary downloader for `yt-dlp` and `ffmpeg`.
  - Automatically checks local vs. latest remote versions to avoid redundant downloads.
  - Portable, self-contained binaries stored in `ThirdParty~` (does not bloat your Git repository).
- **Unity 6 & Modern C# Ready**:
  - Fully asynchronous non-blocking background operations (`async/await`).
  - Native integration with Unity's AssetDatabase.

---

## Requirements

- **Unity**: 6000.0+ (compatible with Unity 2022.3 LTS+)
- **Operating System**: Windows (x64) supported out of the box with portable binaries.
- **Dependencies**: `yt-dlp` and `ffmpeg` (can be downloaded automatically via the tool window).

---

## Installation

### Option 1: Via Unity Package Manager (Git URL)

1. In Unity, open **Window** > **Package Manager**.
2. Click the **+** (plus) button in the upper-left corner.
3. Select **Add package from git URL...**.
4. Enter the Git repository URL:
   ```text
   https://github.com/donsledzion/unity-yt-audio-importer.git
   ```
5. Click **Add**.

### Option 2: Local Embedded Package

1. Clone or copy this repository into your project's `Packages/` directory:
   ```text
   YourUnityProject/Packages/com.softaware.youtube-audio-importer
   ```
2. Unity will automatically detect and load the package.

---

## Quick Start

1. Open the importer window from the top menu:  
   **Window** > **Audio** > **YouTube Audio Importer**.
2. If this is your first time using the tool, click **Download / Update Binaries** in the top banner to automatically install `yt-dlp` and `ffmpeg`.
3. Paste a YouTube URL into the input field and click **Fetch Info**.
4. Use the **Waveform Trim View** to set your start and end points:
   - Drag the green **Start** handle and red **End** handle.
   - Click **Play / Pause** to audition the segment.
   - Toggle **Loop** to verify loopable sound effects or background music.
5. Select your target **Output Folder**, **File Name**, and **Audio Format**.
6. Click **Import Audio Clip**. Once imported, the new `AudioClip` will automatically ping in your Project window.

---

## Repository Structure

```text
├── Editor/
│   ├── Common/              # Process runners, exceptions, format models
│   ├── Services/            # YtDlp, Ffmpeg, BinaryDownloader, ImportPipeline
│   ├── UI/                  # UI Toolkit Window, WaveformTrimView, UXML, USS
│   └── ThirdParty~/         # Downloaded yt-dlp & ffmpeg binaries (gitignored)
├── package.json             # UPM package manifest
└── README.md
```

---

## Disclaimer & Terms of Use

This package is intended for educational, testing, prototyping, and personal workflow use with royalty-free, Creative Commons, or user-owned audio content. Please respect copyright laws and the Terms of Service of YouTube and respective content creators.

---

## License

MIT License. See [LICENSE.md](LICENSE.md) for details.
