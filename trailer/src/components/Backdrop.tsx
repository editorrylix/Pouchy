// The trailer's own stage: deep ink with slow violet/pink glows and a little grain.
import React from 'react';
import { AbsoluteFill } from 'remotion';
import { C } from '../theme';
import { useTime } from '../lib/time';

export const Backdrop: React.FC<{ tint?: string; glow?: number }> = ({ tint = C.violet, glow = 1 }) => {
  const t = useTime();
  const a = Math.sin(t * 0.6) * 6;
  const b = Math.cos(t * 0.45) * 7;
  return (
    <AbsoluteFill style={{ background: `radial-gradient(120% 90% at 50% 40%, ${C.ink2} 0%, ${C.ink} 70%)` }}>
      <AbsoluteFill
        style={{
          opacity: 0.55 * glow,
          background: `radial-gradient(45% 55% at ${28 + a}% ${30 + b}%, ${tint}55 0%, transparent 70%),
                       radial-gradient(40% 50% at ${74 - b}% ${72 + a}%, ${C.pink}40 0%, transparent 70%)`,
        }}
      />
      <Grain />
    </AbsoluteFill>
  );
};

/** Film grain (static SVG noise, shifted every frame so it shimmers). */
export const Grain: React.FC<{ opacity?: number }> = ({ opacity = 0.07 }) => {
  const t = useTime();
  const shift = Math.floor(t * 24) % 8;
  return (
    <AbsoluteFill
      style={{
        opacity,
        mixBlendMode: 'overlay',
        backgroundImage: `url("data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='220' height='220'><filter id='n'><feTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='2' stitchTiles='stitch'/></filter><rect width='100%' height='100%' filter='url(%23n)'/></svg>")`,
        backgroundPosition: `${shift * 37}px ${shift * 53}px`,
        pointerEvents: 'none',
      }}
    />
  );
};

export const Vignette: React.FC<{ strength?: number }> = ({ strength = 0.55 }) => (
  <AbsoluteFill style={{ background: `radial-gradient(ellipse at center, transparent 55%, rgba(5,3,14,${strength}) 100%)`, pointerEvents: 'none' }} />
);

/** A white (or tinted) flash that fades quickly: punctuates impacts. */
export const Flash: React.FC<{ at: number; dur?: number; color?: string; peak?: number }> = ({ at, dur = 0.22, color = '#fff', peak = 0.55 }) => {
  const t = useTime();
  const p = (t - at) / dur;
  if (p < 0 || p > 1) return null;
  return <AbsoluteFill style={{ backgroundColor: color, opacity: peak * (1 - p) * (1 - p), pointerEvents: 'none' }} />;
};
