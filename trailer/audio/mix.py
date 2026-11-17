"""Music, sound effects and the final mixes, all generated from src/timeline.json.

Everything here is synthesised from scratch (original, free to ship), except Pouchy's own interface
sounds, rendered by tools/AppSounds from the app's SoundSynth. Voice lines come from audio/vo.py.

The music is chosen with --music:
  phonk (default)  the phonk / Brazilian-funk track in audio/phonk.py
  classic          the original synth-pop track below
  path/to/song.wav any track you have the rights to; it's trimmed or padded to the trailer and
                   faded out at the end (start it where you want with --music-offset SECONDS)

Writes audio/build/{music,sfx,vo}.wav and the mixes:
  public/audio/mix-full.wav      music + sfx + voice
  audio/build/mix-novoice.wav    music + sfx
  audio/build/mix-musicless.wav  sfx + voice
  audio/build/mix-sfx.wav        sfx only (to post with a sound from Instagram's or TikTok's own library)
"""
import argparse
import json
import sys
from pathlib import Path

import numpy as np
import soundfile as sf
from scipy.signal import butter, lfilter, resample_poly, fftconvolve

HERE = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(Path(__file__).resolve().parent))
ap = argparse.ArgumentParser()
ap.add_argument('--music', default='phonk')
ap.add_argument('--music-offset', type=float, default=0.0)
ap.add_argument('--duck', type=float, default=-5.0, help='music level under the voice, in dB')
ARGS = ap.parse_args()
BUILD = HERE / 'audio' / 'build'
SR = 48000
tl = json.loads((HERE / 'src' / 'timeline.json').read_text(encoding='utf-8'))
DUR = tl['duration']
N = int(DUR * SR) + SR
BEAT = 60 / tl['bpm']
rng = np.random.default_rng(7)


def t_(sec):
    return np.arange(int(sec * SR)) / SR


def env(n, attack=0.005, decay=0.2):
    t = np.arange(n) / SR
    return np.minimum(1, t / max(attack, 1e-4)) * np.exp(-t / decay)


def lp(x, hz, order=2):
    b, a = butter(order, min(hz, SR / 2 - 100) / (SR / 2), 'low')
    return lfilter(b, a, x)


def hp(x, hz, order=2):
    b, a = butter(order, hz / (SR / 2), 'high')
    return lfilter(b, a, x)


def bp(x, lo, hi):
    b, a = butter(2, [lo / (SR / 2), min(hi, SR / 2 - 100) / (SR / 2)], 'band')
    return lfilter(b, a, x)


def place(buf, sound, at, gain=1.0):
    i = int(at * SR)
    if i >= len(buf) or i + len(sound) <= 0:
        return
    s = sound[max(0, -i):]
    i = max(0, i)
    n = min(len(s), len(buf) - i)
    buf[i:i + n] += s[:n] * gain


def note(n):
    """MIDI note → Hz."""
    return 440 * 2 ** ((n - 69) / 12)


def saw(f, sec, detune=0.0):
    t = t_(sec)
    ph = rng.random()
    return 2 * ((t * f * (1 + detune) + ph) % 1) - 1


IR = rng.standard_normal(int(1.6 * SR)) * np.exp(-np.arange(int(1.6 * SR)) / SR / 0.45)
IR = lp(IR, 6000) * 0.08


def reverb(x, wet=0.25):
    return x + fftconvolve(x, IR)[:len(x)] * wet

# ------------------------------------------------------------------------------------------ drums


def kick():
    t = t_(0.45)
    f = 45 + 110 * np.exp(-t / 0.035)
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.22)
    click = hp(rng.standard_normal(len(t)), 2000) * np.exp(-t / 0.004) * 0.3
    return np.tanh((body + click) * 1.6) * 0.9


def clap():
    n = int(0.3 * SR)
    noise = bp(rng.standard_normal(n), 900, 5000)
    e = np.zeros(n)
    for k, d in enumerate([0, 0.011, 0.022]):
        i = int(d * SR)
        e[i:] += np.exp(-np.arange(n - i) / SR / (0.012 if k < 2 else 0.12))
    return noise * e * 0.5


def hat(open_=False):
    n = int((0.22 if open_ else 0.05) * SR)
    return hp(rng.standard_normal(n), 7000) * env(n, 0.001, 0.07 if open_ else 0.014) * 0.28


