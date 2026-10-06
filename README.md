<p align="center">
  <img src="Assets/pouchy-256.png" alt="Pouchy Logo" width="110" />
</p>

<h1 align="center">Pouchy</h1>

<p align="center">
  <strong>The cute, lightning-fast drop shelf for Windows 10 & 11.</strong><br />
  A temporary parking spot for your files, photos, links, and snippets — summoned with a flick of your mouse.<br />
  <em>The modern, open-source Dropover & Yoink alternative built natively for Windows.</em>
</p>

<p align="center">
  <a href="https://dotnet.microsoft.com/download/dotnet/8.0"><img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet&logoColor=white" alt=".NET 8" /></a>
  <a href="https://learn.microsoft.com/windows/"><img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D4?style=flat&logo=windows&logoColor=white" alt="Windows 10 / 11" /></a>
  <a href="#-test-suite"><img src="https://img.shields.io/badge/Tests-129%20Passing-10B981?style=flat" alt="129 Tests Passing" /></a>
  <a href="#-license"><img src="https://img.shields.io/badge/License-PolyForm%20Noncommercial-8B7CFF?style=flat" alt="PolyForm Noncommercial" /></a>
  <a href="https://buymeacoffee.com/notrishi"><img src="https://img.shields.io/badge/Buy%20Me%20a%20Coffee-Support-FFDD00?style=flat&logo=buy-me-a-coffee&logoColor=black" alt="Support on Buy Me a Coffee" /></a>
  <a href="#-technical-architecture-built-the-right-way"><img src="https://img.shields.io/badge/Telemetry-Zero-black?style=flat" alt="Zero Telemetry" /></a>
</p>

<p align="center">
  <img src="docs/media/demo.gif" alt="Pouchy in Action - Windows Drop Shelf" width="860" />
</p>

---

## 💡 Why Pouchy? (Dropover for Windows)

Moving files around on Windows has always been painful. You pick up a file in File Explorer, awkwardly Alt-Tab across four windows, accidentally drop it into the wrong folder, or email photos to yourself just to move them between workspaces.

macOS users have had **Dropover** and **Yoink** for a decade. Windows users were left with clunky clipboard managers or nothing at all.

**Pouchy brings the effortless drop shelf to Windows:**
1. **Grab any file, image, URL, or text snippet.**
2. **Give your mouse a little shake** (or tap your global hotkey).
3. **Drop it into Pouchy.** Your files stay safely parked in an unobtrusive, floating shelf while you navigate anywhere.
4. **Drag them out** whenever and wherever you need them — into Discord, Slack, Photoshop, Premiere, or another folder.

---

## 🍏 Comparing Pouchy to macOS Yoink & Dropover

| Feature | **Pouchy** (Windows) | **Dropover** (macOS) | **Yoink** (macOS) | Default Windows |
| :--- | :---: | :---: | :---: | :---: |
| **Shake Mouse to Summon** | ✅ Yes | ✅ Yes | ❌ No | ❌ No |
| **Multiple Work Shelves** | ✅ Yes (with 40+ icons) | ❌ Single shelf | ❌ Single shelf | ❌ No |
| **Living Reactive Mascot** | ✅ Yes (4 Moods) | ❌ No | ❌ No | ❌ No |
| **20-Step Undo (<kbd>Ctrl</kbd>+<kbd>Z</kbd>)** | ✅ Yes | ❌ No | ❌ No | ❌ No |
| **Global Search (<kbd>Ctrl</kbd>+<kbd>F</kbd>)** | ✅ Yes | ❌ No | ❌ No | ❌ No |
| **Custom Theme Engine** | ✅ Yes (6 themes + JSON) | ❌ Limited | ❌ Dark/Light only | ❌ No |
| **Native Drag Image & Safe Copy**| ✅ Yes (OLE `IDragSourceHelper`) | ✅ Yes | ✅ Yes | ❌ Destructive move |
| **Memory Footprint** | ⚡ **~35 MB** (Native .NET 8) | ~50 MB | ~40 MB | N/A |
| **Price** | 🟢 **Free for Personal Use** | $4.99+ | $8.99 | N/A |

---

## ✨ Features That Make Pouchy Different

