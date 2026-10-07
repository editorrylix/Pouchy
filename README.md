<div align="center">

<img src="Assets/pouchy-256.png" alt="Pouchy app icon" width="112" />

# Pouchy

**A drag-and-drop shelf for Windows 10 and 11.**
<br />
Park files, images, links and text in a floating pouch, then drop them wherever they need to go.
<br />
A free Windows alternative to Dropover and Yoink.

<a href="https://github.com/editorrylix/Pouchy/releases/latest"><img src="https://img.shields.io/github/v/release/editorrylix/Pouchy?style=flat&label=release&color=8B7CFF" alt="Latest release" /></a>
<a href="https://github.com/editorrylix/Pouchy/releases"><img src="https://img.shields.io/github/downloads/editorrylix/Pouchy/total?style=flat&color=4F8BFF&label=downloads" alt="Downloads" /></a>
<a href="https://github.com/editorrylix/Pouchy/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/editorrylix/Pouchy/ci.yml?branch=main&style=flat&label=build" alt="Build status" /></a>
<img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat&logo=windows&logoColor=white" alt="Windows 10 and 11" />
<img src="https://img.shields.io/badge/telemetry-none-22C55E?style=flat" alt="No telemetry" />

<br />

<a href="https://github.com/editorrylix/Pouchy/releases/latest"><b>Download for Windows</b></a>
&nbsp;·&nbsp;
<a href="#quick-start">Quick start</a>
&nbsp;·&nbsp;
<a href="#features">Features</a>
&nbsp;·&nbsp;
<a href="docs/GUIDE.md">Guide</a>
&nbsp;·&nbsp;
<a href="#faq">FAQ</a>

<br />
<br />

<img src="docs/media/demo.gif" alt="Pouchy demo: shaking a dragged file opens the shelf, files are dropped in and dragged out to another app" width="860" />

<sub><a href="docs/media/demo.mp4">Watch the one-minute demo</a></sub>

</div>

## What is Pouchy?

Moving a file between two apps on Windows usually means Alt+Tab, a window that ends up behind another one, and a file left on the desktop because there was nowhere better to put it.

Pouchy is a small shelf that appears where your mouse is while you drag. Drop things into it, switch to the app you need, and drag them back out. Items stay in the pouch until you remove them, including after a restart.

- **Open it while dragging:** shake the mouse, bump the screen edge twice, or press <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>Z</kbd>.
- **Put anything in it:** files, folders, groups of files, images, text, links and colour codes.
- **Drag it out to any app:** Explorer, a browser, chat apps, design tools, email. Files are copied by default, so the originals stay where they were.

<p align="center">
  <img src="docs/media/hero.png" alt="The Pouchy drop shelf in three themes" width="900" />
</p>

## Quick start