def snare():
    n = int(0.25 * SR)
    t = np.arange(n) / SR
    tone = np.sin(2 * np.pi * 190 * t) * np.exp(-t / 0.05)
    return (bp(rng.standard_normal(n), 1500, 8000) * np.exp(-t / 0.09) * 0.5 + tone * 0.4) * 0.6

# ------------------------------------------------------------------------------------------ music

# vi–IV–I–V in C (Am, F, C, G), one chord per bar.
CHORDS = [[57, 60, 64, 69], [53, 57, 60, 65], [48, 55, 60, 64], [55, 59, 62, 67]]
ROOTS = [45, 41, 48, 43]


def chord_at(sec):
    return int(sec // (4 * BEAT)) % 4


def pad(notes, sec, bright):
    x = sum(saw(note(n), sec, d) for n in notes for d in (-0.006, 0, 0.007)) / (len(notes) * 3)
    x = lp(x, bright)
    n = len(x)
    a = np.minimum(1, np.arange(n) / (0.08 * SR))
    r = np.minimum(1, (n - np.arange(n)) / (0.12 * SR))
    return x * a * r * 0.5


def pluck(f, sec=0.35, bright=4000):
    x = saw(f, sec) * env(int(sec * SR), 0.002, 0.11)
    return lp(x, bright) * 0.4


def bell(f, sec=1.2):
    t = t_(sec)
    m = np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t / 0.3) * 1.2
    return np.sin(2 * np.pi * f * t + m) * np.exp(-t / 0.5) * 0.25


def bass(f, sec):
    x = saw(f, sec) * 0.6 + np.sin(2 * np.pi * f * t_(sec)) * 0.6
    return lp(x, 380) * env(len(x), 0.004, sec * 0.8)


def music_classic():
    m = np.zeros(N)
    S = tl['scenes']
    groove = (S['drop'][0], S['burst'][0])        # full groove
    light = (S['themes'][0], S['triad'][0])       # half-time
    build = (S['magic'][0], S['drop'][0])         # build-up
    triad = (S['triad'][0], S['end'][0])
    end = S['end'][0]

    beats = np.arange(0, DUR, BEAT)
    for b in beats:
        bar_chord = chord_at(b)
        in_groove = groove[0] <= b < groove[1]
        in_light = light[0] <= b < light[1]
        in_build = build[0] <= b < build[1]
        k = int(round(b / BEAT)) % 4
        if in_groove or (in_build and b >= build[0] + 2.5):
            place(m, kick(), b, 0.9 if in_groove else 0.6)
        if in_light and k in (0,):
            place(m, kick(), b, 0.75)
        if (in_groove and k in (1, 3)) or (in_light and k == 2):
            place(m, clap(), b, 0.75)
        if in_groove or in_light:
            place(m, hat(True), b + BEAT / 2, 0.55)
            for s in range(4):
                place(m, hat(), b + s * BEAT / 4, 0.4 if s % 2 else 0.25)
        if b < build[0]:
            place(m, hat(), b + BEAT / 2, 0.3)          # tick-tock in the hook
        if in_build:
            for s in range(2):
                place(m, hat(), b + s * BEAT / 2, 0.3)
        # Bass on the off-beats in the groove.
        if in_groove:
            place(m, bass(note(ROOTS[bar_chord]), BEAT * 0.4), b + BEAT / 2, 0.75)
        if in_light and k in (0, 2):
            place(m, bass(note(ROOTS[bar_chord]), BEAT * 0.9), b, 0.6)

    # Pads, one chord per bar; dark in the hook, opening up through the build.
    bar = 4 * BEAT
    for b in np.arange(0, end, bar):
        c = chord_at(b)
        bright = 600 if b < build[0] else (1400 if b < groove[0] else 3200)
        if triad[0] <= b < triad[1]:
            continue
        place(m, pad(CHORDS[c], bar + 0.1, bright), b, 0.35)

    # Arpeggios: gentle in the build, brighter in the groove, bells in the light section.
    for b in np.arange(build[0], groove[1], BEAT / 4):
        c = CHORDS[chord_at(b)]
        i = int(round(b / (BEAT / 4)))
        pitch = c[[0, 1, 2, 3, 2, 1][i % 6]] + 12
        if b < build[0] + 2.6 and i % 2:
            continue
        place(m, pluck(note(pitch), bright=2500 if b < groove[0] else 5000), b, 0.32 if b < groove[0] else 0.38)
    for b in np.arange(light[0], light[1], BEAT / 2):
        c = CHORDS[chord_at(b)]
        i = int(round(b / (BEAT / 2)))
        place(m, bell(note(c[i % 4] + 24)), b, 0.35)

    # Snare roll into the drop and into the burst.
    for start, length in ((S['drop'][0] - 1.0, 1.0), (S['burst'][0], S['themes'][0] - S['burst'][0])):
        steps = int(length / (BEAT / 4))
        for s in range(steps):
            place(m, snare(), start + s * BEAT / 4, 0.25 + 0.5 * s / steps)

    # Triad: a big chord stab on each word, then the final chord rings under the end card.
    for i in range(3):
        at = triad[0] + i
        place(m, pad(CHORDS[[0, 1, 3][i]], 0.9, 4000) * env(int(0.9 * SR), 0.002, 0.35) * 2.2, at, 0.6)
        place(m, bass(note(ROOTS[[0, 1, 3][i]] - 12), 0.8), at, 1.0)
    final = [48, 55, 60, 64, 67, 74]
    place(m, pad(final, DUR - end + 0.5, 2600), end, 0.55)
    place(m, bell(note(84), 3.0), end, 0.4)
    place(m, bell(note(79), 3.0), end + 0.25, 0.3)
    place(m, bass(note(36), 3.0), end, 0.9)
    m = reverb(m, 0.18)
    # Gentle fade at the very end.
    fade = np.ones(N)
    fe = int((DUR - 0.6) * SR)
    fade[fe:] = np.linspace(1, 0, N - fe)
    return m * fade

