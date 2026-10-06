<p align="center">
  <img src="Assets/pouchy-256.png" alt="Pouchy logo" width="112" />
</p>

<h1 align="center">Pouchy</h1>

<p align="center">
  <strong>The drag-and-drop shelf Windows should already have.</strong>
</p>

<p align="center">
  Temporarily park files, images, links, and text while you move between apps.<br />
  No cloud workspace. No Electron. No clipboard juggling.
</p>

<p align="center">
  A native, source-available <strong>Dropover and Yoink alternative for Windows 10 & 11</strong>, built with C# and .NET 8.
</p>

<p align="center">
  <a href="https://learn.microsoft.com/windows/"><img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat&logo=windows&logoColor=white" alt="Windows 10 and 11" /></a>
  <a href="https://dotnet.microsoft.com/download/dotnet/8.0"><img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet&logoColor=white" alt=".NET 8" /></a>
  <img src="https://img.shields.io/badge/UI-WPF-5C2D91?style=flat" alt="WPF" />
  <img src="https://img.shields.io/badge/Telemetry-None-111827?style=flat" alt="No telemetry" />
  <a href="#license"><img src="https://img.shields.io/badge/License-PolyForm%20Noncommercial-8B7CFF?style=flat" alt="PolyForm Noncommercial License" /></a>
</p>

<p align="center">
  <a href="#see-it-in-action">Demo</a>
  ·
  <a href="#features">Features</a>
  ·
  <a href="#get-pouchy">Get Pouchy</a>
  ·
  <a href="#keyboard-shortcuts">Shortcuts</a>
  ·
  <a href="#building-from-source">Build</a>
</p>

<p align="center">
  <img src="docs/media/hero.png" alt="Pouchy drag-and-drop file shelf for Windows" width="900" />
</p>

---

## Stop carrying files across your desktop

Windows is great at dragging files. It is much worse at what happens **between** the place you picked them up and the place you want to drop them.

You grab a file in Explorer, Alt-Tab through several windows, lose your destination, clutter the desktop with temporary assets, or keep reopening the same folder just to move one thing.

Pouchy gives those items somewhere to wait.

1. **Grab** a file, folder, image, link, or text snippet.
2. **Summon Pouchy** with a mouse shake or global hotkey.
3. **Drop it onto a shelf.**
4. Go wherever you need to go, then **drag it back out**.

That is the whole idea: **drag, park, keep working.**

---

<a id="see-it-in-action"></a>

## See it in action

<p align="center">
  <img src="docs/media/demo.gif" alt="Pouchy Windows drag-and-drop shelf demo" width="860" />
</p>

Pouchy stays out of the way until you need it, then gives you a temporary visual shelf for the things you are moving between apps.

---

<a id="features"></a>

## Features

### Summon it while you drag

Shake the mouse while dragging, or use the global hotkey, and Pouchy appears without turning a simple file move into an Alt-Tab obstacle course.

The window is positioned with native Windows behavior designed to avoid stealing focus from the drag operation.

### Multiple shelves, search, labels, and undo

Separate temporary items by task instead of throwing everything into one pile.

- Create multiple shelves for work, screenshots, research, assets, or anything else.
- Switch shelves with <kbd>Ctrl</kbd> + <kbd>Tab</kbd> or <kbd>Ctrl</kbd> + <kbd>1</kbd>–<kbd>9</kbd>.
- Search shelf contents instantly with <kbd>Ctrl</kbd> + <kbd>F</kbd>.
- Add color labels to visually group items.
- Undo accidental removals with <kbd>Ctrl</kbd> + <kbd>Z</kbd>.
- Pin items you want to keep around.

<p align="center">
  <img src="docs/media/organise.png" alt="Pouchy shelves, search, labels, and organization" width="840" />
</p>

### Useful actions where you need them

Pouchy is more than a holding area. Right-click an item to act on it without breaking your flow.

**Images**