1. **[Download the installer](https://github.com/editorrylix/Pouchy/releases/latest)** (`setup-x64.exe`, or `setup-arm64.exe` for Snapdragon laptops) and run it. A short welcome guide opens, then Pouchy waits in the tray next to the clock.
2. **Drag any file and give the mouse a little shake.** The pouch pops up next to it. Drop the file in.
3. **Drag it out wherever you need it.** Press <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>Z</kbd> or click the tray icon to show the pouch, then drag the file into an email, a chat or a folder.

### What people use it for

- **📧 Attachments from everywhere.** Collect files from three different folders, then drag them into an email in one go.
- **🐞 Screenshots for a bug report.** Press <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>S</kbd>, snip, repeat. Every capture lands in the pouch, ready to drop into the report.
- **📸 Sorting photos.** Drag a batch over the pouch and drop it on a tile: a folder you often use to copy them there, or **50%** for web-sized copies. No other app needed.

There are more ideas, and every feature and setting explained, in the **[Pouchy guide](docs/GUIDE.md)**.

## Features

### Drop actions and scripts

Drag files over the pouch and a row of actions appears. Drop on a tile instead of the pouch to run it:

| Action | What it does |
| --- | --- |
| **Zip** | Puts everything in one archive |
| **A recent folder** | Copies the files there |
| **PNG, JPG, 50%** | Converts or shrinks images |
| **Text** | Reads the text in a picture, using Windows' own text recognition on your PC |
| **Paths** | Copies the file paths |
| **Share** | Opens the Windows share sheet |
| **Print** | Prints the files |

You can add your own actions: put a PowerShell, batch or Python script (or any .exe) in the actions folder and it shows up as a tile. Files the script prints are added to the pouch. The [actions guide](docs/ACTIONS.md) has examples.

<p align="center">
  <img src="docs/media/actions.png" alt="Pouchy drop action tiles and the command palette" width="900" />
</p>

### Command palette

Press <kbd>Ctrl</kbd>+<kbd>K</kbd> and type. The palette finds:
- commands
- items on any shelf
- shelves, views and themes
- actions for the selected items, such as *Copy to Downloads* or *Convert to PNG*

### Shelves, smart shelves and search

- **Shelves.** Separate tabs for Work, Screenshots, Reading list or anything else. Each has its own colour and icon. Drag an item onto a tab to move it there.
- **Smart shelves.** A shelf can collect one kind of item on its own: images, documents, videos, audio, archives, folders, links, notes or colours. Turn it on with right-click on the tab → **Auto-collect**.
- **Search.** <kbd>Ctrl</kbd>+<kbd>F</kbd> searches every shelf. You can also filter by colour label.
- **Labels, pins and undo.** Colour labels tint the tile. Pinned items survive Clear and auto-clear. <kbd>Ctrl</kbd>+<kbd>Z</kbd> brings removed items back.

<p align="center">
  <img src="docs/media/organise.png" alt="Search, undo and the shelf icon picker" width="860" />
</p>

### Clipboard history and screenshots

- **Clipboard history** is off by default. Turn it on and a Clipboard shelf keeps the last 50 things you copied: text, links, images and files. Copies from password managers that mark their content as private are never saved.
- **Screenshot to Pouchy.** <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>S</kbd> opens the Windows screen snip, and the capture goes straight into the pouch. If another app already uses that shortcut, Pouchy picks a free one and shows it in the tray menu.

### Works with Explorer

- **Add to Pouchy** appears in the right-click menu of files and folders, and Pouchy appears in **Send to**. On Windows 11, both are under *Show more options*.
- **Remembers where things go.** Pouchy notes the folders you drop or copy items into, and offers them again under **Copy to** and **Move to**.
- **The full Explorer menu** (7-Zip, Send to and the rest) is available from **More options** in Pouchy's own menu.

### A right-click menu for every kind of item

| Item | Options |
| --- | --- |
| **Files and folders** | Open, Open with, Quick Look, Show in folder, Rename, Copy to, Move to, Zip, Extract, Delete to the Recycle Bin |
| **Images** | Convert, resize, copy the text in the picture, set as wallpaper |
| **Links** | Shows the page title and icon. Open it, or copy it as a Markdown link |
| **Colours** | Paste `#FF8800` or `rgb(…)` and copy it back as HEX, RGB or HSL |
| **Text** | Edit, change case, tidy whitespace, save as a .txt file |

<p align="center">
  <img src="docs/media/menus.png" alt="Pouchy item menu and tray menu" width="860" />
</p>

### Themes, sounds and a mascot

- **Six built-in themes:** Midnight Glass, Frost, Sunset, Paper, Terminal and Follow Windows.
- **Your own themes:** the **theme editor** lets you pick colours, a gradient, opacity, corner roundness, shadow and fonts, and the pouch updates as you change them. Themes are saved as JSON files, so they're easy to share ([theme guide](docs/THEMES.md)).
- **Views:** Grid, List or Compact, with adjustable tile size, columns and open animation.
- **Sound packs:** Soft, Bubbly, Clicky or your own WAV files. Off by default.
- **A mascot** that reacts as you drag, drop and search. You can hide it.

<p align="center">
  <img src="docs/media/themes.png" alt="The six built-in Pouchy themes" width="860" />
</p>

<p align="center"><b>Every feature, setting and shortcut is explained in the <a href="docs/GUIDE.md">Pouchy guide</a>.</b></p>

## Install

Download from the **[latest release](https://github.com/editorrylix/Pouchy/releases/latest)**:

| | Intel and AMD PCs | Windows on ARM |
| --- | --- | --- |
| **Installer** (recommended) | `Pouchy-x.y.z-setup-x64.exe` | `Pouchy-x.y.z-setup-arm64.exe` |
| **Portable** | `Pouchy-x.y.z-win-x64.zip` | `Pouchy-x.y.z-win-arm64.zip` |

- **Installer:** installs for your account only, so it needs no admin rights. It can add Pouchy to startup and to the Explorer menu, and it has a normal uninstaller.
- **Portable zip:** a single `Pouchy.exe` you can run from any folder.
- **Neither needs .NET installed.**
- **Starts with Windows** and opens a short welcome guide the first time. You can change both in Settings.
- **Updates:** when a new version comes out, Pouchy offers to install it. It downloads the update, checks its SHA-256 checksum, restarts, and shows what's new.

**Requirements:** Windows 10 version 2004 or later, or Windows 11.

> [!NOTE]
> Pouchy isn't code-signed yet, so Windows SmartScreen may say *"Windows protected your PC"* for a new download. Click **More info → Run anyway**. Each release lists SHA-256 checksums so you can check your download.

## Keyboard shortcuts

<details>
<summary><b>Show all shortcuts</b></summary>

| Shortcut | Action |
| --- | --- |
| <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>Z</kbd> | Show or hide the pouch (you can change it) |
| <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>S</kbd> | Screenshot to the pouch (you can change it) |
| <kbd>Ctrl</kbd>+<kbd>K</kbd> | Command palette |
| <kbd>Space</kbd> | Quick Look |
| <kbd>Enter</kbd> | Open |
| <kbd>F2</kbd> | Rename or edit |
| <kbd>Ctrl</kbd>+<kbd>C</kbd> / <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>C</kbd> | Copy / copy file paths |
| <kbd>Ctrl</kbd>+<kbd>V</kbd> | Paste into the pouch |
| <kbd>Ctrl</kbd>+<kbd>N</kbd> | New note |
| <kbd>Ctrl</kbd>+<kbd>F</kbd> | Search every shelf |
| <kbd>Ctrl</kbd>+<kbd>T</kbd> | New shelf |
| <kbd>Ctrl</kbd>+<kbd>Tab</kbd> / <kbd>Ctrl</kbd>+<kbd>1</kbd>–<kbd>9</kbd> | Switch shelves |
| <kbd>Ctrl</kbd>+<kbd>G</kbd> | Group the selected files into a stack |
| <kbd>Ctrl</kbd>+<kbd>P</kbd> | Pin or unpin |
| <kbd>Ctrl</kbd>+<kbd>A</kbd> | Select all |
| <kbd>Ctrl</kbd>+<kbd>Z</kbd> | Undo remove |
| <kbd>Del</kbd> / <kbd>Shift</kbd>+<kbd>Del</kbd> | Remove from the pouch / move the file to the Recycle Bin |
| <kbd>Esc</kbd> | Clear the selection, then hide |

</details>

## Pouchy, Dropover and Yoink

Dropover and Yoink are drag-and-drop shelves for **macOS**. There's no Windows version of either. Pouchy brings the same idea to Windows, built as a native Windows app:

- It uses Windows drag-and-drop, so files arrive in other apps exactly as they would from Explorer.
- Thumbnails come from Windows itself, so photos, videos, PDFs and Office files show previews.
- It works on high-DPI and mixed-DPI monitor setups.
- It's written in C# and WPF, with no Electron or bundled browser. It uses about 3–5 MB of memory while it waits in the tray.

## Privacy

- **No account, analytics, telemetry or cloud.** Your shelves stay in `%AppData%\Pouchy` on your PC.
- **Two optional features go online, and both can be turned off in Settings:**
  - **Link previews:** loads a dropped web page once to show its title and icon.
  - **Update check:** asks GitHub about once a day whether a newer version exists.
- **Clipboard history is off unless you turn it on,** and it skips content that password managers mark as private.

## FAQ

<details>
<summary><b>Is Pouchy free?</b></summary>

Yes, for personal and other noncommercial use. See the [license](#license).
</details>

<details>
<summary><b>Will shaking the mouse open it by accident?</b></summary>

The shake and edge gestures only count while you're actually dragging something. Moving windows, opening menus and selecting text are ignored. You can change the sensitivity, turn single gestures off, list apps to ignore, or pause gestures from the tray. By default it also stays closed over fullscreen apps and games.
</details>

<details>
<summary><b>Does Pouchy copy my files?</b></summary>

No. The pouch holds a reference to each file, the same way the Windows clipboard does. Only pasted images and link icons are saved, in `%AppData%\Pouchy`. If a file is moved or deleted, its tile shows a warning badge.
</details>

<details>
<summary><b>Does it slow down my PC?</b></summary>

No. While it waits in the tray it uses about 3–5 MB of memory and 0% CPU. With the pouch open it uses around 60 MB and well under 1% CPU.
</details>

<details>
<summary><b>Where can I learn everything it does?</b></summary>

In the **[Pouchy guide](docs/GUIDE.md)**: everyday examples, every feature and setting, keyboard shortcuts, and troubleshooting. You can also open the welcome guide again from the tray menu → **Help**.
</details>

<details>
<summary><b>How do I uninstall it?</b></summary>

If you used the installer, uninstall Pouchy from **Settings → Apps** like any other app. That also removes its startup entry and Explorer menu items.

If you use the portable zip, first turn off **Run at Windows startup** and **Add to Pouchy from Explorer** in Pouchy's Settings, then delete `Pouchy.exe`.

To remove your shelves and settings too, delete `%AppData%\Pouchy`.
</details>

## Build from source

You'll need Windows 10 (2004) or later and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/editorrylix/Pouchy.git
cd Pouchy
dotnet run --project Pouchy.csproj
dotnet test
```

Pushing a version tag (`vX.Y.Z`) makes [GitHub Actions](.github/workflows/release.yml) build the release, including the [installer](installer/Pouchy.iss). See the [changelog](CHANGELOG.md) for what's new, the [guide](docs/GUIDE.md) for how everything works, and the [roadmap](IDEAS.md) for what's planned.

## Feedback and support

To report a bug or suggest a feature, [open an issue](https://github.com/editorrylix/Pouchy/issues/new/choose). A screenshot and the last lines of `%AppData%\Pouchy\debug.log` help.

If Pouchy is useful to you, a star helps other Windows users find it. You can also support development:

<a href="https://buymeacoffee.com/notrishi" target="_blank">
  <img src="Assets/buymeacoffee.png" alt="Buy me a coffee" height="44" />
</a>

<a id="license"></a>

## License

Pouchy is released under the [PolyForm Noncommercial License 1.0.0](LICENSE).

You can use, copy, study, modify, fork and share Pouchy for **noncommercial** purposes. Commercial use, paid redistribution, rebranding, bundling or distribution through commercial app stores needs a separate commercial license from the author.

Pouchy is **source-available**. PolyForm Noncommercial is not an OSI-approved open-source license.
