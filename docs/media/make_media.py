"""Builds the README and guide images from Pouchy's own views, rendered by its test suite.

1. Render the views (transparent backgrounds, sample items only):
     $env:POUCHY_RENDER_DIR='C:\\PouchyTrailer\\renders'; $env:POUCHY_RENDER_MARKETING='1'
     dotnet test tests\\Pouchy.Tests --filter ViewSmokeTests
2. python docs/media/make_media.py [--renders DIR]   (Python 3 with Pillow and numpy)

Every picture of the app is the real app; only the backdrop, the tilt and the labels are added here.
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
ap = argparse.ArgumentParser()
ap.add_argument('--renders', default=r'C:\PouchyTrailer\renders')
ap.add_argument('--out', default=str(HERE))
args = ap.parse_args()
R = Path(args.renders)
OUT = Path(args.out)

FONTS = Path(r'C:\Windows\Fonts')
BLACK = str(FONTS / 'seguibl.ttf')     # Segoe UI Black
BOLD = str(FONTS / 'segoeuib.ttf')
SEMI = str(FONTS / 'seguisb.ttf')

INK = (14, 10, 31)
VIOLET = (139, 124, 255)
PINK = (255, 92, 138)
PEACH = (255, 138, 91)
TEAL = (79, 227, 193)
rng = np.random.default_rng(7)


# ------------------------------------------------------------------------------------------ pieces

def backdrop(w, h, blobs=None, corner=28):
    """Deep ink with soft coloured light, a little grain, and rounded corners."""
    y, x = np.mgrid[0:h, 0:w]
    t = (x / w * 0.6 + y / h * 0.4)
    top, bottom = np.array([20, 13, 44]), np.array([38, 16, 64])
    base = top[None, None, :] * (1 - t[..., None]) + bottom[None, None, :] * t[..., None]
    img = Image.fromarray(base.astype(np.uint8), 'RGB').convert('RGBA')
    glow = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(glow)
    for (cx, cy, r, color, a) in (blobs or [
        (0.12, 0.05, 0.42, VIOLET, 150),
        (0.92, 0.95, 0.45, PINK, 120),
        (0.55, 1.15, 0.40, PEACH, 90),
    ]):
        R_ = r * max(w, h)
        d.ellipse([cx * w - R_, cy * h - R_, cx * w + R_, cy * h + R_], fill=color + (a,))
    glow = glow.filter(ImageFilter.GaussianBlur(max(w, h) * 0.12))
    img.alpha_composite(glow)
    grain = (rng.normal(0, 1, (h, w)) * 2.2).clip(-5, 5)
    arr = np.asarray(img).astype(np.int16)
    arr[..., :3] = (arr[..., :3] + grain[..., None]).clip(0, 255)
    img = Image.fromarray(arr.astype(np.uint8), 'RGBA')
    if corner:
        mask = Image.new('L', (w, h), 0)
        ImageDraw.Draw(mask).rounded_rectangle([0, 0, w - 1, h - 1], corner, fill=255)
        img.putalpha(mask)
    return img


def render(name, scale=1.0):
    im = Image.open(R / f'{name}.png').convert('RGBA')
    im = im.crop(im.getbbox())
    if scale != 1.0:
        im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
    return im


def fit(name, max_w, max_h):
    im = render(name)
    s = min(max_w / im.width, max_h / im.height)
    return im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)


def put(canvas, im, cx, cy, angle=0.0):
    if angle:
        im = im.rotate(angle, resample=Image.BICUBIC, expand=True)
    canvas.alpha_composite(im, (round(cx - im.width / 2), round(cy - im.height / 2)))


def pill(canvas, text, cx, cy, size=30, fill=(255, 255, 255, 34), color=(255, 255, 255), font=SEMI, accent=None):
    f = ImageFont.truetype(font, size)
    l, t, r, b = f.getbbox(text)
    pad_x, pad_y = size * 0.75, size * 0.42
    w, h = r - l + pad_x * 2, size * 1.0 + pad_y * 2
    layer = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    box = [cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2]
    d.rounded_rectangle(box, h / 2, fill=accent + (255,) if accent else fill, outline=None if accent else (255, 255, 255, 60), width=2)
    d.text((cx - (r - l) / 2 - l, cy - size * 0.62), text, font=f, fill=color)
    canvas.alpha_composite(layer)


def text(canvas, xy, s, size, font=BLACK, fill=(255, 255, 255), anchor='la'):
    ImageDraw.Draw(canvas).text(xy, s, font=ImageFont.truetype(font, size), fill=fill, anchor=anchor)


def save(img, name, width=None):
    if width and img.width > width:
        img = img.resize((width, round(img.height * width / img.width)), Image.LANCZOS)
    path = OUT / name
    img.save(path, optimize=True)
    print(f'{name:22s} {img.size[0]}x{img.size[1]}  {path.stat().st_size // 1024} KB')


# ------------------------------------------------------------------------------------------ images

def hero():
    W, H = 1800, 1000
    c = backdrop(W, H)
    put(c, render('bubblegum-grid', 0.56), 470, 545, 9)
    put(c, render('aurora-grid', 0.56), 1330, 545, -9)
    put(c, render('midnight-grid', 0.68), 900, 520)
    save(c, 'hero.png')


THEMES = [('midnight', 'Midnight Glass'), ('aurora', 'Aurora'), ('bubblegum', 'Bubblegum'),
          ('acid', 'Acid'), ('sunset', 'Sunset'), ('frost', 'Frost'),
          ('paper', 'Paper'), ('terminal', 'Terminal'), ('system', 'Follow Windows')]


def themes():
    cols, cell_w, card_h, label_h, top = 3, 600, 640, 90, 40
    rows = (len(THEMES) + cols - 1) // cols
    W, H = cols * cell_w, top + rows * (card_h + label_h) + 30
    c = backdrop(W, H, [(0.1, 0.0, 0.4, VIOLET, 140), (0.95, 0.5, 0.4, PINK, 100), (0.2, 1.0, 0.4, TEAL, 70)])
    for i, (tid, name) in enumerate(THEMES):
        col, row = i % cols, i // cols
        cx = cell_w * col + cell_w / 2
        cy = top + row * (card_h + label_h) + card_h / 2
        put(c, fit(f'{tid}-grid', cell_w - 40, card_h), cx, cy)
        pill(c, name, cx, cy + card_h / 2 + 30, size=28)
    save(c, 'themes.png', width=1440)


def views():
    W, H = 1680, 920
    c = backdrop(W, H)
    for name, label, cx in (('midnight-grid', 'Grid', 300), ('midnight-list', 'List', 840), ('midnight-compact', 'Compact', 1380)):
        im = fit(name, 520, 740)
        put(c, im, cx, 420)
        pill(c, label, cx, 860, size=30)
    save(c, 'views.png')


def actions():
    W, H = 1800, 900
    c = backdrop(W, H)
    put(c, fit('readme-actions', 600, 820), 520, 430)
    put(c, fit('readme-palette', 760, 760), 1240, 400)
    pill(c, 'Drop on a tile to run it', 520, 850, 28)
    pill(c, 'Ctrl + K: the command palette', 1240, 850, 28)
    save(c, 'actions.png')


def organise():
    W, H = 1800, 900
    c = backdrop(W, H)
    put(c, fit('search', 560, 760), 330, 420)
    put(c, fit('undo', 560, 760), 900, 420)
    put(c, fit('picker-icon', 560, 420), 1470, 420)
    pill(c, 'Ctrl + F searches every shelf', 330, 850, 26)
    pill(c, 'Removed something? Undo', 900, 850, 26)
    pill(c, 'An icon for every shelf', 1470, 850, 26)
    save(c, 'organise.png')


def menus():
    W, H = 1800, 920
    c = backdrop(W, H)
    put(c, fit('selected', 560, 800), 330, 440)
    put(c, fit('menu-image', 600, 820), 900, 440)
    put(c, fit('menu-tray', 560, 520), 1470, 440)
    pill(c, 'Pick several items', 330, 870, 26)
    pill(c, 'A menu for each kind of item', 900, 870, 26)
    pill(c, 'And one in the tray', 1470, 870, 26)
    save(c, 'menus.png')


def mascot():
    W, H = 1440, 430
    c = backdrop(W, H, [(0.2, 0.0, 0.5, VIOLET, 130), (0.85, 1.0, 0.5, PINK, 110)])
    put(c, fit('mascot', 1300, 330), W / 2, H / 2)
    save(c, 'mascot.png')


def social():
    W, H = 1280, 640
    c = backdrop(W, H, corner=0)
    icon = Image.open(REPO / 'packaging' / 'store' / 'pouchy-1024.png').convert('RGBA').resize((150, 150), Image.LANCZOS)
    c.alpha_composite(icon, (80, 120))
    text(c, (72, 270), 'Pouchy', 128)
    text(c, (80, 430), 'The cute drop shelf for Windows', 38, SEMI, (235, 230, 255))
    for label, x in (('Shake to summon', 80), ('Drop anywhere', 365), ('9 themes', 620)):
        f = ImageFont.truetype(SEMI, 26)
        w = f.getbbox(label)[2] + 40
        pill(c, label, x + w / 2, 520, 26)
    put(c, render('midnight-grid', 0.5), 1040, 330, -7)
    save(c, 'social-preview.png')


if __name__ == '__main__':
    OUT.mkdir(parents=True, exist_ok=True)
    for make in (hero, themes, views, actions, organise, menus, mascot, social):
        make()
