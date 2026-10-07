// The end card's logo: Pouchy's real app icon pops in with a ring, then the name springs up.
import React from 'react';
import { Img, staticFile, useVideoConfig } from 'remotion';
import { C, display } from '../theme';
import { clamp, easeOutCubic, springAt, useTime } from '../lib/time';

export const LogoReveal: React.FC<{ at: number; y?: number }> = ({ at, y = 0.36 }) => {
  const t = useTime();
  const { width, height } = useVideoConfig();
  if (t < at) return null;
  const R = Math.min(width, height);
  const icon = R * 0.22;
  const s = springAt(t - at, { stiffness: 150, damping: 10 });
  const ring = easeOutCubic(clamp((t - at - 0.05) / 0.7));
  const name = 'Pouchy';
  const fs = R * 0.15;
  const cx = width / 2;
  const cy = height * y;
  const breathe = 1 + Math.sin((t - at) * 2.4) * 0.012;
  return (
    <>
      {/* glow */}
      <div
        style={{
          position: 'absolute',
          left: cx - icon * 1.6,
          top: cy - icon * 1.6,
          width: icon * 3.2,
          height: icon * 3.2,
          borderRadius: '50%',
          background: `radial-gradient(circle, ${C.violet}66 0%, ${C.pink}22 40%, transparent 70%)`,
          opacity: clamp(s),
        }}
      />
      {/* expanding ring */}
      <div
        style={{
          position: 'absolute',
          left: cx - (icon / 2) * (1 + ring * 1.6),
          top: cy - (icon / 2) * (1 + ring * 1.6),
          width: icon * (1 + ring * 1.6),
          height: icon * (1 + ring * 1.6),
          borderRadius: '50%',
          border: `${R * 0.006}px solid ${C.white}`,
          opacity: (1 - ring) * 0.8,
        }}
      />
      <Img
        src={staticFile('brand/pouchy-1024.png')}
        style={{
          position: 'absolute',
          left: cx - icon / 2,
          top: cy - icon / 2,
          width: icon,
          height: icon,
          transform: `scale(${s * breathe}) rotate(${(1 - s) * -25}deg)`,
          filter: 'drop-shadow(0 20px 40px rgba(5,2,20,0.5))',
        }}
      />
      <div
        style={{
          position: 'absolute',
          left: 0,
          right: 0,
          top: cy + icon * 0.62,
          textAlign: 'center',
          fontFamily: display,
          fontWeight: 800,
          fontSize: fs,
          letterSpacing: '-0.045em',
          lineHeight: 1,
          color: C.white,
        }}
      >
        {name.split('').map((ch, i) => {
          const ls = springAt(t - at - 0.25 - i * 0.05, { stiffness: 220, damping: 13 });
          return (
            <span key={i} style={{ display: 'inline-block', transform: `translateY(${(1 - ls) * fs * 0.7}px)`, opacity: clamp(ls * 2) }}>
              {ch}
            </span>
          );
        })}
      </div>
    </>
  );
};