### 🦘 Meet Pouchy the Mascot
Pouchy isn’t a sterile utility widget — it’s a living desktop companion. Crafted with vector animations, Pouchy reacts to your actions live:
* **Idle:** Breathes, blinks, glances around, and wiggles every now and then.
* **Excited:** Mouth wide open and bouncing with sparkles the second you drag a file near it!
* **Happy:** Chomps down on dropped items, hops with joy, and floats a little heart.
* **Puzzled:** Tilts its head with a question mark when search returns zero matches.

<p align="center">
  <img src="docs/media/mascot.png" alt="Pouchy Mascot Moods" width="840" />
</p>

---

### 🗂️ Multiple Shelves & Finder-Style Color Labels
Keep concurrent projects organized without clutter:
* **Multiple Shelves:** Separate your `Work`, `Screenshots`, and `Inspirations`.
* **Switch in a Flash:** Cycle tabs with <kbd>Ctrl</kbd> + <kbd>Tab</kbd> or jump directly with <kbd>Ctrl</kbd> + <kbd>1</kbd>–<kbd>9</kbd>.
* **40+ Fluent Icons & Palette:** Personalize shelf tabs with custom icons and accent tones.
* **Color Labels:** Tag items with colored dots that tint the tile and label badge.

<p align="center">
  <img src="docs/media/organise.png" alt="Search, Shelves & Undo" width="840" />
</p>

---

### 🔍 Instant Search & ↩️ 20-Step Undo Stack
* **Global Search (<kbd>Ctrl</kbd> + <kbd>F</kbd>):** Filter through all your shelves instantly with keyword matching and color-dot filters.
* **20-Step Undo (<kbd>Ctrl</kbd> + <kbd>Z</kbd>):** Accidentally cleared a shelf or removed the wrong item? Hit undo and watch it slide right back.

---

### 🎨 6 Handcrafted Themes + Live Hot-Reloading
Pouchy ships with 6 curated visual styles and adapts between **Grid**, **List**, and **Compact** density modes:

* **Midnight Glass:** Deep acrylic dark mode with glowing accents.
* **Frost:** Translucent icy glass with crisp highlights.
* **Sunset:** Warm twilight gradient vibes.
* **Paper:** Flat, tactile minimalism with soft physical shadows.
* **Terminal:** Retro monospace typography with green CRT scanlines.
* **Follow Windows:** Syncs live with your system accent color and light/dark theme.

*Want to build your own? Themes are simple `.json` files in `%AppData%\Pouchy\themes\` that hot-reload the millisecond you hit Save. See the [Theming Guide](docs/THEMES.md).*

<p align="center">
  <img src="docs/media/themes.png" alt="Pouchy Themes" width="840" />
</p>

<p align="center">
  <img src="docs/media/views.png" alt="Pouchy View Modes" width="840" />
</p>

---

### 🪄 Supercharged Right-Click Context Menus
Right-click any tile for instant micro-actions tailored to the file type:
* **Images:** Instant 50% / 25% resize, convert PNG/JPG, run local OCR text extraction, or set as wallpaper.
* **Files:** Quick Look preview (<kbd>Space</kbd>), Open With, Show in Explorer, Zip, or Extract archives.
* **Text & Links:** Edit notes inline, tidy whitespace, open URLs, and preview OpenGraph cards.
* **Colors:** Drop any HEX code (like `#FF8800`) to create an interactive color card that copies as HEX, RGB, or HSL.
* **Windows Shell "More Options":** Directly opens the native Explorer shell menu (`7-Zip`, `Git`, `Send To`, etc.) with full owner-drawn submenu support.

<p align="center">
  <img src="docs/media/menus.png" alt="Pouchy Context Menus" width="840" />
</p>

---

### 🛡️ Real Windows Drag Integration (No Destroyed Files)
* **Native Drag Images:** When dragging files out of Pouchy, Windows displays high-res thumbnail cards with item count badges under your cursor using COM `IDragSourceHelper`.
* **Safe Same-Drive Drops:** Pouchy sets `Preferred DropEffect` on outgoing drags. When you drop files onto the same drive in Explorer, it **copies** by default instead of Windows' default destructive move.

---

## 🎯 Real-World Workflows

