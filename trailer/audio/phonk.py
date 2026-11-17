"""An original phonk / Brazilian-funk style track for the trailer, synthesised from scratch.

The sound people know from Instagram and TikTok edits: a pitched 808-cowbell riff, a distorted
808 that slides, a funk kick pattern, snappy claps, rolling hats, a "slowed + reverb" beat switch
and hard stops. Everything is generated here (no samples, no copied melody), so it's free to ship.
The only outside sound is the word "Pouchy", spoken by the local Kokoro voice and chopped up.

Timed to src/timeline.json (120 BPM, a 16th note is 0.125 s), so every cut lands on the beat:

  hook   0.0   muffled cowbell riff, ticking hats
  magic  3.0   build: the filter opens, hats speed up, a stop before the drop
  drop  10.0   the drop: full beat under "Drop it anywhere"
  burst 23.5   break: "Pou-pou-pou-Pouchy" stutter and a riser
  themes 24.5  beat switch: half-time, slowed and pitched down
  triad 28.5   three hits with silence between (SHAKE. PARK. DROP.)
  end   31.5   808 slide and the riff echoing out

Usage from mix.py: music(tl, SR) -> mono float array (length tl['duration'] + 1 s).
Run on its own to write audio/build/music-phonk.wav for listening.
"""
import json
from pathlib import Path

import numpy as np
from scipy.signal import butter, lfilter, sosfilt, fftconvolve, resample_poly

HERE = Path(__file__).resolve().parent.parent
rng = np.random.default_rng(1312)

SR = 48000
STEP = 0.125            # one 16th at 120 BPM
BAR = 2.0


# ------------------------------------------------------------------------------------------ dsp

def t_(sec):
    return np.arange(int(sec * SR)) / SR


def env(n, attack=0.002, decay=0.2):
    t = np.arange(n) / SR
    return np.minimum(1, t / max(attack, 1e-4)) * np.exp(-t / decay)


def lp(x, hz, order=2):
    sos = butter(order, min(hz, SR / 2 - 200) / (SR / 2), 'low', output='sos')
    return sosfilt(sos, x)


def hp(x, hz, order=2):
    sos = butter(order, hz / (SR / 2), 'high', output='sos')
    return sosfilt(sos, x)


def bp(x, lo, hi, order=2):
    sos = butter(order, [lo / (SR / 2), min(hi, SR / 2 - 200) / (SR / 2)], 'band', output='sos')
    return sosfilt(sos, x)


def sweep_lp(x, f0, f1):
    """Low-pass whose cutoff moves from f0 to f1 (exponentially) across the signal."""
    out = np.zeros_like(x)
    blocks = 64
    n = len(x)
    zi = None
    for k in range(blocks):
        a, b = k * n // blocks, (k + 1) * n // blocks
        fc = f0 * (f1 / f0) ** (k / (blocks - 1))
        bb, aa = butter(2, min(fc, SR / 2 - 200) / (SR / 2), 'low')
        if zi is None:
            zi = np.zeros(2)
        out[a:b], zi = lfilter(bb, aa, x[a:b], zi=zi)
    return out


def place(buf, sound, at, gain=1.0):
    i = int(round(at * SR))
    if i >= len(buf) or i + len(sound) <= 0:
        return
    s = sound[max(0, -i):]
    i = max(0, i)
    n = min(len(s), len(buf) - i)
    buf[i:i + n] += s[:n] * gain


def hz(midi):
    return 440.0 * 2 ** ((midi - 69) / 12)


def square(f, sec):
    """Band-limited square wave (odd harmonics up to 16 kHz), so the cowbell doesn't alias."""
    t = t_(sec)
    out = np.zeros_like(t)
    k = 1
    while k * f < 16000:
        out += np.sin(2 * np.pi * k * f * t) / k
        k += 2
    return out * (4 / np.pi)


_IR_CACHE = {}


def reverb(x, wet=0.3, size=1.6, damp=5000):
    key = (size, damp)
    if key not in _IR_CACHE:
        n = int(size * SR)
        ir = rng.standard_normal(n) * np.exp(-np.arange(n) / SR / (size / 4.5))
        _IR_CACHE[key] = lp(ir, damp) * 0.06
    return x + fftconvolve(x, _IR_CACHE[key])[:len(x)] * wet


