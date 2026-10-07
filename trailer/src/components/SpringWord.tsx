// Words that spring up one after another (kinetic type for lines of copy).
import React from 'react';
import { useVideoConfig } from 'remotion';
import { C, display } from '../theme';
import { clamp, springAt, useTime } from '../lib/time';

export const SpringWord: React.FC<{
  words: string[];
  at: number;
  stagger?: number;
  until?: number;
  size?: number;
  color?: string;
  /** Words to colour with the accent. */
  highlight?: string[];
  accent?: string;
  y?: number;
  weight?: number;
  maxWidth?: number;
}> = ({ words, at, stagger = 0.09, until = Infinity, size = 1, color = C.white, highlight = [], accent = C.violet, y = 0.5, weight = 800, maxWidth = 0.86 }) => {
  const t = useTime();
  const { width, height } = useVideoConfig();
  if (t < at || t > until + 0.25) return null;
  const fontSize = Math.min(width * 0.075, height * 0.13) * size;
  const exit = clamp((t - until) / 0.25);

  return (
    <div
      style={{
        position: 'absolute',
        left: (width * (1 - maxWidth)) / 2,
        width: width * maxWidth,
        top: height * y,
        transform: 'translateY(-50%)',
        display: 'flex',
        flexWrap: 'wrap',
        justifyContent: 'center',
        columnGap: fontSize * 0.28,
        rowGap: fontSize * 0.02,
        fontFamily: display,
        fontWeight: weight,
        fontSize,
        lineHeight: 1,
        letterSpacing: '-0.035em',
      }}
    >
      {words.map((w, i) => {
        const s = springAt(t - at - i * stagger, { stiffness: 210, damping: 15 });
        const e = clamp(exit * 1.6 - i * 0.08);
        return (
          <span
            key={i}
            style={{
              display: 'inline-block',
              color: highlight.includes(w) ? accent : color,
              opacity: clamp(s * 3) * (1 - e),
              transform: `translateY(${(1 - s) * fontSize * 0.9 - e * fontSize * 0.4}px) rotate(${(1 - s) * 7}deg) scale(${0.85 + 0.15 * s})`,
              textShadow: `0 ${fontSize * 0.05}px ${fontSize * 0.2}px rgba(10,6,30,0.5)`,
            }}
          >
            {w}
          </span>
        );
      })}
    </div>
  );
};
