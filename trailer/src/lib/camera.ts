// A virtual camera over the real screen recordings.
//
// Each keyframe names the part of the screen that matters (a focus rectangle in screen pixels).
// The camera frames that rectangle in whatever aspect the trailer is rendered at, so the same edit
// works for 16:9, 9:16, 1:1 and 4:5 without re-cutting.
import { clamp, easeInOutQuint } from './time';

export type Focus = { x: number; y: number; w: number; h: number };
export type CamKey = Focus & { t: number; ease?: (x: number) => number };

/** The usable recording: 1920 wide, the bottom strip (Windows taskbar) is never shown. */
export const SCREEN = { w: 1920, h: 1010 };

export const focusAt = (keys: CamKey[], t: number): Focus => {
  if (t <= keys[0].t) return keys[0];
  for (let i = 1; i < keys.length; i++) {
    const a = keys[i - 1];
    const b = keys[i];
    if (t <= b.t) {
      const p = (b.ease ?? easeInOutQuint)(clamp((t - a.t) / (b.t - a.t)));
      return {
        x: a.x + (b.x - a.x) * p,
        y: a.y + (b.y - a.y) * p,
        w: a.w + (b.w - a.w) * p,
        h: a.h + (b.h - a.h) * p,
      };
    }
  }
  return keys[keys.length - 1];
};

/**
 * The transform that shows `focus` centred in a W×H frame, as large as fits, without ever showing
 * anything outside the recording (it slides or zooms in instead).
 */
export const frameFocus = (focus: Focus, W: number, H: number, pad = 1.08) => {
  let scale = Math.min(W / (focus.w * pad), H / (focus.h * pad));
  // Never smaller than the frame: the recording must always cover it.
  scale = Math.max(scale, W / SCREEN.w, H / SCREEN.h);
  const viewW = W / scale;
  const viewH = H / scale;
  const cx = clamp(focus.x, viewW / 2, SCREEN.w - viewW / 2);
  const cy = clamp(focus.y, viewH / 2, SCREEN.h - viewH / 2);
  return { scale, tx: W / 2 - cx * scale, ty: H / 2 - cy * scale };
};

/** Focus rectangle from corners, which is how positions are read off the footage. */
export const rect = (x1: number, y1: number, x2: number, y2: number): Focus => ({
  x: (x1 + x2) / 2,
  y: (y1 + y2) / 2,
  w: x2 - x1,
  h: y2 - y1,
});

export const WIDE: Focus = rect(0, 0, 1920, 1010);
