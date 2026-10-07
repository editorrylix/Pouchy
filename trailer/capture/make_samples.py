"""Creates the sample files the trailer footage is recorded with (real files in a real folder).

    python trailer/capture/make_samples.py <folder>

Makes <folder>\\Photos (pictures, a note, a "screenshot" with text for OCR) and <folder>\\Project
(an empty destination folder). The pictures are simple generated landscapes, so nothing here has
licensing questions.
"""
import math
import random
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont

W, H = 1600, 1066


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def sky(top, bottom):
    img = Image.new("RGB", (W, H))
    d = ImageDraw.Draw(img)
    for y in range(H):
        d.line([(0, y), (W, y)], fill=lerp(top, bottom, y / H))
    return img


def ridge(d, base, amp, freq, color, seed):
    rnd = random.Random(seed)
    phases = [rnd.random() * 6.28 for _ in range(3)]
    points = [(0, H)]
    for x in range(0, W + 8, 8):
        y = base + sum(amp / (i + 1) * math.sin(x * freq * (i + 1) / W * 6.28 + phases[i]) for i in range(3))
        points.append((x, y))
    points.append((W, H))
    d.polygon(points, fill=color)


def sunset():
    img = sky((44, 26, 84), (255, 140, 90))
    d = ImageDraw.Draw(img)
    d.ellipse([W * 0.58, H * 0.42, W * 0.58 + 260, H * 0.42 + 260], fill=(255, 214, 140))
    ridge(d, H * 0.66, 70, 1.1, (92, 40, 90), 1)
    ridge(d, H * 0.76, 50, 1.7, (58, 24, 64), 2)
    ridge(d, H * 0.88, 30, 2.3, (30, 14, 38), 3)
    return img


def beach():
    img = sky((96, 190, 255), (210, 240, 255))
    d = ImageDraw.Draw(img)
    d.ellipse([W * 0.72, H * 0.12, W * 0.72 + 170, H * 0.12 + 170], fill=(255, 246, 200))
    d.rectangle([0, H * 0.55, W, H * 0.72], fill=(40, 150, 210))
    for i in range(14):
        y = H * 0.56 + i * 12
        d.line([(0, y), (W, y + 6)], fill=(70, 175, 230), width=2)
    d.polygon([(0, H * 0.72), (W, H * 0.66), (W, H), (0, H)], fill=(246, 214, 160))
    d.polygon([(0, H * 0.70), (W, H * 0.64), (W, H * 0.67), (0, H * 0.73)], fill=(255, 255, 255))
    return img


def mountains():
    img = sky((120, 170, 255), (230, 236, 255))
    d = ImageDraw.Draw(img)
    for i, (x, h, c) in enumerate([(0.2, 0.30, (90, 110, 160)), (0.55, 0.22, (70, 90, 140)), (0.85, 0.34, (100, 120, 170))]):
        peak = (W * x, H * h)
        d.polygon([(peak[0] - W * 0.38, H * 0.8), peak, (peak[0] + W * 0.38, H * 0.8)], fill=c)
        d.polygon([(peak[0] - 70, peak[1] + 80), peak, (peak[0] + 70, peak[1] + 80)], fill=(245, 248, 255))
    ridge(d, H * 0.80, 25, 1.4, (60, 120, 90), 7)
    ridge(d, H * 0.90, 18, 2.2, (40, 96, 70), 8)
    return img


def forest():
    img = sky((180, 230, 210), (240, 250, 235))
    d = ImageDraw.Draw(img)
    rnd = random.Random(4)
    for layer, (color, base) in enumerate([((120, 170, 140), 0.55), ((70, 130, 100), 0.68), ((34, 86, 64), 0.82)]):
        for _ in range(26):
            x = rnd.random() * W
            h = H * (0.22 + rnd.random() * 0.18)
            y = H * base
            d.polygon([(x - h * 0.28, y), (x, y - h), (x + h * 0.28, y)], fill=color)
        d.rectangle([0, H * base, W, H], fill=color)
    return img


