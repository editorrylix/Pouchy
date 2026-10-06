# Changelog

All notable changes to Pouchy are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and Pouchy uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html):

- **Major** (`2.0.0`): changes that break saved shelves, settings or custom themes.
- **Minor** (`1.1.0`): new features.
- **Patch** (`1.0.1`): bug fixes.

## [Unreleased]

## [1.0.1] - 2026-10-06

### Fixed

- **Much lower memory use.** Pouchy now uses about **3–5 MB** in Task Manager while it waits in the tray (it was over 100 MB), and around 60 MB with the pouch open (it was 130–250 MB).
  - The release `.exe` is no longer internally compressed. A compressed single-file app unpacks about 80 MB of its own code into memory on every launch. Downloads are now a zip instead (about 70 MB).
  - Pasted and dropped pictures stay on disk at full resolution. Only a small thumbnail is kept in memory, and the full picture loads only when you copy, drag, preview, share or OCR it. A 4K screenshot used to hold about 33 MB for as long as it sat in the pouch.
  - After the pouch hides, Pouchy compacts its memory and returns unused pages to Windows.
  - The mascot's looping animations stop while the pouch is hidden.

## [1.0.0] - 2026-10-06

The first public release. 🎉

### Highlights

- **Shake to summon.** Grab any file, shake the mouse a little, and Pouchy pops up right where you are. You can also bump the screen edge or press <kbd>Alt</kbd> + <kbd>Shift</kbd> + <kbd>Z</kbd>.
- **Park anything.** Files, folders, multi-file stacks, images, text, links and colour values.
- **Drop anywhere.** Drag items out to any app. Files are copied by default (hold <kbd>Shift</kbd> to move), and a picture of the tile follows the cursor.
- **Meet Pouchy.** A reactive mascot that gets excited while you drag, hops happily when fed, and looks puzzled when a search finds nothing.

### Added

- **Shelves**
  - Coloured tabs with 40 icons. They collapse to icons when space is tight.
  - Move items between shelves by dragging them onto a tab.
- **Search, labels and undo**
  - Search every shelf with <kbd>Ctrl</kbd> + <kbd>F</kbd>.
  - Finder-style colour labels that tint the tile.
  - Pin items so Clear keeps them.
  - Undo removals (<kbd>Ctrl</kbd> + <kbd>Z</kbd>).
- **Thumbnail grid** using real Windows thumbnails for photos, videos, PDFs and Office files. Stacks show a fanned preview. Grid, List and Compact views.
- **Right-click menu**
  - Open, Open with, Quick Look, Show in folder.
  - Copy path, Rename, Move or Copy to.
  - ZIP and unzip.
  - Image convert and resize, text extraction (OCR) and Set as wallpaper.
  - Note editing and case tools.
  - The Windows share sheet.
  - The full Explorer menu (7-Zip, Send to and so on).
- **Smart items:**
  - URLs become link cards with the page title and icon.
  - `#hex` and `rgb()` values become colour swatches you can copy as HEX, RGB or HSL.
- **Themes:** Midnight Glass, Frost, Sunset, Paper, Terminal and Follow Windows, plus your own themes as JSON files that reload live ([guide](docs/THEMES.md)).
- **Customisation:** tile size, columns, background opacity, open animations, reduce motion, gesture sensitivity, ignored apps and your own hotkey.
- **Keyboard support:** <kbd>Enter</kbd>, <kbd>Space</kbd>, <kbd>F2</kbd>, <kbd>Del</kbd>, <kbd>Ctrl</kbd> + <kbd>A/C/V/N/P/G/T</kbd>, <kbd>Ctrl</kbd> + <kbd>1</kbd>–<kbd>9</kbd>, and the menu key.
- **Tray menu** with quick actions, shelf, theme and view switching, and Pause gestures.
- **Update notifications:** Pouchy checks GitHub for a new release about once a day. You can turn this off in Settings → About.
- **Start with Windows**, single instance, and items saved between sessions.

[Unreleased]: https://github.com/editorrylix/Pouchy/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/editorrylix/Pouchy/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/editorrylix/Pouchy/releases/tag/v1.0.0
