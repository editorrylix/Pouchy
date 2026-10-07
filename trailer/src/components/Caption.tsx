// Captions for the voice-over: one line at a time, words lighting up as they're spoken.
import React from 'react';
import { useVideoConfig } from 'remotion';
import { C, text } from '../theme';
import { clamp, easeOutCubic, useTime } from '../lib/time';

export const Caption: React.FC<{ text: string; at: number; dur: number }> = ({ text: line, at, dur }) => {
  const t = useTime();
  const { width, height } = useVideoConfig();
  if (t < at - 0.05 || t > at + dur + 0.35) return null;
  const words = line.split(' ');
  const R = Math.min(width, height);
  const fs = R * 0.036;
  const inP = easeOutCubic(clamp((t - at + 0.05) / 0.2));
  const outP = clamp((t - at - dur) / 0.35);
  // Words light up evenly across the line's duration, weighted by length.
  const total = words.reduce((n, w) => n + w.length + 2, 0);
  let acc = 0;
  return (
    <div
      style={{
        position: 'absolute',
        left: 0,
        right: 0,
        bottom: height * (width >= height ? 0.075 : 0.12),
        display: 'flex',
        justifyContent: 'center',
        opacity: inP * (1 - outP),
        transform: `translateY(${(1 - inP) * 20}px)`,
      }}
    >
      <div
        style={{
          maxWidth: width * 0.84,
          padding: `${fs * 0.42}px ${fs * 0.8}px`,
          borderRadius: fs * 0.6,
          background: 'rgba(10,7,24,0.72)',
          border: '1px solid rgba(255,255,255,0.12)',
          fontFamily: text,
          fontWeight: 700,
          fontSize: fs,
          lineHeight: 1.25,
          textAlign: 'center',
          letterSpacing: '-0.005em',
        }}
      >
        {words.map((w, i) => {
          const start = at + (acc / total) * dur;
          acc += w.length + 2;
          const lit = t >= start;
          return (
            <span key={i} style={{ color: lit ? C.white : 'rgba(255,255,255,0.38)', transition: 'none' }}>
              {w}
              {i < words.length - 1 ? ' ' : ''}
            </span>
          );
        })}
      </div>
    </div>
  );
};
