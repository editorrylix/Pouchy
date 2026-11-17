# Editing the trailer

The trailer is code: a [Remotion](https://www.remotion.dev) project (React + TypeScript) that
edits real screen recordings of Pouchy, plus Python scripts that make the music, sound effects
and voice from the same timeline. Nothing in the picture imitates the app: product UI is either a
recording of the real app or the app's own views rendered by its tests.

## Layout

```
capture/          recording the footage: record.ps1 drives the real app on a separate virtual
                  desktop (Rec.cs = input/window helpers, backdrop.ps1, sample files, notes.html)
src/timeline.json scene start times, voice cues and sound-effect cues — the single source of timing
src/Trailer.tsx   the scene order and the transitions between scenes
src/scenes/       Opening (hook, shake, drop), Montage (features), Closing (themes, verbs, end card)
src/components/   Footage (retimed recordings + virtual camera), ImpactText, SpringWord, Doodles
                  (DoodleArrow, ScribbleCircle, CursorTrail, ShakeMarks), Spotlight, Caption,
                  LogoReveal, Chips (FileChip, FeatureBurst, FeatureLabel), Layout (WindowFrame,
                  SplitScene, ParallaxLayer, Tag), Transitions (Scene, ZoomTransition, MaskTransition)
audio/            vo.py (Kokoro voice), phonk.py (the soundtrack), mix.py (effects, mixes, music choice)
tools/AppSounds   writes Pouchy's built-in interface sounds to WAV (used in the mix)
scripts/          prepare.py (gathers footage and renders into public/), render.ps1 (final files)
```

## Rebuilding from scratch

1. **Footage** — `powershell -File capture\record.ps1` (all shots) or `-Shots 02-summon,03-drop-anywhere`.
   It publishes nothing and touches none of your data: it uses a `--profile trailer` copy of Pouchy
   from `C:\PouchyTrailer\app`, records on its own virtual desktop, refuses to record if any other
   window is visible, and puts the taskbar, Run key and Explorer verbs back afterwards.
   Clips land in `C:\PouchyTrailer\footage`.
2. **App renders** (theme cards):
   `$env:POUCHY_RENDER_DIR='C:\PouchyTrailer\renders'; $env:POUCHY_RENDER_MARKETING='1'; dotnet test tests\Pouchy.Tests --filter ViewSmokeTests`
3. **App sounds** — `dotnet run --project trailer\tools\AppSounds -- trailer\audio\build\app-sounds`
4. **Python** (3.12; numpy, scipy, soundfile, pillow, kokoro-onnx) and the Kokoro model files
   (`kokoro-v1.0.onnx`, `voices-v1.0.bin` from the kokoro-onnx releases) in `C:\PouchyTrailer\tts`.
5. `npm install`, then `powershell -File scripts\render.ps1` → `out/trailer-master-1080p60.mp4` and
   `out/trailer-master-1080p30.mp4`. `-Vertical` adds the 9:16 cut, `-Variants` the no-voice, musicless
   and effects-only mixes, and `-Readme` writes `trailer.mp4`, `trailer-vertical.mp4` and `preview.gif`
   into `docs/media`. On a PC short of memory (a game running), add `-Concurrency 2`.

Preview while editing: `npm run studio` (set the `audio` prop to `full` to hear the mix).

## Music

The soundtrack is `audio/phonk.py`: an original phonk / Brazilian-funk style track (pitched 808
cowbell riff, distorted sliding 808, funk kick pattern, a "Pou-pou-Pouchy" vocal chop and a slowed
beat switch), synthesised from scratch and timed to `timeline.json`, so the cuts land on the beat.
`python audio/phonk.py` writes it alone to `audio/build/music-phonk.wav` for listening.

- **Use another track:** `render.ps1 -Music path\to\song.wav` (or `audio\mix.py --music ... --music-offset 12.5`
  to start the song at its drop). Only use music you have the rights to: a popular song in a video on
  GitHub, YouTube or the web is a copyright claim waiting to happen.
- **Trending sounds on Instagram, TikTok or Shorts:** post `trailer-vertical-sfx-only.mp4` (from
  `-Vertical -Variants`) and pick the sound in the app. Sounds from the app's own library are licensed
  for use there; the same song baked into the file is not, and gets the video muted or blocked.
- The original synth-pop track is still there: `-Music classic`.

## Common edits

- **Move a scene or a sound**: change its time in `src/timeline.json`, then re-run `audio/mix.py`
  (picture and sound both read it). Keep scene starts on the beat grid (multiples of 0.5 s).
- **Retime footage**: each `<Footage map>` is a list of `[trailer seconds, recording seconds]`;
  footage plays linearly between points, so two points close together make a speed ramp and two
  points 0.0001 s apart make a hard cut.
- **Reframe**: `cam` keyframes name the part of the screen to show (`rect(x1, y1, x2, y2)` in screen
  pixels of the 1920×1080 recording). The camera fits that area into any aspect ratio and never
  shows the taskbar strip, which is how the `Vertical`, `Square` and `Portrait` compositions work
  without a separate edit (they're set up; check the framing before publishing them).
- **Change a line of voice-over**: edit it in `timeline.json`, run `audio/vo.py` (it updates
  `src/vo-timings.json` for the captions) and `audio/mix.py`.

## Licences

- Footage and app renders: Pouchy itself (this repository).
- Music and sound effects: synthesised by `audio/phonk.py` and `audio/mix.py` — original, no samples.
  The interface sounds are Pouchy's own (its `SoundSynth`); the vocal chop is the Kokoro voice below.
- Voice: Kokoro-82M (Apache-2.0) run locally with kokoro-onnx (MIT).
- Fonts: Bricolage Grotesque and Inter (SIL Open Font License), loaded with @remotion/google-fonts.
- Remotion is free for individuals and companies of up to three people; larger teams need a
  company licence (remotion.dev/license).