# ------------------------------------------------------------------------------------------ sfx


def sweep_noise(sec, f0, f1, shape='hump'):
    n = int(sec * SR)
    x = rng.standard_normal(n)
    out = np.zeros(n)
    blocks = 32
    for k in range(blocks):
        a, b = k * n // blocks, (k + 1) * n // blocks
        fc = f0 + (f1 - f0) * (k / blocks)
        out[a:b] = bp(x, max(80, fc * 0.6), fc * 1.6)[a:b]
    p = np.linspace(0, 1, n)
    e = np.sin(np.pi * p) ** 2 if shape == 'hump' else p ** 2
    return out * e


def make_sfx(name, length=None):
    if name.startswith('app:'):
        data, sr = sf.read(BUILD / 'app-sounds' / f'{name[4:]}.wav')
        return resample_poly(data, SR, sr) * 0.9
    if name == 'impact' or name == 'impact_big':
        big = name == 'impact_big'
        t = t_(1.2 if big else 0.7)
        f = 38 + 80 * np.exp(-t / 0.05)
        sub = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / (0.35 if big else 0.2))
        crack = lp(rng.standard_normal(len(t)), 3500) * np.exp(-t / 0.03)
        return reverb(np.tanh((sub + crack * 0.6) * 1.5) * (0.9 if big else 0.7), 0.4)
    if name == 'boom':
        t = t_(1.8)
        f = 32 + 60 * np.exp(-t / 0.08)
        return reverb(np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.6) + lp(rng.standard_normal(len(t)), 900) * np.exp(-t / 0.15) * 0.4, 0.5) * 0.9
    if name == 'whoosh':
        return sweep_noise(0.45, 600, 3500) * 0.6
    if name == 'whoosh_soft':
        return sweep_noise(0.6, 400, 1800) * 0.4
    if name == 'swoosh_up':
        return sweep_noise(0.35, 800, 6000, 'rise') * 0.5
    if name == 'riser':
        sec = length or 1.0
        t = t_(sec)
        f = 300 * 2 ** (t / sec * 2.5)
        tone = np.sin(2 * np.pi * np.cumsum(f) / SR) * 0.15
        return (sweep_noise(sec, 500, 7000, 'rise') * 0.6 + tone * (t / sec) ** 2)
    if name == 'click':
        n = int(0.03 * SR)
        return (np.sin(2 * np.pi * 2200 * np.arange(n) / SR) * 0.4 + hp(rng.standard_normal(n), 3000) * 0.5) * env(n, 0.0005, 0.006)
    if name == 'tick':
        n = int(0.025 * SR)
        return np.sin(2 * np.pi * 3200 * np.arange(n) / SR) * env(n, 0.0005, 0.006) * 0.5
    if name == 'pop':
        t = t_(0.09)
        f = 380 + 700 * (t / 0.09)
        return np.sin(2 * np.pi * np.cumsum(f) / SR) * env(len(t), 0.002, 0.035) * 0.6
    if name == 'pops':
        out = np.zeros(int(0.9 * SR))
        for k in range(10):
            p = make_sfx('pop')
            place(out, resample_poly(p, 100, 100 + int(rng.integers(-30, 40))), k * 0.08, 0.6)
        return out
    if name in ('shimmer', 'sparkle'):
        sec = 1.2 if name == 'shimmer' else 0.6
        out = np.zeros(int(sec * SR))
        for k in range(14 if name == 'shimmer' else 7):
            f = note(int(rng.choice([84, 86, 88, 91, 93, 96])))
            place(out, bell(f, 0.8) * 0.6, k * sec / 18, 0.5)
        return reverb(out, 0.5)
    if name == 'rattle':
        sec = length or 1.0
        t = t_(sec)
        am = (0.5 + 0.5 * np.sin(2 * np.pi * 13 * t)) ** 2
        return bp(rng.standard_normal(len(t)), 1200, 6000) * am * 0.35
    if name == 'shutter':
        out = np.zeros(int(0.15 * SR))
        place(out, make_sfx('click'), 0, 1)
        place(out, lp(rng.standard_normal(int(0.04 * SR)), 5000) * env(int(0.04 * SR), 0.001, 0.01), 0.06, 0.6)
        return out
    if name == 'typing':
        sec = length or 0.4
        out = np.zeros(int((sec + 0.05) * SR))
        tt = 0.0
        while tt < sec:
            place(out, make_sfx('click'), tt, 0.35 + 0.2 * rng.random())
            tt += 0.06 + rng.random() * 0.05
        return out
    raise SystemExit(f'unknown sfx {name}')


