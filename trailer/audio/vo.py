"""Voice-over lines with Kokoro (local TTS, Apache-2.0 model), one WAV per line.

Writes audio/build/vo/<id>.wav and src/vo-timings.json (spoken length of each line, for captions).
Usage: python audio/vo.py [--model DIR] [--voice af_heart]
"""
import argparse
import json
from pathlib import Path

import numpy as np
import soundfile as sf
from kokoro_onnx import Kokoro

HERE = Path(__file__).resolve().parent.parent
ap = argparse.ArgumentParser()
ap.add_argument('--model', default=r'C:\PouchyTrailer\tts')
ap.add_argument('--voice', default='af_heart')
args = ap.parse_args()

tl = json.loads((HERE / 'src' / 'timeline.json').read_text(encoding='utf-8'))
kokoro = Kokoro(str(Path(args.model) / 'kokoro-v1.0.onnx'), str(Path(args.model) / 'voices-v1.0.bin'))
out = HERE / 'audio' / 'build' / 'vo'
out.mkdir(parents=True, exist_ok=True)
timings = {}
for line in tl['vo']:
    samples, sr = kokoro.create(line['text'], voice=args.voice, speed=1.05, lang='en-us')
    # Trim leading/trailing silence so the cue time is when the voice starts.
    loud = np.where(np.abs(samples) > 0.01)[0]
    samples = samples[max(0, loud[0] - int(0.02 * sr)): loud[-1] + int(0.08 * sr)]
    sf.write(out / f"{line['id']}.wav", samples, sr)
    timings[line['id']] = round(len(samples) / sr, 3)
    print(line['id'], timings[line['id']], line['text'])
(HERE / 'src' / 'vo-timings.json').write_text(json.dumps(timings, indent=2) + '\n', encoding='utf-8')
