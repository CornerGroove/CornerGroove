<p align="center">
  <img src="logo.png" width="128" height="128" alt="CornerGroove Logo" style="border-radius: 24px;" />
</p>

<h1 align="center">CornerGroove</h1>

<p align="center">
  <strong>A minimalist, edge-docked vinyl music widget &amp; per-application volume mixer for Windows 10 &amp; 11</strong><br>
  <em>Crafted with Apple and Vercel design aesthetics — Flat soft UI, smooth theme transitions, and left-edge blurred radial aura.</em>
</p>

<p align="center">
  <a href="https://github.com/CornerGroove/CornerGroove/releases/tag/v1.0.0"><img src="https://img.shields.io/badge/release-v1.0.0-10B981?style=flat-square" alt="Release v1.0.0"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue?style=flat-square" alt="License MIT"></a>
  <img src="https://img.shields.io/badge/.NET-9.0-purple?style=flat-square" alt=".NET 9.0">
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4?style=flat-square" alt="Windows 10/11">
</p>

---

Tucked discreetly into the bottom-right corner of your screen, **CornerGroove** stays hidden until you slide your cursor into the corner—smoothly gliding into view with your active music playback, spinning vinyl disc artwork, blurred radial aura glow, and a complete system per-app volume mixer.

---

## 📦 Downloads (v1.0.0 Release)

Grab the precompiled release directly from the repository:
- **Direct Executable:** [`releases/v1.0.0/CornerGroove.exe`](releases/v1.0.0/CornerGroove.exe)
- **Portable Zip Archive:** [`releases/v1.0.0/CornerGroove-v1.0.0-windows-x64.zip`](releases/v1.0.0/CornerGroove-v1.0.0-windows-x64.zip)

---

## ✨ Features

- **Abstract Radial Blurred Aura (Left Edge Glow)**:
  - Emanates from behind the spinning vinyl disc on the left side of the widget card.
  - Atmospheric dual-orb radial gradient that dynamically matches your active theme palette (Emerald, Cyan, Violet, Caramel).
  - Cleanly clipped to the card's 14px rounded corners—no harsh boxes or overflow artifacts.

- **Smooth Animated Theme Transitions**:
  - Silky 60fps color interpolation across all theme brushes and aura gradients.
  - Zero abrupt color snapping when switching themes—everything glides effortlessly into the new colorway.

- **Corner Edge-Slide Trigger**:
  - Sits tucked against the right screen edge with a subtle 4px handle.
  - Hovering into the bottom-right screen corner or handle triggers a smooth cubic ease-out slide-in.
  - Auto-hides after 1.3 seconds when cursor leaves (unless pinned).
  - `ShowActivated="False"` ensures it never steals focus from fullscreen games or active coding windows.

- **Vinyl Record & Sleeve Animation**:
  - Displays the album jacket / artwork with rounded corners.
  - Half of the vinyl record sticks out with grooved tracks and center label.
  - **Spins in real-time** when playing (`IsPlaying`), and freezes smoothly in place when paused.

- **3-Bar Sound Wave Visualizer**:
  - Dynamic 3-bar sound wave in the header.
  - Animated heights when music is playing, settles and dims when stopped.

- **Per-Application Volume Mixer (WASAPI Core Audio)**:
  - Hover or click the **Volume icon (🔊)** to reveal the upper **Mixer Panel**.
  - **Master Volume Slider** with one-click Mute toggle.
  - **App Mixer**: Lists only active audio-producing applications (e.g., Spotify, Chrome, Firefox, Discord, games) with their high-resolution icons, individual volume sliders, and mute toggles.
  - No bloated background processes—only actual audio sessions!

- **"Open Last Played" Memory**:
  - Remembers the last played song title, artist, and source application (Spotify, YouTube, Apple Music, etc.).
  - When nothing is playing, displays an **"Open [App]"** button that immediately launches or focuses the app/service so you can resume listening in one click.

- **Theme Engine (4 Built-in Themes)**:
  - 🌙 **Dark** (Default Vercel & Apple deep obsidian `#0C0C0E` + Emerald `#10B981`)
  - ☀️ **Light** (Clean Apple crisp silver `#F4F4F6` + Teal `#0D9488`)
  - 🧛 **Dracula** (Iconic `#282A36` + Purple `#BD93F9` & Green `#50FA7B`)
  - ☕ **Cappuccino** (Warm cozy espresso & caramel tones `#2A231F` + `#D4A373`)

- **Integrated Settings View with iOS-Style Switches**:
  - Click the **Gear (⚙)** icon to open the settings view directly in the upper card.
  - Clean brand header with official CornerGroove logo and version badge.
  - **iOS-style tactile toggle switches** for "Always on Top" and "Start with Windows".

- **Clean Custom Context Menu**:
  - Custom styled WPF tray context menu—zero white icon gutters!
  - Minimalist rounded card design matching your active theme.

- **Universal Media Capture (Windows SMTC)**:
  - Native integration with `GlobalSystemMediaTransportControlsSessionManager`:
    - **Spotify** (Desktop & Web)
    - **YouTube / YouTube Music** (Chrome, Edge, Brave, Firefox, Opera)
    - **Apple Music, Tidal, Deezer, VLC, Windows Media Player**

---

## 🎨 Themes Showcase

| Theme | Background | Card | Aura Accents | Text |
| :--- | :--- | :--- | :--- | :--- |
| **Dark** | `#0C0C0E` | `#131317` | Emerald `#10B981` • Cyan `#06B6D4` | `#EDEDED` |
| **Light** | `#F4F4F6` | `#FFFFFF` | Teal `#0D9488` • Sky `#38BDF8` | `#18181B` |
| **Dracula** | `#1E1F29` | `#282A36` | Purple `#BD93F9` • Pink `#FF79C6` | `#F8F8F2` |
| **Cappuccino** | `#1E1916` | `#2A231F` | Caramel `#D4A373` • Amber `#F59E0B` | `#FDFBF7` |

---

## 🚀 Getting Started

### Run Precompiled Release
Simply run:
```powershell
.\releases\v1.0.0\CornerGroove.exe
```

### Build from Source
Requirements: [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) on Windows 10/11.

```powershell
git clone https://github.com/CornerGroove/CornerGroove.git
cd CornerGroove
dotnet build -c Release
dotnet run -c Release
```

---

## ⌨️ Shortcuts & Controls

- **Hover bottom-right screen edge**: Slides widget into view.
- **Pin Icon (📌)**: Keeps widget permanently visible on top of all windows.
- **Volume Icon (🔊)**: Toggles the master and per-application volume mixer panel.
- **Gear Icon (⚙)**: Opens Settings & Theme selector with iOS toggle switches.
- **Click progress bar**: Seeks forward or backward in the track.
- **System Tray Icon**: Right-click for context menu; left-click to toggle widget.

---

## 📄 License

MIT License — see [LICENSE](LICENSE) for details.
