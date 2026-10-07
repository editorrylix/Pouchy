// Hand-drawn marks: an arrow, a scribbled circle and a motion trail. Coordinates are in the
// space they're placed in (screen pixels inside <Footage>, frame pixels outside it); strokes keep
// the same width on screen whatever the camera zoom.
import React from 'react';
import { AbsoluteFill } from 'remotion';
import { evolvePath } from '@remotion/paths';
import { C } from '../theme';
import { clamp, easeInOutCubic, easeOutCubic, rand, useTime } from '../lib/time';
import { useScreenScale } from './Footage';

const Svg: React.FC<{ children: React.ReactNode; opacity?: number }> = ({ children, opacity = 1 }) => (
  <AbsoluteFill style={{ pointerEvents: 'none', opacity }}>
    <svg style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', overflow: 'visible' }}>{children}</svg>
  </AbsoluteFill>
);

const fadeOut = (t: number, until: number) => 1 - clamp((t - until) / 0.25);

export const DoodleArrow: React.FC<{
  from: [number, number];
  to: [number, number];
  at: number;
  dur?: number;
  until?: number;
  bend?: number;
  color?: string;
  width?: number;
  seed?: number;
}> = ({ from, to, at, dur = 0.45, until = Infinity, bend = 0.25, color = C.peach, width = 7, seed = 1 }) => {
  const t = useTime();
  const s = useScreenScale();
  if (t < at || t > until + 0.25) return null;
  const [x1, y1] = from;
  const [x2, y2] = to;
  const dx = x2 - x1;
  const dy = y2 - y1;
  const len = Math.hypot(dx, dy);
  // Control point off to one side, with a little hand wobble.
  const cx = (x1 + x2) / 2 - dy * bend + (rand(seed) - 0.5) * len * 0.06;
  const cy = (y1 + y2) / 2 + dx * bend + (rand(seed + 1) - 0.5) * len * 0.06;
  const d = `M ${x1} ${y1} Q ${cx} ${cy} ${x2} ${y2}`;
  const p = easeInOutCubic(clamp((t - at) / dur));
  const head = easeOutCubic(clamp((t - at - dur * 0.85) / 0.15));
  // Arrowhead along the curve's end tangent.
  const ang = Math.atan2(y2 - cy, x2 - cx);
  const hl = (len * 0.09 + 22) * head;
  const hx = (a: number) => x2 - Math.cos(ang + a) * hl;
  const hy = (a: number) => y2 - Math.sin(ang + a) * hl;
  const sw = width / s;
  const ev = evolvePath(p, d);
  return (
    <Svg opacity={fadeOut(t, until)}>
      <path d={d} fill="none" stroke={color} strokeWidth={sw} strokeLinecap="round" strokeDasharray={ev.strokeDasharray} strokeDashoffset={ev.strokeDashoffset} />
      {head > 0 && (
        <path d={`M ${hx(0.5)} ${hy(0.5)} L ${x2} ${y2} L ${hx(-0.5)} ${hy(-0.5)}`} fill="none" stroke={color} strokeWidth={sw} strokeLinecap="round" strokeLinejoin="round" />
      )}
    </Svg>
  );
};

export const ScribbleCircle: React.FC<{
  x: number;
  y: number;
  w: number;
  h: number;
  at: number;
  dur?: number;
  until?: number;
  loops?: number;
  color?: string;
  width?: number;
  seed?: number;
}> = ({ x, y, w, h, at, dur = 0.5, until = Infinity, loops = 1.25, color = C.peach, width = 7, seed = 3 }) => {
  const t = useTime();
  const s = useScreenScale();
  if (t < at || t > until + 0.25) return null;
  const steps = 64;
  const pts: string[] = [];
  for (let i = 0; i <= steps * loops; i++) {
    const a = (i / steps) * Math.PI * 2 - 2.2;
    const wob = 1 + (rand(seed + Math.floor(i / 8)) - 0.5) * 0.08 + (i / steps) * 0.06;
    pts.push(`${(x + Math.cos(a) * (w / 2) * wob).toFixed(1)} ${(y + Math.sin(a) * (h / 2) * wob).toFixed(1)}`);
  }
  const d = `M ${pts.join(' L ')}`;
  const ev = evolvePath(easeInOutCubic(clamp((t - at) / dur)), d);
  return (
    <Svg opacity={fadeOut(t, until)}>
      <path d={d} fill="none" stroke={color} strokeWidth={width / s} strokeLinecap="round" strokeLinejoin="round" strokeDasharray={ev.strokeDasharray} strokeDashoffset={ev.strokeDashoffset} />
    </Svg>
  );
};

/**
 * A motion trail through timed points ([x, y, seconds]): the stroke follows the newest points and
 * fades along its tail. Used for the shake, drawn where the recording's cursor shakes.
 */
export const CursorTrail: React.FC<{
  points: [number, number, number][];
  tail?: number;
  color?: string;
  width?: number;
}> = ({ points, tail = 0.35, color = C.pink, width = 9 }) => {
  const t = useTime();
  const s = useScreenScale();
  const visible = points.filter(([, , pt]) => pt <= t && pt >= t - tail);
  if (visible.length < 2) return null;
  return (
    <Svg>
      {visible.slice(1).map(([x, y, pt], i) => {
        const [px, py] = visible[i];
        const age = (t - pt) / tail;
        return (
          <line key={i} x1={px} y1={py} x2={x} y2={y} stroke={color} strokeWidth={(width * (1 - age * 0.7)) / s} strokeLinecap="round" opacity={1 - age} />
        );
      })}
    </Svg>
  );
};

/** Comic-style shake marks: short arcs either side of a point, pulsing while it shakes. */
export const ShakeMarks: React.FC<{ x: number; y: number; at: number; until: number; size?: number; color?: string }> = ({
  x,
  y,
  at,
  until,
  size = 120,
  color = C.white,
}) => {
  const t = useTime();
  const s = useScreenScale();
  if (t < at || t > until + 0.2) return null;
  const on = clamp((t - at) / 0.12) * (1 - clamp((t - until) / 0.2));
  const pulse = 1 + Math.sin((t - at) * 40) * 0.08;
  const arc = (side: 1 | -1, k: number) => {
    const r = size * (0.55 + k * 0.22) * pulse;
    const a = 0.55;
    const x1 = x + side * Math.cos(a) * r;
    const y1 = y - Math.sin(a) * r;
    const x2 = x + side * Math.cos(a) * r;
    const y2 = y + Math.sin(a) * r;
    return <path key={`${side}${k}`} d={`M ${x1} ${y1} A ${r} ${r} 0 0 ${side === 1 ? 1 : 0} ${x2} ${y2}`} fill="none" stroke={color} strokeWidth={6 / s} strokeLinecap="round" opacity={0.95 - k * 0.3} />;
  };
  return <Svg opacity={on}>{[arc(1, 0), arc(1, 1), arc(-1, 0), arc(-1, 1)]}</Svg>;
};
