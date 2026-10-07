// Small graphic pieces: file chips (the kinds of things Pouchy holds), a burst of feature names,
// and the feature title used in the montage.
import React from 'react';
import { AbsoluteFill, useVideoConfig } from 'remotion';
import { C, display, text } from '../theme';
import { clamp, easeInCubic, easeOutCubic, rand, springAt, useTime } from '../lib/time';

export type ChipKind = 'image' | 'text' | 'link' | 'color' | 'folder' | 'file';

const Glyph: React.FC<{ kind: ChipKind; size: number; swatch?: string }> = ({ kind, size, swatch = C.peach }) => {
  const s = size;
  const stroke = { fill: 'none', stroke: C.white, strokeWidth: s * 0.09, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const };
  return (
    <svg width={s} height={s} viewBox="0 0 24 24" style={{ flex: 'none' }}>
      {kind === 'image' && (
        <>
          <rect x="3" y="4" width="18" height="16" rx="3" {...stroke} strokeWidth={2} />
          <path d="M5 17 L10 11 L14 15 L16 13 L19 17" {...stroke} strokeWidth={2} />
          <circle cx="16" cy="8.5" r="1.6" fill={C.white} />
        </>
      )}
      {kind === 'text' && <path d="M5 6 H19 M5 10 H19 M5 14 H15 M5 18 H12" {...stroke} strokeWidth={2} />}
      {kind === 'link' && <path d="M10 14 L14 10 M8.5 11.5 L6.5 13.5 A3.2 3.2 0 0 0 11 18 L13 16 M15.5 12.5 L17.5 10.5 A3.2 3.2 0 0 0 13 6 L11 8" {...stroke} strokeWidth={2} />}
      {kind === 'color' && <circle cx="12" cy="12" r="7.5" fill={swatch} stroke={C.white} strokeWidth={1.6} />}
      {kind === 'folder' && <path d="M3 7 A2 2 0 0 1 5 5 H9.5 L11.5 7 H19 A2 2 0 0 1 21 9 V17 A2 2 0 0 1 19 19 H5 A2 2 0 0 1 3 17 Z" {...stroke} strokeWidth={2} />}
      {kind === 'file' && <path d="M7 3 H14 L18 7 V21 H7 Z M14 3 V7 H18" {...stroke} strokeWidth={2} />}
    </svg>
  );
};

export const FileChip: React.FC<{
  kind: ChipKind;
  label: string;
  x: number;
  y: number;
  at: number;
  size?: number;
  color?: string;
  swatch?: string;
  rotate?: number;
}> = ({ kind, label, x, y, at, size = 34, color = 'rgba(255,255,255,0.1)', swatch, rotate = 0 }) => {
  const t = useTime();
  const s = springAt(t - at, { stiffness: 190, damping: 13 });
  if (t < at) return null;
  return (
    <div
      style={{
        position: 'absolute',
        left: x,
        top: y,
        transform: `translate(-50%, -50%) scale(${s}) rotate(${rotate * s}deg)`,
        display: 'flex',
        alignItems: 'center',
        gap: size * 0.4,
        padding: `${size * 0.42}px ${size * 0.7}px ${size * 0.42}px ${size * 0.5}px`,
        borderRadius: size * 0.7,
        background: color,
        backdropFilter: 'blur(12px)',
        border: '1.5px solid rgba(255,255,255,0.18)',
        boxShadow: '0 18px 40px rgba(5,2,20,0.45)',
        color: C.white,
        fontFamily: text,
        fontWeight: 700,
        fontSize: size * 0.82,
        whiteSpace: 'nowrap',
      }}
    >
      <Glyph kind={kind} size={size} swatch={swatch} />
      {label}
    </div>
  );
};

/** Feature names bursting out of the centre, then rushing past the camera. */
export const FeatureBurst: React.FC<{ words: string[]; at: number; until: number; center?: string }> = ({ words, at, until, center }) => {
  const t = useTime();
  const { width, height } = useVideoConfig();
  if (t < at || t > until) return null;
  const R = Math.min(width, height);
  const out = easeInCubic(clamp((t - (until - 0.3)) / 0.3));
  const fs = R * 0.042;
  return (
    <AbsoluteFill style={{ transform: `scale(${1 + out * 1.8})`, opacity: 1 - out }}>
      {words.map((w, i) => {
        // Spread on two rings, alternating, with a little randomness.
        const ring = i % 2 === 0 ? 0.34 : 0.43;
        const a = (i / words.length) * Math.PI * 2 + rand(i) * 0.25 - Math.PI / 2;
        const rx = (width / 2) * ring * (width > height ? 1.55 : 1.05);
        const ry = (height / 2) * ring * (width > height ? 1.35 : 1.25);
        const s = springAt(t - at - i * 0.035, { stiffness: 200, damping: 14 });
        const x = width / 2 + Math.cos(a) * rx * s;
        const y = height / 2 + Math.sin(a) * ry * s;
        const hue = [C.violet, C.pink, C.peach][i % 3];
        return (
          <div
            key={w}
            style={{
              position: 'absolute',
              left: x,
              top: y,
              transform: `translate(-50%, -50%) scale(${0.4 + 0.6 * s}) rotate(${(rand(i + 9) - 0.5) * 10}deg)`,
              opacity: clamp(s * 2),
              padding: `${fs * 0.3}px ${fs * 0.65}px`,
              borderRadius: 999,
              border: `2px solid ${hue}`,
              background: 'rgba(16,10,36,0.75)',
              color: C.white,
              fontFamily: text,
              fontWeight: 700,
              fontSize: fs,
              whiteSpace: 'nowrap',
            }}
          >
            {w}
          </div>
        );
      })}
      {center && (
        <div
          style={{
            position: 'absolute',
            left: 0,
            right: 0,
            top: height / 2,
            transform: `translateY(-50%) scale(${springAt(t - at - 0.1, { stiffness: 180, damping: 12 })})`,
            textAlign: 'center',
            fontFamily: display,
            fontWeight: 800,
            fontSize: R * 0.12,
            letterSpacing: '-0.04em',
            color: C.white,
          }}
        >
          {center}
        </div>
      )}
    </AbsoluteFill>
  );
};