def echo(x, delay, feedback=0.4, taps=4, tone=3500):
    out = x.copy()
    d = int(delay * SR)
    tap = x
    for k in range(1, taps + 1):
        tap = lp(tap, tone) * feedback
        pad = np.zeros(len(x))
        if k * d < len(x):
            pad[k * d:] = tap[:len(x) - k * d]
        out += pad
    return out


def saturate(x, drive):
    return np.tanh(x * drive) / np.tanh(drive)


# ------------------------------------------------------------------------------------------ instruments

def cowbell(midi, sec=0.32, bright=1.0):
    """The 808 cowbell (two detuned squares, ratio ~1.48, through a band-pass), pitched to a note."""
    f = hz(midi)
    x = square(f, sec) * 0.6 + square(f * 1.4815, sec) * 0.4
    x = bp(x, f * 0.9, min(f * 4.2 * bright + 400, 14000))
    n = len(x)
    t = np.arange(n) / SR
    e = np.minimum(1, t / 0.0015) * (0.62 * np.exp(-t / 0.012) + 0.38 * np.exp(-t / 0.11))
    return saturate(x * e, 1.6) * 0.5


def kick(punch=1.0):
    t = t_(0.32)
    f = 48 + 150 * np.exp(-t / 0.028)
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.16)
    click = hp(rng.standard_normal(len(t)), 3000) * np.exp(-t / 0.003) * 0.35 * punch
    return saturate(body + click, 2.2) * 0.85


def bass808(midi, sec, glide_to=None, glide_at=0.6, drive=3.2):
    """A long 808: pitch drop at the start, optional slide to another note, then distorted."""
    t = t_(sec)
    f0 = hz(midi)
    f = np.full(len(t), f0)
    f *= 1 + 1.6 * np.exp(-t / 0.018)              # the punch at the start
    if glide_to is not None:
        g = np.clip((t - sec * glide_at) / (sec * (1 - glide_at) * 0.7), 0, 1)
        g = g * g * (3 - 2 * g)
        f *= (hz(glide_to) / f0) ** g
    ph = 2 * np.pi * np.cumsum(f) / SR
    x = np.sin(ph) + 0.18 * np.sin(2 * ph)
    e = np.minimum(1, t / 0.003) * np.exp(-t / (sec * 1.4 + 0.05))
    r = np.minimum(1, (len(t) - np.arange(len(t))) / (0.012 * SR))
    x = saturate(x * e, drive) * r
    # Keep the sub clean and let the distortion add the bite you hear on phone speakers.
    return lp(x, 2600) * 0.62


def clap():
    n = int(0.28 * SR)
    noise = bp(rng.standard_normal(n), 900, 6500)
    e = np.zeros(n)
    for k, d in enumerate([0, 0.009, 0.019, 0.028]):
        i = int(d * SR)
        e[i:] += np.exp(-np.arange(n - i) / SR / (0.008 if k < 3 else 0.09))
    return reverb(noise * e * 0.55, 0.25, 0.8)


def snap():
    n = int(0.08 * SR)
    t = np.arange(n) / SR
    tone = np.sin(2 * np.pi * 1850 * t) * np.exp(-t / 0.012)
    noise = hp(rng.standard_normal(n), 2500) * np.exp(-t / 0.018)
    return (tone * 0.35 + noise * 0.6) * 0.6


def hat(open_=False, tone=8500):
    n = int((0.16 if open_ else 0.045) * SR)
    x = hp(rng.standard_normal(n), tone)
    return x * env(n, 0.0005, 0.06 if open_ else 0.011) * 0.26


def snare():
    n = int(0.22 * SR)
    t = np.arange(n) / SR
    tone = np.sin(2 * np.pi * 200 * t) * np.exp(-t / 0.04)
    return (bp(rng.standard_normal(n), 1800, 9000) * np.exp(-t / 0.07) * 0.55 + tone * 0.35) * 0.7


def crash():
    n = int(1.8 * SR)
    x = hp(rng.standard_normal(n), 5000)
    return reverb(x * env(n, 0.001, 0.55) * 0.22, 0.3)


def reverse_cymbal(sec=1.0):
    return crash()[:int(sec * SR)][::-1] * 0.8


def riser(sec):
    t = t_(sec)
    p = t / sec
    noise = sweep_lp(hp(rng.standard_normal(len(t)), 300), 400, 9000) * p ** 2
    f = 220 * 2 ** (p * 2.0)
    tone = np.sin(2 * np.pi * np.cumsum(f) / SR) * 0.12 * p ** 2
    return noise * 0.32 + tone


