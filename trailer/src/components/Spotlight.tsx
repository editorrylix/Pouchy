// Dims everything except one rectangle (in the space it's placed in), to point at a detail.
import React from 'react';
import { AbsoluteFill } from 'remotion';
import { clamp, easeOutCubic, useTime } from '../lib/time';
import { useScreenScale } from './Footage';

export const Spotlight: React.FC<{
  x: number;
  y: number;
  w: number;
  h: number;
  at: number;
  until?: number;
  radius?: number;
  dim?: number;
}> = ({ x, y, w, h, at, until = Infinity, radius = 14, dim = 0.55 }) => {
  const t = useTime();
  const s = useScreenScale();
  if (t < at || t > until + 0.3) return null;
  const on = easeOutCubic(clamp((t - at) / 0.3)) * (1 - clamp((t - until) / 0.3));
  // The hole opens from a little larger than the target down to it.
  const grow = 1 + (1 - on) * 0.25;
  return (
    <AbsoluteFill style={{ pointerEvents: 'none', overflow: 'hidden' }}>
      <div
        style={{
          position: 'absolute',
          left: x - (w * grow) / 2,
          top: y - (h * grow) / 2,
          width: w * grow,
          height: h * grow,
          borderRadius: radius,
          boxShadow: `0 0 0 4000px rgba(8,5,20,${dim * on}), 0 0 ${40 / s}px ${12 / s}px rgba(139,124,255,${0.5 * on})`,
          outline: `${3 / s}px solid rgba(255,255,255,${0.85 * on})`,
          outlineOffset: 4 / s,
        }}
      />
    </AbsoluteFill>
  );
};
