import { useCurrentFrame, useVideoConfig } from 'remotion';
import timeline from '../timeline.json';

export const BPM = timeline.bpm;
export const BEAT = 60 / BPM;

/** Seconds into the current Sequence (works the same at 30 and 60 fps). */
export const useTime = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  return frame / fps;
};

/** Seconds → frames for the current composition. */
export const useFrames = () => {
  const { fps } = useVideoConfig();
  return (seconds: number) => Math.round(seconds * fps);
};

export const clamp = (v: number, lo = 0, hi = 1) => Math.min(hi, Math.max(lo, v));

/** 0 → 1 over [start, start + dur], eased. */
export const progress = (t: number, start: number, dur: number, ease: (x: number) => number = easeOutCubic) =>
  ease(clamp((t - start) / dur));

export const easeOutCubic = (x: number) => 1 - Math.pow(1 - x, 3);
export const easeInCubic = (x: number) => x * x * x;
export const easeInOutCubic = (x: number) => (x < 0.5 ? 4 * x * x * x : 1 - Math.pow(-2 * x + 2, 3) / 2);
export const easeOutExpo = (x: number) => (x >= 1 ? 1 : 1 - Math.pow(2, -10 * x));
export const easeInOutQuint = (x: number) => (x < 0.5 ? 16 * x ** 5 : 1 - Math.pow(-2 * x + 2, 5) / 2);
export const easeOutBack = (x: number, s = 1.70158) => 1 + (s + 1) * Math.pow(x - 1, 3) + s * Math.pow(x - 1, 2);

/** A damped spring in seconds, 0 → 1 with a little overshoot (deterministic, fps-independent). */
export const springAt = (t: number, { stiffness = 170, damping = 14, mass = 1 } = {}) => {
  if (t <= 0) return 0;
  const w0 = Math.sqrt(stiffness / mass);
  const zeta = damping / (2 * Math.sqrt(stiffness * mass));
  if (zeta < 1) {
    const wd = w0 * Math.sqrt(1 - zeta * zeta);
    return 1 - Math.exp(-zeta * w0 * t) * (Math.cos(wd * t) + ((zeta * w0) / wd) * Math.sin(wd * t));
  }
  return 1 - Math.exp(-w0 * t) * (1 + w0 * t);
};

/** Piecewise-linear lookup through [x, y] pairs (x ascending), clamped at both ends. */
export const piecewise = (x: number, points: readonly (readonly [number, number])[]) => {
  if (x <= points[0][0]) return points[0][1];
  for (let i = 1; i < points.length; i++) {
    const [x1, y1] = points[i];
    const [x0, y0] = points[i - 1];
    if (x <= x1) return y0 + ((x - x0) / (x1 - x0)) * (y1 - y0);
  }
  return points[points.length - 1][1];
};

/** Deterministic pseudo-random in [0, 1) from a seed. */
export const rand = (seed: number) => {
  const s = Math.sin(seed * 12.9898 + 78.233) * 43758.5453;
  return s - Math.floor(s);
};