- Resize to 50% or 25%
- Convert to PNG or JPG
- Extract text with local OCR
- Set as wallpaper

**Files and folders**

- Quick Look preview
- Open With
- Show in Explorer
- Move or copy to another location
- Compress to ZIP
- Extract ZIP archives

**Text and links**

- Edit notes inline
- Transform text and tidy whitespace
- Open links
- Copy links as Markdown
- Preview supported link metadata

**Colors**

Drop a color such as `#FF8800` and copy it back as HEX, RGB, or HSL.

Pouchy can also open the native Windows shell menu, so installed Explorer integrations such as 7-Zip or Git remain available.

<p align="center">
  <img src="docs/media/menus.png" alt="Pouchy context menus, OCR, image tools, and file actions" width="840" />
</p>

### Native Windows drag-and-drop

Pouchy uses Windows OLE drag-and-drop integration rather than faking the interaction inside a browser shell.

- Outgoing drags can preserve native shell drag data and drag imagery.
- Pouchy sets Windows' `Preferred DropEffect` so files dragged from Pouchy into Explorer default to **copying** rather than unexpectedly moving the source on same-drive drops.
- Shell thumbnail work is isolated on a dedicated STA worker instead of being pushed through the main UI thread.

### Themes that are actually customizable

Pouchy ships with multiple visual styles and supports Grid, List, and Compact views.

Included themes cover dark glass, light/frosted, warm, paper-like, terminal, and Windows-following styles.

<p align="center">
  <img src="docs/media/themes.png" alt="Pouchy custom themes for Windows" width="840" />
</p>

<p align="center">
  <img src="docs/media/views.png" alt="Pouchy grid, list, and compact layouts" width="840" />
</p>

Custom themes are plain JSON files in:

```text
%AppData%\Pouchy\themes\
```

They hot-reload while Pouchy is running. See the [custom theming guide](docs/THEMES.md) for the full format.

### A little personality, without turning the app into a toy

Pouchy includes a reactive mascot that responds to what is happening in the app with idle, excited, happy, and puzzled states.

<p align="center">
  <img src="docs/media/mascot.png" alt="Pouchy mascot states" width="840" />
</p>

---

## Looking for Dropover or Yoink on Windows?

If you searched for **Dropover for Windows**, **Yoink for Windows**, a **drag-and-drop shelf for Windows**, or a temporary file parking utility, Pouchy lives in that category.

The goal is not to clone a macOS app pixel-for-pixel. Pouchy takes the same useful idea — temporarily parking things during a drag-and-drop workflow — and builds around Windows-native behavior, multiple shelves, search, labels, quick actions, themes, and local-first operation.

---

## Built for real workflows

**Designers and video editors** can park footage, screenshots, logos, exports, and references while moving between Explorer, Photoshop, Premiere, After Effects, or other creative tools.

**Developers** can keep test files, screenshots, JSON, snippets, URLs, and temporary assets close while switching between terminals, editors, browsers, and branches.

**Students and researchers** can collect PDFs, quotes, screenshots, and links on a dedicated shelf while writing or studying.

**Everyone else** can use Pouchy for the boring little file moves that somehow eat more time than they should.

---

<a id="keyboard-shortcuts"></a>

## Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| <kbd>Ctrl</kbd> + <kbd>F</kbd> | Search shelf items |
| <kbd>Ctrl</kbd> + <kbd>Z</kbd> | Undo a recent removal |
| <kbd>Ctrl</kbd> + <kbd>Tab</kbd> | Switch to the next shelf |
| <kbd>Ctrl</kbd> + <kbd>1</kbd>–<kbd>9</kbd> | Jump directly to a shelf |
| <kbd>Ctrl</kbd> + <kbd>T</kbd> | Create a new shelf |
| <kbd>Space</kbd> | Quick Look / preview |
| <kbd>Enter</kbd> | Open selected item |
| <kbd>F2</kbd> | Rename or edit selected item |
| <kbd>Ctrl</kbd> + <kbd>C</kbd> | Copy selected items |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>C</kbd> | Copy full file paths |
| <kbd>Ctrl</kbd> + <kbd>A</kbd> | Select all |
| <kbd>Del</kbd> | Remove item from Pouchy |
| <kbd>Shift</kbd> + <kbd>Del</kbd> | Delete the original file from disk |
| <kbd>Esc</kbd> | Clear selection / dismiss Pouchy |