def sub_drone(sec, midi=33):
    t = t_(sec)
    x = np.sin(2 * np.pi * hz(midi) * t) * 0.5
    a = np.minimum(1, t / 0.6)
    r = np.minimum(1, (len(t) - np.arange(len(t))) / (0.3 * SR))
    return x * a * r


def vinyl(sec):
    """Crackle and hiss, for the muffled intro."""
    n = int(sec * SR)
    hiss = lp(rng.standard_normal(n), 6000) * 0.012
    crackle = np.zeros(n)
    for i in rng.integers(0, n, int(sec * 14)):
        k = min(n - i, 60)
        crackle[i:i + k] += rng.standard_normal(k) * np.exp(-np.arange(k) / 8) * rng.uniform(0.05, 0.25)
    return hiss + hp(crackle, 1500)


# ------------------------------------------------------------------------------------------ the song

# A natural minor, with a borrowed G# over the E chord (harmonic minor) for that phonk tension.
# Four bars, eight seconds: Am | Am | F | E. Each riff is 32 sixteenths (two bars).
RIFF_A = {0: 69, 3: 72, 6: 69, 8: 67, 10: 69, 12: 72, 14: 74,
          16: 76, 19: 74, 22: 72, 24: 69, 26: 67, 28: 64, 30: 67}
RIFF_B = {0: 65, 3: 69, 6: 65, 8: 64, 10: 65, 12: 69, 14: 72,
          16: 68, 19: 71, 22: 68, 24: 64, 26: 68, 28: 71, 30: 74}
ROOTS = [33, 33, 29, 28]          # A1 A1 F1 E1

# Funk ("tamborzão"-style) kick pattern on 16ths, and where the claps and snaps go.
KICKS = [0, 3, 6, 10, 13]
CLAPS = [4, 12]
SNAPS = [7, 15]


def riff_notes(start, end, slowed=0):
    """(time, midi) for the cowbell riff from start, looping every 8 s, cut at end."""
    notes = []
    loop = 0
    while True:
        base = start + loop * 4 * BAR
        for half, riff in ((0, RIFF_A), (1, RIFF_B)):
            for step, n in sorted(riff.items()):
                t = base + half * 2 * BAR + step * STEP
                if t >= end:
                    return notes
                notes.append((t, n - slowed))
        loop += 1


