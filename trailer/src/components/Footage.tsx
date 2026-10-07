// Real Pouchy screen recordings (trailer/capture), retimed and framed by a virtual camera.
// Nothing here draws product UI: the pixels are the recording. Overlays (children) are placed in
// screen coordinates, so a doodle around a button stays on that button while the camera moves.
import React, { createContext, useContext } from 'react';
import { AbsoluteFill, Freeze, OffthreadVideo, staticFile, useVideoConfig } from 'remotion';
import { CamKey, focusAt, frameFocus, SCREEN } from '../lib/camera';
import { piecewise, useTime } from '../lib/time';

/** Scale of the screen layer, so overlays can keep strokes a constant width on screen. */
export const ScreenScale = createContext(1);
export const useScreenScale = () => useContext(ScreenScale);

export type TimeMap = readonly (readonly [number, number])[];

export const Footage: React.FC<{
  clip: string;
  /** [sequence seconds, recording seconds] pairs; linear between them (a speed ramp per segment). */
  map: TimeMap;
  cam: CamKey[];
  width?: number;
  height?: number;
  /** Extra look: blur in px and brightness, for footage used behind type. */
  blur?: number;
  brightness?: number;
  children?: React.ReactNode;
}> = ({ clip, map, cam, width, height, blur = 0, brightness = 1, children }) => {
  const t = useTime();
  const { fps, width: vw, height: vh } = useVideoConfig();
  const W = width ?? vw;
  const H = height ?? vh;
  const source = piecewise(t, map);
  const { scale, tx, ty } = frameFocus(focusAt(cam, t), W, H);

  return (
    <AbsoluteFill style={{ overflow: 'hidden', width: W, height: H, backgroundColor: '#100c1e' }}>
      <div
        style={{
          position: 'absolute',
          width: SCREEN.w,
          height: SCREEN.h,
          transformOrigin: '0 0',
          transform: `translate(${tx}px, ${ty}px) scale(${scale})`,
          overflow: 'hidden',
          filter: blur || brightness !== 1 ? `blur(${blur}px) brightness(${brightness})` : undefined,
        }}
      >
        <Freeze frame={Math.max(0, Math.round(source * fps))}>
          {/* 1920×1080 recording; the layer is 1010 tall, which crops the taskbar off the bottom. */}
          <OffthreadVideo src={staticFile(`footage/${clip}.mp4`)} muted style={{ width: 1920, height: 1080, display: 'block' }} />
        </Freeze>
        <ScreenScale.Provider value={scale}>
          <AbsoluteFill>{children}</AbsoluteFill>
        </ScreenScale.Provider>
      </div>
    </AbsoluteFill>
  );
};