def city():
    img = sky((16, 18, 52), (70, 40, 110))
    d = ImageDraw.Draw(img)
    rnd = random.Random(9)
    x = 0
    while x < W:
        w = rnd.randint(70, 160)
        h = rnd.randint(int(H * 0.25), int(H * 0.62))
        d.rectangle([x, H - h, x + w, H], fill=(28, 26, 60))
        for wy in range(H - h + 20, H - 20, 34):
            for wx in range(x + 12, x + w - 12, 26):
                if rnd.random() < 0.45:
                    d.rectangle([wx, wy, wx + 10, wy + 14], fill=(255, 210, 120) if rnd.random() < 0.8 else (140, 200, 255))
        x += w + 8
    return img.filter(ImageFilter.GaussianBlur(0.6))


def font(size, bold=False):
    for name in (["segoeuib.ttf", "arialbd.ttf"] if bold else ["segoeui.ttf", "arial.ttf"]):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def note_card():
    """A picture with text on it, for the 'Extract text' (OCR) shot."""
    img = Image.new("RGB", (1400, 800), (250, 247, 240))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([60, 60, 1340, 740], radius=40, fill=(255, 255, 255), outline=(225, 220, 210), width=4)
    d.text((140, 150), "Design review", font=font(92, True), fill=(30, 30, 40))
    d.text((140, 300), "Thursday 4:30 pm", font=font(70), fill=(60, 60, 75))
    d.text((140, 400), "Studio B, second floor", font=font(70), fill=(60, 60, 75))
    d.text((140, 540), "Bring the new icons!", font=font(70, True), fill=(139, 92, 246))
    return img


def main():
    root = Path(sys.argv[1] if len(sys.argv) > 1 else "samples")
    photos = root / "Photos"
    project = root / "Project"
    photos.mkdir(parents=True, exist_ok=True)
    project.mkdir(parents=True, exist_ok=True)
    for old in list(photos.iterdir()) + list(project.iterdir()):
        old.unlink()

    for name, make in [("Sunset", sunset), ("Beach", beach), ("Mountains", mountains), ("Forest", forest), ("City lights", city)]:
        make().save(photos / f"{name}.jpg", quality=92)
    note_card().save(photos / "Meeting note.png")
    (photos / "Ideas.txt").write_text(
        "Trailer ideas\n\n- Shake to summon\n- Park anything\n- Drop it anywhere\n", encoding="utf-8")
    print(f"Samples in {root.resolve()}")


if __name__ == "__main__":
    main()


def backdrop(path, width=1920, height=1080):
    """The full-screen backdrop shown behind the windows while recording (hides the real desktop)."""
    import random as _r
    base = Image.new("RGB", (width, height))
    d = ImageDraw.Draw(base)
    for y in range(height):
        d.line([(0, y), (width, y)], fill=lerp((34, 26, 78), (92, 44, 120), y / height))
    glow = Image.new("RGB", (width, height), (0, 0, 0))
    g = ImageDraw.Draw(glow)
    for (cx, cy, r, color) in [(0.15, 0.2, 520, (90, 110, 255)), (0.85, 0.9, 640, (255, 120, 140)),
                               (0.7, 0.15, 420, (170, 110, 255)), (0.25, 0.95, 380, (120, 70, 200))]:
        g.ellipse([cx * width - r, cy * height - r, cx * width + r, cy * height + r], fill=color)
    glow = glow.filter(ImageFilter.GaussianBlur(220))
    img = Image.blend(base, glow, 0.55)
    noise = Image.effect_noise((width, height), 6).convert("RGB")
    img = Image.blend(img, noise, 0.03)
    img.save(path)


if __name__ == "__main__" and len(sys.argv) > 2 and sys.argv[2] == "--backdrop":
    backdrop(Path(sys.argv[1]) / "backdrop.png")
