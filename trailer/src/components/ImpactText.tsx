// Huge words that land on the beat: they punch in from oversized, leave a short afterimage, and
// shake the frame a little.
import React from 'react';
import { AbsoluteFill, useVideoConfig } from 'remotion';
import { C, display } from '../theme';
import { clamp, easeOutExpo, rand, useTime } from '../lib/time';

export const ImpactText: React.FC<{
  text: string;
  at: number;
  until?: number;
  size?: number;
  color?: string;
  accent?: string;
  /** Vertical position as a fraction of the frame height. */
  y?: number;
  tilt?: number;
  outline?: boolean;
}> = ({ text, at, until = Infinity, size = 1, color = C.white, accent = C.violet, y = 0.5, tilt = -3, outline = false }) => {
  const t = useTime();
  const { width, height } = useVideoConfig();
  if (t < at || t > until + 0.2) return null;

  const local = t - at;
  const p = easeOutExpo(clamp(local / 0.22));
  const scale = 1.9 - 0.9 * p;
  const out = clamp((t - until) / 0.2);
  const shake = local < 0.25 ? (1 - local / 0.25) * 10 : 0;
  const sx = (rand(Math.floor(t * 60)) - 0.5) * shake;
  const sy = (rand(Math.floor(t * 60) + 7) - 0.5) * shake;
  // Type size follows the narrow side, so it fits in vertical and square cuts too.
  const fontSize = Math.min(width * 0.14, height * 0.24) * size;

  const word = (extra: React.CSSProperties) => (
    <div
      style={{
        position: 'absolute',
        left: 0,
        right: 0,
        top: height * y,
        transform: `translate(${sx}px, calc(-50% + ${sy}px)) rotate(${tilt * (1 - p * 0.6)}deg)`,
        textAlign: 'center',
        fontFamily: display,
        fontWeight: 800,
        fontSize,
        lineHeight: 0.92,
        letterSpacing: `${-0.04 + 0.02 * (1 - p)}em`,
        whiteSpace: 'pre-line',
        ...extra,
      }}
    >
      {text}
    </div>
  );

  return (
    <AbsoluteFill style={{ opacity: 1 - out, transform: `scale(${1 - out * 0.15})` }}>
      {/* afterimage */}
      {local < 0.35 &&
        word({
          color: accent,
          opacity: 0.5 * (1 - local / 0.35),
          transform: `translate(${sx}px, calc(-50% + ${sy}px)) scale(${scale * 1.18}) rotate(${tilt}deg)`,
          filter: 'blur(6px)',
        })}
      {word({
        color: outline ? 'transparent' : color,
        WebkitTextStroke: outline ? `${fontSize * 0.025}px ${color}` : undefined,
        transform: `translate(${sx}px, calc(-50% + ${sy}px)) scale(${scale}) rotate(${tilt * (1 - p * 0.6)}deg)`,
        textShadow: `0 ${fontSize * 0.06}px ${fontSize * 0.25}px rgba(10,6,30,0.55)`,
        opacity: clamp(local / 0.05),
      })}
    </AbsoluteFill>
  );
};