def sfx_track():
    out = np.zeros(N)
    for cue in tl['sfx']:
        place(out, make_sfx(cue['s'], cue.get('len')), cue['t'], cue['g'])
    return out


def vo_track():
    out = np.zeros(N)
    for line in tl['vo']:
        data, sr = sf.read(BUILD / 'vo' / f"{line['id']}.wav")
        data = resample_poly(data, SR, sr)
        place(out, reverb(data, 0.06), line['t'], 1.0)
    return out


def music():
    if ARGS.music == 'phonk':
        import phonk
        x = phonk.music(tl, SR)
    elif ARGS.music == 'classic':
        x = music_classic()
    else:
        data, sr = sf.read(ARGS.music)
        if data.ndim > 1:
            data = data.mean(axis=1)
        data = resample_poly(data, SR, sr) if sr != SR else data
        data = data[int(ARGS.music_offset * SR):]
        x = np.zeros(N)
        x[:min(N, len(data))] = data[:N]
        fe = int((DUR - 1.5) * SR)
        x[fe:] *= np.linspace(1, 0, N - fe) ** 2
        x = x / (np.max(np.abs(x)) + 1e-9) * 0.95
    out = np.zeros(N)
    out[:min(N, len(x))] = x[:N]
    return out


def duck(music, vo, amount_db=-7):
    """Lower the music while the voice speaks (smoothed)."""
    level = lp(np.abs(vo), 8) > 0.004
    g = np.where(level, 10 ** (amount_db / 20), 1.0)
    return music * lp(g.astype(float), 6)


def master(x, peak=-1.0):
    x = np.tanh(x * 1.1) / 1.1                      # soft clip the odd transient
    return x / (np.max(np.abs(x)) + 1e-9) * 10 ** (peak / 20)


def main():
    BUILD.mkdir(parents=True, exist_ok=True)
    (HERE / 'public' / 'audio').mkdir(parents=True, exist_ok=True)
    mus, fx, vo = music(), sfx_track(), vo_track()
    for name, x in (('music', mus), ('sfx', fx), ('vo', vo)):
        sf.write(BUILD / f'{name}.wav', x[:int(DUR * SR)].astype(np.float32), SR)
    mus_d = duck(mus, vo, ARGS.duck)
    mixes = {
        HERE / 'public' / 'audio' / 'mix-full.wav': mus_d * 0.6 + fx * 0.6 + vo * 1.15,
        BUILD / 'mix-novoice.wav': mus * 0.62 + fx * 0.55,
        BUILD / 'mix-musicless.wav': fx * 0.8 + vo * 1.15,
        BUILD / 'mix-sfx.wav': fx * 0.8,
    }
    for path, x in mixes.items():
        y = master(x[:int(DUR * SR)])
        sf.write(path, np.stack([y, y], axis=1).astype(np.float32), SR)
        print(path.name, f'{len(y) / SR:.2f}s')


main()
