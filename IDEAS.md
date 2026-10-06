# Pouchy: Ideas & Roadmap

A Windows drop shelf in the spirit of Yoink / Dropover / Dropzone (macOS), built around **deep customisation** and a distinctive UI.

Legend: 🔴 must-have · 🟡 should-have · 🟢 nice-to-have

---

## 1. Theming & customisation (core identity)

### Theme engine
- 🔴 Remove every hard-coded colour, size and radius from XAML and replace it with **design tokens** (accent, surface, text, border, radius, blur, shadow, font, spacing, item size).
- 🔴 Themes are `.json` files in `%AppData%\Pouchy\themes\`, **hot-reloaded** when they change.
- 🔴 Built-in themes:
  - **Glass**: acrylic or Mica with a soft glow
  - **Paper**: warm, flat colours with a tactile shadow
  - **Terminal**: monospace, scanlines, neon accent
  - **Minimal pill**: a tiny capsule that expands on hover or drop
  - **Follow Windows**: system accent colour and light/dark mode
- 🟡 Backdrop options: solid, acrylic, Mica, or a custom image (blur and tint sliders).
- 🟡 Theme editor in Settings with a **live preview pouch**, colour pickers, sliders, and import/export.
- 🟢 Theme gallery or community sharing (as `.pouchytheme` files).
- 🟢 Different themes per shelf.
- 🟢 Theme switches automatically by time of day.

### Layout & motion
- 🔴 View modes: **list**, **grid**, **compact chips**, **fanned stack**.
- 🟡 Settings for icon size, density, and whether metadata is shown.
- 🟡 Spawn animation: scale, slide from cursor, bounce drop, or none; adjustable speed.
- 🟡 Reduced-motion mode.
- 🟢 Sound effects (drop, remove, spawn), with custom sound packs.
- 🟢 Window size, opacity and corner style per shelf.

---

## 2. Core UI/UX

- 🔴 The pouch appears only during a **real drag** (file or data), near the cursor, and auto-hides when empty.
- 🔴 **Snap target**: when a drag starts, a "drop here" zone slides in from the nearest screen edge.
- 🔴 Drag-in feedback: the pouch grows or glows, and a badge shows **Copy / Move / Link**.
- 🔴 Real **Shell thumbnails** for every file type (PDF, video frames, Office docs, etc.).
- 🔴 Multi-select (Ctrl/Shift+click, rubber-band selection), and dragging the selection out.
- 🟡 Reorder items by dragging.
- 🟡 Full keyboard support: arrow keys, Space (Quick Look), Delete, Ctrl+C, Ctrl+A, Enter (open).
- 🟡 Empty state with an animated "drop things here" hint.
- 🟡 Edge-dock tab, configurable per side (left/right/top/bottom) and per monitor.
- 🟢 Pin the pouch as an always-visible mini dock.
- 🟢 Peek mode: hovering the edge tab shows a thumbnail strip without fully opening the pouch.

---

## 3. Right-click context menu (per item)

A themed menu (not the default WPF one) with sections. Options appear only when they apply to the item type.

**Open**
- Open
- Open with… (list of apps from the Windows registry)
- Show in Explorer
- Quick Look

**Clipboard**
- Copy (file)
- Copy path
- Copy as `"quoted path"`
- Copy file name
- Copy contents (text files and images)

**Organise**
- Rename (inline)
- Pin / Unpin (pinned items survive "Clear")
- Move to shelf → submenu
- Add tag / colour label
- Add a note
- Group into stack / Ungroup stack

**File actions**
- Move to… / Copy to… (with recent folders)
- Compress to ZIP
- Extract (archives)
- Delete from disk, with confirmation (separate from "Remove from pouch")

**Image actions**
- Resize
- Convert (PNG/JPG/WebP)
- Rotate
- Copy as image
- Set as wallpaper
- OCR text

**Text actions**
- Edit snippet
- Save as `.txt`
- Trim whitespace
- Change case
- Open URL (if the text is a link)

**Share**
- Email
- Nearby Share
- Upload and copy link (configurable target)

**Shell**
- "More options…": opens the **native Windows Explorer context menu** for the file (via `IContextMenu`)

**Remove**
- Remove from pouch
- Remove all others

Also:
- 🟡 Right-click on **empty pouch space**: Paste, New text note, Clear all, View mode, Theme, Settings.
- 🟡 Right-click on the **header**: Pin shelf, Rename shelf, Always on top, Dock to edge, Close.
- 🟡 Right-click on a **multi-selection**: bulk versions of the actions above.

---

## 4. Shelves & organisation

- 🔴 **Multiple shelves** with names, colours and icons (e.g. Work, Screenshots, Temp).
- 🟡 Pinned items stay put when a shelf is cleared.
- 🟡 Expandable stacks: expand inline, pull a single file out, merge stacks.
- 🟡 Tags or colour labels with filtering.
- 🟡 Search box (Ctrl+F) across all shelves.
- 🟢 Smart shelves that fill automatically by rule (e.g. "all images dropped today").
- 🟢 Shelf history / recently removed (undo with Ctrl+Z).

---

## 5. Content types

- 🔴 Files, folders, stacks, text, images (already partly supported).
- 🟡 **URLs** shown as link cards with page title and favicon.
- 🟡 **Rich text and HTML** snippets.
- 🟡 **Colour values** (`#ff8800`) shown as swatches.
- 🟡 Text notes created inside Pouchy.
- 🟢 Code snippets with syntax highlighting.
- 🟢 Screenshot capture straight into the pouch (hotkey).

---

## 6. Drop actions (Dropzone-style)