* 🎨 **Designers & Video Editors:** Drag 15 raw footage files and brand assets into Pouchy, open Premiere / Photoshop, and drag them in one by one without cluttering your desktop.
* 💻 **Developers:** Park GitHub PR links, snippets, API response JSONs, and test images while switching branches.
* ✍️ **Writers & Students:** Collect reference PDFs, quotes, and research URLs on a dedicated `Research` shelf while drafting your paper.
* 📸 **Screenshots & Memes:** Snap screenshots directly into Pouchy, use right-click OCR to grab text, or drag straight into Discord and Slack.

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| <kbd>Ctrl</kbd> + <kbd>F</kbd> | Search shelf items |
| <kbd>Ctrl</kbd> + <kbd>Z</kbd> | Undo last remove / clear (up to 20 steps) |
| <kbd>Ctrl</kbd> + <kbd>Tab</kbd> | Cycle next shelf |
| <kbd>Ctrl</kbd> + <kbd>1</kbd> – <kbd>9</kbd> | Switch to shelf 1 through 9 |
| <kbd>Ctrl</kbd> + <kbd>T</kbd> | Create new shelf |
| <kbd>Space</kbd> | Quick Look / full file preview |
| <kbd>Enter</kbd> | Open item in default application |
| <kbd>F2</kbd> | Rename selected item |
| <kbd>Ctrl</kbd> + <kbd>C</kbd> | Copy selected items to clipboard |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>C</kbd> | Copy full file paths |
| <kbd>Ctrl</kbd> + <kbd>A</kbd> | Select all items |
| <kbd>Del</kbd> | Remove item from shelf |
| <kbd>Shift</kbd> + <kbd>Del</kbd> | Delete original file from disk (Recycle Bin) |
| <kbd>Esc</kbd> | Clear selection / dismiss pouch |

---

## ⚡ Technical Architecture (Built the Right Way)

Unlike modern desktop apps wrapped in 300MB of Chromium and Electron:
* **Native C# & WPF (.NET 8):** Ultra-lean memory footprint (~35–45 MB RAM idle).
* **Zero Focus Stealing:** Window positioning uses `SWP_NOACTIVATE` so mouse drags are never cancelled by stealing OS focus.
* **Dedicated STA Worker:** Shell COM thumbnail calls run on an isolated single-threaded apartment background thread ([StaWorker.cs](Helpers/StaWorker.cs)) so the UI thread never stutters.
* **Atomic State Persistence:** State writes are debounced, written to a `.tmp` file, and atomically swapped to prevent corruption if your PC loses power.
* **100% Offline & Private:** Zero telemetry. No trackers. No background network requests (except optional OpenGraph fetching for dropped URLs).

---

## 🧪 Test Suite

Pouchy is engineered for rock-solid daily reliability:
```powershell
dotnet test
```
```
Passed!  - Failed: 0, Passed: 129, Skipped: 0, Total: 129 (Duration: 1 s)
```
Covers pure gesture mathematics, atomic persistence round-trips, link parsing, legacy schema migrations, orientation assertion, and full XAML runtime smoke testing.

---

## 🚀 Building from Source

### Prerequisites
* Windows 10 (version 19041+) or Windows 11
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Clone & Run
```powershell
git clone https://github.com/editorrylix/Pouchy.git
cd Pouchy
dotnet run --project Pouchy.csproj
```

---

## ☕ Support the Creator

Pouchy is an independent passion project made with love for the Windows community.

If Pouchy saves you time every day, consider supporting its development:
* ⭐ **Star this repository** on GitHub — it helps more people discover Pouchy!
* 💬 **Share it** on Twitter/X, Reddit, or with your friends.
* ☕ **[Buy Me a Coffee](https://buymeacoffee.com/notrishi)** to support future updates and features!

<p align="center">
  <a href="https://buymeacoffee.com/notrishi">
    <img src="https://img.buymeacoffee.com/button-api/?text=Buy%20me%20a%20coffee&emoji=☕&slug=notrishi&button_colour=5F7FFF&font_colour=ffffff&font_family=Inter&outline_colour=000000&coffee_colour=FFDD00" alt="Buy Me a Coffee" />
  </a>
</p>

---

## 📜 License

Pouchy is licensed under the **[PolyForm Noncommercial License 1.0.0](LICENSE)**.

* **Free for Personal Use:** You can use, modify, and study Pouchy for personal, educational, and non-commercial purposes for free.
* **Commercial Restrictions:** You may **not** sell, monetize, bundle, rebrand, or distribute Pouchy on commercial app stores (including the Microsoft Store or Steam) without prior written permission from the creator ([editorrylix](https://github.com/editorrylix)).
