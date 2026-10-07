"""Draws the Pouchy app icon at any size and writes the MSIX logo images.

The icon is drawn as shapes (not scaled up from the 256 px PNG), so every size is sharp.
Run from the repository root:  python packaging/make_assets.py
Needs Pillow (pip install pillow).
"""
from pathlib import Path
from PIL import Image, ImageDraw

OUT = Path(__file__).parent / "Assets"
BASE = 256          # The icon is designed on a 256 x 256 grid.
SUPERSAMPLE = 4     # Drawn this many times larger, then shrunk, for smooth edges.

INK = (42, 34, 64, 255)
WHITE = (255, 255, 255, 255)
BERET = (226, 222, 255, 255)
CHEEK = (255, 177, 204, 255)
TILE_FROM = (160, 122, 255)   # top-left
TILE_TO = (60, 141, 255)      # bottom-right


def icon(size: int) -> Image.Image:
    """The app icon as a square RGBA image."""
    big = size * SUPERSAMPLE
    s = big / BASE

    def box(x0, y0, x1, y1):
        return [x0 * s, y0 * s, x1 * s, y1 * s]

    # Tile: rounded square with a diagonal gradient.
    gradient = Image.new("RGBA", (big, big))
    gp = gradient.load()
    for y in range(big):
        for x in range(big):
            t = min(1.0, max(0.0, ((x + y) / (2 * big) - 12 / BASE) / (231 / BASE)))
            gp[x, y] = tuple(int(TILE_FROM[i] + (TILE_TO[i] - TILE_FROM[i]) * t) for i in range(3)) + (255,)
    mask = Image.new("L", (big, big), 0)
    ImageDraw.Draw(mask).rounded_rectangle(box(12, 12, 243, 243), radius=56 * s, fill=255)
    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    img.paste(gradient, (0, 0), mask)

    d = ImageDraw.Draw(img)
    # Body.
    d.rounded_rectangle(box(52, 100, 203, 220), radius=46 * s, fill=WHITE)
    # Beret and its little stalk.
    d.rounded_rectangle(box(114, 55, 142, 82), radius=9 * s, fill=BERET)
    d.rounded_rectangle(box(40, 72, 215, 116), radius=22 * s, fill=BERET)
    # Eyes with highlights.
    for cx in (104, 152):
        d.ellipse(box(cx - 13, 136.5, cx + 13, 167.5), fill=INK)
        d.ellipse(box(cx + 3, 140, cx + 10, 147), fill=WHITE)
    # Cheeks.
    for cx in (80, 176):
        d.ellipse(box(cx - 13, 167, cx + 13, 181), fill=CHEEK)
    # Smile.
    import math
    cx, cy, rx, ry, w = 128, 170, 15, 10.5, 5.6
    d.arc(box(cx - rx - w / 2, cy - ry - w / 2, cx + rx + w / 2, cy + ry + w / 2), start=18, end=162, fill=INK, width=round(w * s))
    for angle in (18, 162):  # Rounded ends.
        ex = cx + rx * math.cos(math.radians(angle))
        ey = cy + ry * math.sin(math.radians(angle))
        d.ellipse(box(ex - w / 2, ey - w / 2, ex + w / 2, ey + w / 2), fill=INK)

    return img.resize((size, size), Image.LANCZOS)


def logo(width: int, height: int, icon_fraction: float) -> Image.Image:
    """The icon centred on a transparent canvas, for wide tiles and the splash screen."""
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    side = round(min(width, height) * icon_fraction)
    canvas.paste(icon(side), ((width - side) // 2, (height - side) // 2))
    return canvas


SCALES = {"100": 1.0, "125": 1.25, "150": 1.5, "200": 2.0, "400": 4.0}


def main():
    OUT.mkdir(exist_ok=True)
    for old in OUT.glob("*.png"):
        old.unlink()

    for scale, factor in SCALES.items():
        icon(round(44 * factor)).save(OUT / f"Square44x44Logo.scale-{scale}.png")
        # Square tiles: the icon has its own rounded background, so it fills most of the tile.
        logo(round(150 * factor), round(150 * factor), 0.78).save(OUT / f"Square150x150Logo.scale-{scale}.png")
        logo(round(310 * factor), round(150 * factor), 0.78).save(OUT / f"Wide310x150Logo.scale-{scale}.png")
        icon(round(50 * factor)).save(OUT / f"StoreLogo.scale-{scale}.png")
        logo(round(620 * factor), round(300 * factor), 0.6).save(OUT / f"SplashScreen.scale-{scale}.png")

    # Taskbar, Start menu list and Alt+Tab use exact pixel sizes. "Unplated" means Windows
    # draws the icon as is, without putting it on an accent-coloured square.
    for size in (16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256):
        img = icon(size)
        img.save(OUT / f"Square44x44Logo.targetsize-{size}.png")
        img.save(OUT / f"Square44x44Logo.targetsize-{size}_altform-unplated.png")
        img.save(OUT / f"Square44x44Logo.targetsize-{size}_altform-lightunplated.png")

    icon(1024).save(Path(__file__).parent / "pouchy-1024.png")
    print(f"Wrote {len(list(OUT.glob('*.png')))} images to {OUT}")


if __name__ == "__main__":
    main()
