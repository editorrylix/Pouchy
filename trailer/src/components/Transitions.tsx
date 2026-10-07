// Scene timing and the transitions between scenes. Scenes are placed at absolute times (seconds)
// so they line up with the music and sound effects, which are generated from the same timeline.
import React from 'react';
import { AbsoluteFill, Sequence, useVideoConfig } from 'remotion';
import { clamp, easeInCubic, easeOutExpo, useTime } from '../lib/time';

export type TransitionSpec =
  | { kind: 'cut' }
  | { kind: 'zoom'; dur: number }
  | { kind: 'whip'; dur: number; dir?: 1 | -1 }
  | { kind: 'mask'; dur: number; x?: number; y?: number; shape?: 'circle' | 'pill' };

/** Zoom through: the old scene rushes towards the viewer, the new one settles in from behind. */
const zoomIn = (p: number): React.CSSProperties => ({
  transform: `scale(${0.82 + 0.18 * p})`,
  opacity: p,
  filter: p < 1 ? `blur(${(1 - p) * 10}px)` : undefined,
});
const zoomOut = (p: number): React.CSSProperties => ({
  transform: `scale(${1 + 0.35 * p})`,
  opacity: 1 - p,
  filter: p > 0 ? `blur(${p * 14}px)` : undefined,
});

const whip = (p: number, dir: number, entering: boolean): React.CSSProperties => {
  const x = entering ? (1 - p) * 100 * dir : -p * 100 * dir;
  const speed = entering ? 1 - p : p;
  return { transform: `translateX(${x}%) skewX(${-dir * speed * 8}deg)`, filter: speed > 0.02 ? `blur(${speed * 18}px)` : undefined };
};

/** Mask reveal: the new scene grows out of a point (a circle, or a pill like Pouchy's buttons). */
const mask = (p: number, x = 50, y = 50, shape: 'circle' | 'pill' = 'circle'): React.CSSProperties => {
  if (p >= 1) return {};
  if (shape === 'pill') {
    const ix = (1 - p) * 50;
    const iy = (1 - p) * 50;
    return { clipPath: `inset(${iy}% ${ix}% ${iy}% ${ix}% round ${Math.max(2, (1 - p) * 40)}%)` };
  }
  return { clipPath: `circle(${p * 150}% at ${x}% ${y}%)` };
};

const Body: React.FC<{ length: number; enter: TransitionSpec; exit: TransitionSpec; children: React.ReactNode }> = ({ length, enter, exit, children }) => {
  const t = useTime();
  let style: React.CSSProperties = {};
  if (enter.kind !== 'cut') {
    const p = easeOutExpo(clamp(t / enter.dur));
    if (p < 1) {
      if (enter.kind === 'zoom') style = zoomIn(p);
      else if (enter.kind === 'whip') style = whip(p, enter.dir ?? 1, true);
      else if (enter.kind === 'mask') style = mask(easeOutExpo(clamp(t / enter.dur)), enter.x, enter.y, enter.shape);
    }
  }
  if (exit.kind !== 'cut') {
    const p = easeInCubic(clamp((t - (length - exit.dur)) / exit.dur));
    if (p > 0) {
      if (exit.kind === 'zoom') style = { ...style, ...zoomOut(p) };
      else if (exit.kind === 'whip') style = { ...style, ...whip(p, exit.dir ?? 1, false) };
      // A mask exit leaves the old scene untouched: the next scene's mask covers it.
    }
  }
  return <AbsoluteFill style={style}>{children}</AbsoluteFill>;
};

/** A scene from `from` to `to` (seconds in the trailer). */
export const Scene: React.FC<{
  from: number;
  to: number;
  enter?: TransitionSpec;
  exit?: TransitionSpec;
  name?: string;
  children: React.ReactNode;
}> = ({ from, to, enter = { kind: 'cut' }, exit = { kind: 'cut' }, name, children }) => {
  const { fps } = useVideoConfig();
  const start = Math.round(from * fps);
  const end = Math.round(to * fps);
  return (
    <Sequence from={start} durationInFrames={end - start} name={name}>
      <Body length={(end - start) / fps} enter={enter} exit={exit}>
        {children}
      </Body>
    </Sequence>
  );
};

/** Zoom transition as a standalone wrapper (for elements inside a scene). */
export const ZoomTransition: React.FC<{ at: number; dur?: number; out?: boolean; children: React.ReactNode }> = ({ at, dur = 0.35, out = false, children }) => {
  const t = useTime();
  const p = out ? easeInCubic(clamp((t - at) / dur)) : easeOutExpo(clamp((t - at) / dur));
  if (!out && t < at) return null;
  if (out && p >= 1) return null;
  return <AbsoluteFill style={out ? zoomOut(p) : zoomIn(p)}>{children}</AbsoluteFill>;
};

/** Mask transition as a standalone wrapper: reveals its children out of a point. */
export const MaskTransition: React.FC<{ at: number; dur?: number; x?: number; y?: number; shape?: 'circle' | 'pill'; children: React.ReactNode }> = ({
  at,
  dur = 0.5,
  x,
  y,
  shape,
  children,
}) => {
  const t = useTime();
  if (t < at) return null;
  return <AbsoluteFill style={mask(easeOutExpo(clamp((t - at) / dur)), x, y, shape)}>{children}</AbsoluteFill>;
};
