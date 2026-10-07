// Layout pieces: a frame for footage panes, split screens that adapt to the aspect ratio, and
// parallax layers.
import React from 'react';
import { AbsoluteFill, useVideoConfig } from 'remotion';
import { C, text } from '../theme';
import { clamp, easeOutBack, easeOutCubic, springAt, useTime } from '../lib/time';

/**
 * A plain rounded card that holds a piece of footage. It deliberately has no title bar or window
 * buttons: it frames the recording, it doesn't imitate any app.
 */
export const WindowFrame: React.FC<{
  x: number;
  y: number;
  w: number;
  h: number;
  at?: number;
  label?: string;
  radius?: number;
  children: (size: { width: number; height: number }) => React.ReactNode;
}> = ({ x, y, w, h, at = 0, label, radius = 26, children }) => {
  const t = useTime();
  const s = springAt(t - at, { stiffness: 160, damping: 17 });
  return (
    <div
      style={{
        position: 'absolute',
        left: x,
        top: y,
        width: w,
        height: h,
        transform: `translateY(${(1 - s) * 80}px) scale(${0.92 + 0.08 * s})`,
        opacity: clamp(s * 2),
      }}
    >
      <div
        style={{
          position: 'absolute',
          inset: 0,
          borderRadius: radius,
          overflow: 'hidden',
          boxShadow: '0 30px 80px rgba(5,2,20,0.55), 0 0 0 1.5px rgba(255,255,255,0.14)',
        }}
      >
        {children({ width: w, height: h })}
      </div>
      {label && <Tag label={label} at={at + 0.15} style={{ position: 'absolute', left: 22, top: -22 }} />}
    </div>
  );
};

export const Tag: React.FC<{ label: string; at: number; color?: string; style?: React.CSSProperties; size?: number }> = ({ label, at, color = C.violet, style, size = 30 }) => {
  const t = useTime();
  const p = easeOutBack(clamp((t - at) / 0.35));
  return (
    <div
      style={{
        padding: `${size * 0.32}px ${size * 0.7}px`,
        borderRadius: 999,
        background: color,
        color: C.white,
        fontFamily: text,
        fontWeight: 700,
        fontSize: size,
        letterSpacing: '0.01em',
        boxShadow: '0 10px 30px rgba(5,2,20,0.4)',
        transform: `scale(${p})`,
        transformOrigin: 'left center',
        whiteSpace: 'nowrap',
        ...style,
      }}
    >
      {label}
    </div>
  );
};

/** Two panes: side by side in landscape, stacked in portrait. */
export const SplitScene: React.FC<{
  at?: number;
  labels?: [string, string];
  gap?: number;
  children: [(size: { width: number; height: number }) => React.ReactNode, (size: { width: number; height: number }) => React.ReactNode];
}> = ({ at = 0, labels, gap = 40, children }) => {
  const { width, height } = useVideoConfig();
  const landscape = width >= height;
  const margin = Math.min(width, height) * 0.07;
  const top = landscape ? height * 0.2 : margin * 1.6;
  const paneW = landscape ? (width - margin * 2 - gap) / 2 : width - margin * 2;
  const paneH = landscape ? Math.min(paneW * 0.8, height - top - margin) : (height - top - margin - gap) / 2;
  const panes = [0, 1].map((i) => ({
    x: landscape ? margin + i * (paneW + gap) : margin,
    y: landscape ? top : top + i * (paneH + gap),
  }));
  return (
    <AbsoluteFill>
      {panes.map((p, i) => (
        <WindowFrame key={i} x={p.x} y={p.y} w={paneW} h={paneH} at={at + i * 0.12} label={labels?.[i]}>
          {children[i]}
        </WindowFrame>
      ))}
    </AbsoluteFill>
  );
};

/** Moves its content with a slow drift scaled by depth (0 = far, 1 = near). */
export const ParallaxLayer: React.FC<{ depth: number; drift?: [number, number]; at?: number; children: React.ReactNode }> = ({
  depth,
  drift = [-60, -20],
  at = 0,
  children,
}) => {
  const t = useTime();
  const p = easeOutCubic(clamp((t - at) / 3));
  return (
    <AbsoluteFill style={{ transform: `translate(${drift[0] * depth * p}px, ${drift[1] * depth * p}px) scale(${1 + depth * 0.04 * p})` }}>
      {children}
    </AbsoluteFill>
  );
};