def root_at(start, t):
    return ROOTS[int((t - start) // BAR) % 4]


def music(tl, sr=SR, chops_dir=None):
    assert sr == SR
    dur = tl['duration']
    N = int(dur * SR) + SR
    S = tl['scenes']
    drop = S['drop'][0]
    burst, themes, triad, end = S['burst'][0], S['themes'][0], S['triad'][0], S['end'][0]
    build = S['magic'][0]

    bells = np.zeros(N)      # cowbell riff (gets its own filter moves)
    drums = np.zeros(N)
    low = np.zeros(N)        # kick + 808 (sidechain source)
    fx = np.zeros(N)
    vox = np.zeros(N)

    # ---- hook + build: the riff from "another room", opening up towards the drop.
    intro_riff = np.zeros(N)
    for t, n in riff_notes(0.0, drop - 0.25):
        place(intro_riff, cowbell(n, 0.3, bright=0.6), t, 0.9)
    i0, i1 = 0, int((drop - 0.25) * SR)
    seg = intro_riff[i0:i1]
    cut = int(build * SR)
    seg[:cut] = lp(seg[:cut], 700)                        # muffled under the hook
    seg[cut:] = sweep_lp(seg[cut:], 700, 7000)             # opens through the build
    bells[i0:i1] += reverb(seg, 0.35, 2.2)
    place(fx, vinyl(build + 0.5), 0.0, 1.0)
    place(fx, sub_drone(build, 33), 0.0, 0.5)

    # Tick-tock under the hook, then the hats speed up through the build.
    for b in np.arange(0, build, 0.5):
        place(drums, hat(tone=9500), b + 0.25, 0.55)
    for b in np.arange(build, drop - 0.25, STEP):
        k = int(round((b - build) / STEP))
        progress = (b - build) / (drop - build)
        if progress < 0.3 and k % 4:
            continue
        if progress < 0.6 and k % 2:
            continue
        place(drums, hat(), b, 0.35 + 0.35 * progress + (0.15 if k % 4 == 0 else 0))
    # Phonk triplet hat roll and a snare roll into the drop.
    for k in range(6):
        place(drums, hat(), drop - 1.5 + k * (0.5 / 6), 0.55)
    for k in range(8):
        place(drums, snare(), drop - 1.0 + k * STEP * 0.75, 0.2 + 0.08 * k)
    # 808 hits on the downbeats of the build, so it already moves.
    for t in np.arange(build, drop - 1, BAR):
        place(low, bass808(ROOTS[int((t - build) // BAR) % 4], 0.9, drive=2.4), t, 0.75)
        place(low, kick(), t, 0.7)
    place(fx, riser(2.0), drop - 2.0, 0.55)
    place(fx, reverse_cymbal(1.0), drop - 1.0, 0.6)
    # (drop - 0.25 .. drop is left empty: the stop before the drop.)

    # ---- the drop and the montage: the full beat.
    groove_end = burst
    for t, n in riff_notes(drop, groove_end):
        place(bells, cowbell(n, 0.32), t, 1.0)
        place(bells, cowbell(n + 12, 0.18, 1.2), t, 0.18)   # a thin octave on top for shine
    beat = drop
    bar_i = 0
    while beat < groove_end - 1e-6:
        root = ROOTS[bar_i % 4]
        last_bar = bar_i % 4 == 3
        for s in range(16):
            t = beat + s * STEP
            if t >= groove_end - 1e-6:
                break
            if s in KICKS:
                place(low, kick(), t, 0.95)
                length = (KICKS[KICKS.index(s) + 1] - s) * STEP if s != KICKS[-1] else 3 * STEP
                glide = root + 12 if (last_bar and s == 13) else None
                n = root + (12 if s == 10 else 0)
                place(low, bass808(n, length + 0.04, glide_to=glide, glide_at=0.3), t, 0.9)
            if s in CLAPS:
                place(drums, clap(), t, 0.9)
            if s in SNAPS:
                place(drums, snap(), t, 0.5)
            # 16th hats with accents; a 32nd roll at the end of every other bar.
            place(drums, hat(), t, 0.3 if s % 2 else 0.42)
            if s == 14 and bar_i % 2 == 1:
                place(drums, hat(), t + STEP / 2, 0.32)
                place(drums, hat(), t + STEP, 0.36)
                place(drums, hat(), t + STEP * 1.5, 0.4)
            if s % 4 == 2:
                place(drums, hat(open_=True), t, 0.2)
        beat += BAR
        bar_i += 1
    place(drums, crash(), drop, 0.9)
    place(low, bass808(33, 0.9, drive=4.0), drop, 0.3)    # extra weight on the first hit

    # ---- burst: the beat drops out; "Pou-pou-pou-Pouchy!" and a riser.
    place(fx, riser(themes - burst), burst, 0.6)
    chop = load_chop(chops_dir)
    if chop is not None:
        head = chop[:int(0.13 * SR)] * np.hanning(int(0.13 * SR) * 2)[int(0.13 * SR):][::-1] ** 0.3
        # Done by beat 2 of the break, so it never talks over the voice-over line that follows.
        for k, at in enumerate([burst, burst + STEP, burst + 2 * STEP]):
            place(vox, head, at, 0.7 + 0.1 * k)
        place(vox, chop, burst + 3 * STEP, 1.0)
    for k in range(4):
        place(drums, snare(), burst + 4 * STEP + k * STEP, 0.25 + 0.1 * k)

    # ---- beat switch: half-time, the riff slowed down a fourth and drenched in reverb.
    switch = np.zeros(N)
    for t, n in riff_notes(themes, triad - 0.1, slowed=5):
        # Half speed: every riff note lands twice as late.
        tt = themes + (t - themes) * 2
        if tt < triad - 0.1:
            place(switch, cowbell(n, 0.5, 0.5), tt, 0.8)
    bells += reverb(lp(switch, 2600), 0.6, 2.6)
    for b in np.arange(themes, triad - 0.01, BAR):
        place(low, kick(), b, 0.95)
        place(low, bass808(33 - 5 + 12, BAR * 0.9, glide_to=33 - 5, glide_at=0.55, drive=3.6), b, 0.9)
        place(drums, clap(), b + 1.0, 0.95)
        place(drums, snare(), b + 1.0, 0.35)
        for s in range(0, 16, 2):
            place(drums, hat(tone=7000), b + s * STEP, 0.26 if s % 4 else 0.34)
        place(drums, hat(), b + 1.75, 0.3)
        place(drums, hat(), b + 1.8125, 0.32)
        place(drums, hat(), b + 1.875, 0.35)

    # ---- triad: three hits, silence between (the words SHAKE. PARK. DROP.).
    hits = [triad, triad + 1.0, triad + 2.0]
    for k, at in enumerate(hits):
        n = [33, 29, 28][k]
        place(low, kick(1.4), at, 1.0)
        place(low, bass808(n, 0.55, drive=4.5), at, 1.0)
        place(drums, clap(), at, 0.9)
        place(drums, crash(), at, 0.35)
        stab = sum(cowbell(m, 0.45, 1.1) for m in ([69, 72, 76], [65, 69, 72], [64, 68, 71])[k])
        place(bells, reverb(stab, 0.35, 0.9), at, 0.75)

    # ---- end card: one long 808 slide, the riff echoing out.
    place(low, kick(1.4), end, 1.0)
    place(low, bass808(33, dur - end, glide_to=21, glide_at=0.45, drive=3.0), end, 0.95)
    place(drums, crash(), end, 0.6)
    tail = np.zeros(N)
    for t, n in riff_notes(end, end + 2.0):
        place(tail, cowbell(n, 0.3), t, 0.7)
    bells += reverb(echo(tail, 0.375, 0.45, 5), 0.5, 2.4)
    if chop is not None:
        place(vox, reverb(resample_poly(chop, 4, 3), 0.7, 2.6) * 0.35, dur - 1.6, 0.5)

    # ---- mix: sidechain the riff and drums to the kick/808, glue, master.
    pump = sidechain(low, depth=0.45)
    mixed = low * 0.85 + (bells * 0.8 + drums * 0.62) * pump + fx * 0.8 + vox * 0.8
    mixed = hp(mixed, 28)
    fade = np.ones(N)
    fe = int((dur - 0.7) * SR)
    fade[fe:] = np.clip(np.linspace(1, 0, N - fe) * 1.2, 0, 1)
    out = saturate(mixed * fade * 0.9, 1.25)
    return out / (np.max(np.abs(out)) + 1e-9) * 0.95


def sidechain(source, depth=0.4):
    """Gain that dips when the kick/808 hits and recovers over ~120 ms."""
    level = lp(np.abs(source), 30)
    level = level / (np.max(level) + 1e-9)
    return 1 - depth * np.clip(level * 1.6, 0, 1)


def load_chop(chops_dir):
    """The word "Pouchy" (Kokoro voice), trimmed. None if it hasn't been made (see make_chop)."""
    path = Path(chops_dir or HERE / 'audio' / 'build' / 'chops') / 'pouchy.wav'
    if not path.exists():
        return None
    import soundfile as sf
    data, sr = sf.read(path)
    if data.ndim > 1:
        data = data.mean(axis=1)
    data = resample_poly(data, SR, sr)
    loud = np.where(np.abs(data) > 0.02)[0]
    data = data[max(0, loud[0] - 200): loud[-1] + 2000]
    return hp(data, 120) / (np.max(np.abs(data)) + 1e-9) * 0.9


def make_chop(model_dir=r'C:\PouchyTrailer\tts', voice='af_bella'):
    """Says "Pouchy!" with the local Kokoro model (Apache-2.0) into audio/build/chops/pouchy.wav."""
    import soundfile as sf
    from kokoro_onnx import Kokoro
    out = HERE / 'audio' / 'build' / 'chops'
    out.mkdir(parents=True, exist_ok=True)
    kokoro = Kokoro(str(Path(model_dir) / 'kokoro-v1.0.onnx'), str(Path(model_dir) / 'voices-v1.0.bin'))
    samples, sr = kokoro.create('Pouchy!', voice=voice, speed=1.15, lang='en-us')
    sf.write(out / 'pouchy.wav', samples, sr)
    return out / 'pouchy.wav'


if __name__ == '__main__':
    import sys
    import soundfile as sf
    if '--chop' in sys.argv or not (HERE / 'audio' / 'build' / 'chops' / 'pouchy.wav').exists():
        try:
            print('chop:', make_chop())
        except Exception as ex:  # Kokoro missing: the track works without the chop.
            print('no chop:', ex)
    tl = json.loads((HERE / 'src' / 'timeline.json').read_text(encoding='utf-8'))
    x = music(tl)
    path = HERE / 'audio' / 'build' / 'music-phonk.wav'
    path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(path, np.stack([x, x], axis=1)[:int(tl['duration'] * SR)].astype(np.float32), SR)
    print(path, f'{len(x) / SR:.1f}s')