---

## Native by design

Pouchy is a **C# / WPF desktop application targeting .NET 8 and Windows 10 version 19041+**.

There is no Electron runtime and no bundled Chromium instance.

A few implementation details that matter for this kind of utility:

- **`SWP_NOACTIVATE` window positioning** helps Pouchy appear without stealing focus from an active drag.
- **OLE drag-and-drop integration** preserves native Windows behavior that ordinary WPF drag wrappers cannot always carry through untouched.
- **Dedicated STA worker** handles shell COM work such as thumbnails on the apartment model Windows shell handlers expect.
- **Atomic state persistence** reduces the risk of corrupting saved state during interrupted writes.
- **Automated tests** cover core file actions, gestures, persistence, parsing, settings, theming, thumbnails, view behavior, and related logic.

---

## Privacy

Pouchy is built to work locally.

- No analytics
- No telemetry
- No tracking SDKs
- No account required
- No cloud storage requirement

Your files remain on your machine.

Dropped URLs may optionally fetch OpenGraph metadata to create richer link previews, but normal file-shelf usage does not require a cloud service.

---

<a id="get-pouchy"></a>

## Get Pouchy

Check [GitHub Releases](https://github.com/editorrylix/Pouchy/releases) for packaged builds.

If a packaged release is not available yet, you can run Pouchy directly from source using the steps below.

---

<a id="building-from-source"></a>

## Building from source

### Requirements

- Windows 10 version 19041+ or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git

### Clone and run

```powershell
git clone https://github.com/editorrylix/Pouchy.git
cd Pouchy
dotnet run --project Pouchy.csproj
```

### Run the test suite

```powershell
dotnet test
```

---

## Contributing and feedback

Found a bug or have an idea that would make Pouchy better?

Open an [issue](https://github.com/editorrylix/Pouchy/issues) with a clear description, reproduction steps when relevant, and screenshots if they help explain the problem.

If you like the project, starring the repository is one of the simplest ways to help more Windows users discover it.

---

## Support development

Pouchy is an independent project.

If it saves you time and you want to support continued development, you can:

- Star the repository
- Share it with another Windows user
- Report bugs and useful edge cases
- [Buy me a coffee](https://buymeacoffee.com/notrishi)

<p align="center">
  <a href="https://buymeacoffee.com/notrishi">
    <img src="https://img.buymeacoffee.com/button-api/?text=Buy%20me%20a%20coffee&emoji=☕&slug=notrishi&button_colour=5F7FFF&font_colour=ffffff&font_family=Inter&outline_colour=000000&coffee_colour=FFDD00" alt="Support Pouchy on Buy Me a Coffee" />
  </a>
</p>

---

<a id="license"></a>

## License

Pouchy is distributed under the [PolyForm Noncommercial License 1.0.0](LICENSE).

You may use, copy, study, modify, fork, and share Pouchy for permitted **noncommercial** purposes under the terms of that license. Commercial use, monetized redistribution, rebranding, bundling, or commercial app-store distribution requires explicit permission or a separate commercial license from the author.

> **Note:** Pouchy is source-available. PolyForm Noncommercial is not an OSI-approved open-source license.

---

<p align="center">
  <strong>Pouchy — a native drag-and-drop file shelf for Windows.</strong>
</p>

<p align="center">
  <a href="https://github.com/editorrylix/Pouchy">Star on GitHub</a>
  ·
  <a href="https://github.com/editorrylix/Pouchy/issues">Report an issue</a>
  ·
  <a href="https://buymeacoffee.com/notrishi">Support development</a>
</p>
