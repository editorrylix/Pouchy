# Custom themes

A Pouchy theme is a JSON file in `%AppData%\Pouchy\themes\`. Pouchy watches the folder: save the file and the pouch updates immediately.

The easiest way to start is **Settings → Theme → Customize this theme**. This copies the selected theme to a new file, selects it and opens it in your editor.

## Format

Every field is optional. Anything you leave out uses the Midnight Glass default, so a theme can be as small as this:

```json
{
  "Name": "Hot Pink",
  "Colors": { "Accent": "#FF2D95" }
}
```

A complete theme:

```json
{
  "Id": "my-theme",
  "Name": "My Theme",
  "Author": "you",
  "Base": "dark",
  "FontFamily": "Segoe UI Variable Text, Segoe UI",
  "HeaderFontFamily": "Segoe UI Variable Display, Segoe UI",
  "MonoFontFamily": "Cascadia Mono, Consolas",
  "CornerRadius": 18,
  "TileCornerRadius": 12,
  "Colors": {
    "Background": "linear(160deg, #F21D1B29 0%, #F20C0B11 100%)",
    "Border": "linear(180deg, #40FFFFFF 0%, #0FFFFFFF 100%)",
    "Accent": "#8B7CFF",
    "Accent2": "#BB4FFF",
    "AccentText": "#FFFFFF",
    "Text": "#F5F5F7",
    "TextSecondary": "#B3FFFFFF",
    "TextMuted": "#66FFFFFF",
    "Tile": "#12FFFFFF",
    "TileHover": "#22FFFFFF",
    "TileBorder": "#14FFFFFF",
    "Card": "#FF2A2A33",
    "Badge": "#B3000000",
    "BadgeText": "#FFFFFF",
    "Danger": "#FF5A52",
    "Warning": "#FFB27A",
    "Scrollbar": "#44FFFFFF"
  },
  "Shadow": { "Color": "#000000", "Blur": 36, "Depth": 8, "Opacity": 0.6 },
  "Effects": {
    "Scanlines": false,
    "ScanlineOpacity": 0.08,
    "Glow": 0.22,
    "GlowColor": "#8B7CFF",
    "Sheen": 0.06,
    "AccentGlow": 0.45
  }
}
```

Field names are not case-sensitive.

| Field | Meaning |
|---|---|
| `Id` | Unique id. Defaults to the file name. |
| `Base` | `dark` or `light` (sets the style of the Settings and Quick Look windows), or `system` to follow Windows. With `system`, the theme uses `LightColors` while Windows is in light mode. |
| `CornerRadius` / `TileCornerRadius` | Roundness of the pouch and of the tiles, in pixels. |
| `Colors.Background` / `Colors.Border` | Can be a solid colour or a gradient. |
| `Colors.Accent2` | Where the accent gradient ends: the Drag all button fades from `Accent` into it. Leave it out and Pouchy picks one by turning the accent's hue a little. |
| `Colors.Card` | Background of the cards in a stack's fanned preview. |
| `Colors.Badge` / `Colors.BadgeText` | The file-type labels on tiles. |
| `Effects.Scanlines` | Draws CRT-style lines over the pouch (used by the Terminal theme). |
| `Effects.Glow` / `Effects.GlowColor` | Soft light falling from the top-left of the pouch, 0 (off) to 1. The colour defaults to the accent. |
| `Effects.Sheen` | A glassy highlight along the top of the pouch and its tiles, 0 to 1. |
| `Effects.AccentGlow` | The coloured glow under the Drag all button and around selected items, 0 to 1. |

## Colours

| Format | Example |
|---|---|
| `#RRGGBB` | `#FF8A5B` |
| `#AARRGGBB` (alpha first) | `#80FFFFFF` is 50% white |
| Named colour | `White`, `CornflowerBlue` |
| `system` | The Windows accent colour |
| Gradient (background and border only) | `linear(135deg, #2A1B3D 0%, #7A3042 100%)` |

Gradient angles work like CSS: `0deg` points up and `90deg` points right. If you leave out the stop positions, the stops are spaced evenly.

If a theme file has a mistake, Pouchy skips it, keeps the previous theme and writes the reason to `debug.log` (**Settings → Advanced → Open log**).