- 🟡 While you drag, **action tiles** appear next to the pouch; dropping a file on a tile runs that action.
- Built-in actions:
  - Zip
  - Resize image
  - Convert to PDF
  - Move to folder X
  - Copy to folder X
  - Upload and copy link
  - Email
  - Print
  - Run script
- 🟡 `IPouchAction` plugin interface for adding actions.
- 🟢 User scripts (PowerShell / Python) as actions, with parameters.
- 🟢 Actions can be chained (resize → convert → upload).

---

## 7. Clipboard integration

- 🟡 Clipboard history tab (text, images, files) with pin and search.
- 🟡 Optional automatic capture: every copy goes into a chosen shelf.
- 🟡 Paste a pouch item into the active app (double-click or Enter).
- 🟢 Ignore clipboard content from password managers (by app or by Windows' sensitive-content clipboard flag).

---

## 8. Triggers & behaviour

- 🔴 Each trigger can be turned on or off: **shake**, **edge bump**, **hotkey**, **modifier held while dragging**, **tray click**, **always-visible dock**.
- 🔴 Triggers fire only during a real drag operation, not text selection or window moves.
- 🟡 Adjustable shake sensitivity (distance, reversals, time window).
- 🟡 Hotkey recorder (custom combos) for each action: toggle pouch, new note, screenshot, search.
- 🟡 Visual per-app blacklist and whitelist picker (choose from running processes).
- 🟡 Smart suppression: fullscreen on **any** monitor, games, presentation mode, Focus Assist.
- 🟢 Spawn position: at cursor, at nearest edge, fixed position, or last position.

---

## 9. Persistence & reliability

- 🔴 Persist everything: stacks, images (saved to a cache folder), notes, pins, tags, shelves, order.
- 🔴 Detect missing or moved files and show a "missing" badge with Locate / Remove.
- 🟡 Auto-clear policy: never, on restart, after N hours, or when the item is dragged out.
- 🟡 Debounced, async saving.
- 🟢 Export/import shelves and settings (backup).
- 🟢 Settings sync via a OneDrive or Dropbox folder.

---

## 10. System integration

- 🔴 Start with Windows (registry `Run` key).
- 🟡 Explorer context menu: **"Send to Pouchy"** / "Send to shelf →".
- 🟡 "Send To" menu entry.
- 🟡 Tray icon with badge count, left-click toggle, themed menu.
- 🟢 Windows Share target.
- 🟢 Jump list on the taskbar.
- 🟢 Toast notifications for completed actions (e.g. "Zipped 5 files").

---

## 11. Command palette

- 🟡 **Ctrl+Space** (configurable) opens a fuzzy search over items, shelves, actions and settings.
- 🟢 Run an action on the selected items from the palette.

---

## 12. Settings app

Pages:
- **General**: startup, auto-clear, language
- **Appearance**: theme editor, layout, animations
- **Triggers**: per-trigger toggles, sensitivity, hotkeys
- **Shelves**: manage shelves
- **Actions**: enable, reorder and configure drop actions
- **Apps**: blacklist / whitelist
- **Clipboard**: history settings
- **Advanced**: logs, reset, export/import
- **About**

Settings apply live, without a restart.

---

## 13. Technical foundation ✅ (Phase 1 complete)

Done:
- ✅ Git repo with a .NET `.gitignore`; solution file `Pouchy.sln`.
- ✅ **MVVM** (CommunityToolkit.Mvvm) with folders `Models/`, `ViewModels/`, `Views/`, `Services/`, `Interop/`, `Helpers/`.
- ✅ Services: `SettingsService`, `StartupService`, `PersistenceService`, `TriggerService` + pure `GestureDetector`, `HotkeyService`, `ItemFactory`, `IIconProvider`.
- ✅ Bug fixes:
  - Folder drops no longer crash.
  - Unhandled UI exceptions are logged instead of killing the app.
  - Triggers ignore window moves, menus and text selection (configurable drag detection).
  - Ctrl-drag trigger is off by default.
  - Safe bitmap handling.
  - Per-monitor DPI positioning and fullscreen detection.
  - Spawn/despawn animation race.
  - Log rotation.
  - Single instance.
  - Tray icon now actually created.
- ✅ Full persistence: stacks, images (PNG cache), folders; missing files flagged; debounced atomic saves.
- ✅ Working Settings window: startup, clear on startup, trigger toggles and sensitivity, drag detection, hotkey recorder, fullscreen suppression, ignored apps.
- ✅ Dead code and the unused Behaviors package removed.
- ✅ Unit tests (`Pouchy.Tests`): gestures, persistence, item creation, settings, XAML smoke test.

Deferred to later phases:
- `ThemeService` (Phase 2), `ShelfService` (Phase 4), `ActionRegistry` (Phase 5).
- 🟡 App icon (Phase 2, with the visual redesign).
- 🟢 Installer (MSIX or Inno Setup) and auto-update (Phase 6).

---

## Roadmap

| Phase | Focus | Contents |
|---|---|---|
| **1. Foundation** ✅ | Stability | Git, MVVM, services, bug fixes, settings model, start with Windows |
| **2. Look** | Identity | Theme engine, 4–5 built-in themes, redesigned pouch, view modes, animations |
| **3. Interaction** | UX | Right-click menus, multi-select, keyboard support, Shell thumbnails, smart triggers |
| **4. Organisation** | Power | Multiple shelves, pins, tags, search, full persistence, link and colour content types |
| **5. Actions** | Pro | Drop actions, plugin interface, Explorer integration, clipboard history |
| **6. Polish** | Release | Theme editor, command palette, installer, auto-update, sound packs |