/** The title of a feature in the montage: a number, a name and one line of detail. */
export const FeatureLabel: React.FC<{
  n?: number;
  title: string;
  detail?: string;
  at: number;
  until: number;
  keys?: string;
  /** Corner in landscape (portrait cuts always use the top). */
  pos?: 'bl' | 'br' | 'tl' | 'tr';
}> = ({ n, title, detail, at, until, keys, pos = 'bl' }) => {
  const t = useTime();
  const { width, height } = useVideoConfig();
  if (t < at || t > until) return null;
  const landscape = width >= height;
  const R = Math.min(width, height);
  const inP = easeOutCubic(clamp((t - at) / 0.35));
  const outP = easeInCubic(clamp((t - (until - 0.18)) / 0.18));
  const fs = R * (landscape ? 0.085 : 0.095);
  const rise = (delay: number) => {
    const p = easeOutCubic(clamp((t - at - delay) / 0.35));
    return { transform: `translateY(${(1 - p) * 110 + outP * -40}%)`, opacity: p * (1 - outP) };
  };
  return (
    <div
      style={{
        position: 'absolute',
        left: !landscape ? 0 : pos[1] === 'l' ? R * 0.08 : undefined,
        right: !landscape ? 0 : pos[1] === 'r' ? R * 0.08 : undefined,
        bottom: landscape && pos[0] === 'b' ? R * 0.08 : undefined,
        top: !landscape ? R * 0.1 : pos[0] === 't' ? R * 0.08 : undefined,
        textAlign: !landscape ? 'center' : pos[1] === 'r' ? 'right' : 'left',
        color: C.white,
        textShadow: '0 4px 30px rgba(5,2,20,0.85), 0 2px 6px rgba(5,2,20,0.6)',
      }}
    >
      {n !== undefined && (
        <div style={{ overflow: 'hidden' }}>
          <div style={{ ...rise(0), fontFamily: text, fontWeight: 700, fontSize: fs * 0.3, color: C.violet, letterSpacing: '0.2em' }}>{String(n).padStart(2, '0')}</div>
        </div>
      )}
      <div style={{ overflow: 'hidden', paddingBottom: fs * 0.08 }}>
        <div style={{ ...rise(0.04), fontFamily: display, fontWeight: 800, fontSize: fs, lineHeight: 1, letterSpacing: '-0.04em' }}>{title}</div>
      </div>
      {detail && (
        <div style={{ overflow: 'hidden' }}>
          <div style={{ ...rise(0.1), fontFamily: text, fontWeight: 500, fontSize: fs * 0.34, color: 'rgba(255,255,255,0.86)', marginTop: fs * 0.12 }}>{detail}</div>
        </div>
      )}
      {keys && (
        <div style={{ overflow: 'hidden', marginTop: fs * 0.18 }}>
          <div style={{ ...rise(0.16), display: 'inline-flex', gap: fs * 0.12, justifyContent: 'inherit' }}>
            {keys.split('+').map((k) => (
              <span
                key={k}
                style={{
                  fontFamily: text,
                  fontWeight: 700,
                  fontSize: fs * 0.28,
                  padding: `${fs * 0.06}px ${fs * 0.18}px`,
                  borderRadius: fs * 0.1,
                  background: 'rgba(255,255,255,0.14)',
                  border: '1.5px solid rgba(255,255,255,0.3)',
                  boxShadow: `0 ${fs * 0.04}px 0 rgba(255,255,255,0.18)`,
                  textShadow: 'none',
                }}
              >
                {k.trim()}
              </span>
            ))}
          </div>
        </div>
      )}
      <div style={{ height: R * 0.008, width: R * 0.12 * inP * (1 - outP), background: C.pink, borderRadius: 99, marginTop: fs * 0.2, marginLeft: landscape && pos[1] === 'l' ? 0 : 'auto', marginRight: landscape && pos[1] === 'r' ? 0 : 'auto' }} />
    </div>
  );
};
