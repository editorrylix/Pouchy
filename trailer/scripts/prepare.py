"""Gathers everything the trailer reads from public/ (all of it generated locally, none committed).

  footage/   the screen recordings from capture/record.ps1 (default C:\\PouchyTrailer\\footage)
  renders/   Pouchy's own views rendered by the test suite (POUCHY_RENDER_DIR, see EDITING.md)
  brand/     the app icon from packaging/store

Usage: python scripts/prepare.py [--footage DIR] [--renders DIR]
"""
import argparse
import shutil
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent.parent
REPO = HERE.parent
PUBLIC = HERE / 'public'

ap = argparse.ArgumentParser()
ap.add_argument('--footage', default=r'C:\PouchyTrailer\footage')
ap.add_argument('--renders', default=r'C:\PouchyTrailer\renders')
args = ap.parse_args()

for sub in ('footage', 'renders', 'brand'):
    (PUBLIC / sub).mkdir(parents=True, exist_ok=True)

clips = sorted(Path(args.footage).glob('*.mp4'))
for clip in clips:
    shutil.copy2(clip, PUBLIC / 'footage' / clip.name)
print(f'{len(clips)} clips')

for theme in ('midnight', 'aurora', 'bubblegum', 'acid', 'sunset', 'terminal', 'paper', 'frost'):
    shutil.copy2(Path(args.renders) / f'{theme}-grid.png', PUBLIC / 'renders' / f'{theme}-grid.png')

# The mascot render is the four moods side by side on a transparent background: split it into
# one image per mood by finding the gaps between them.
sheet = Image.open(Path(args.renders) / 'mascot.png').convert('RGBA')
alpha = np.asarray(sheet)[:, :, 3]
cols = np.where(alpha.max(axis=0) > 8)[0]
rows = np.where(alpha.max(axis=1) > 8)[0]
runs, start = [], cols[0]
for a, b in zip(cols, cols[1:]):
    if b - a > 6:
        runs.append((start, a))
        start = b
runs.append((start, cols[-1]))
if len(runs) != 4:
    raise SystemExit(f'expected 4 mascots in mascot.png, found {len(runs)}')
top, bottom = rows[0], rows[-1]
for i, (x0, x1) in enumerate(runs):
    w = x1 - x0 + 1
    h = bottom - top + 1
    side = max(w, h) + 16
    cell = Image.new('RGBA', (side, side))
    cell.paste(sheet.crop((x0, top, x1 + 1, bottom + 1)), ((side - w) // 2, (side - h) // 2))
    cell.save(PUBLIC / 'renders' / f'mascot-{i}.png')
print('mascots', runs)

shutil.copy2(REPO / 'packaging' / 'store' / 'pouchy-1024.png', PUBLIC / 'brand' / 'pouchy-1024.png')
print('ok')
