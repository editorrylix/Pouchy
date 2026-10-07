// Act one: the problem, the shake, the drop.
import React from 'react';
import { AbsoluteFill } from 'remotion';
import { Flash } from '../components/Backdrop';
import { DoodleArrow, ShakeMarks } from '../components/Doodles';
import { Footage } from '../components/Footage';
import { ImpactText } from '../components/ImpactText';
import { SpringWord } from '../components/SpringWord';
import { rect } from '../lib/camera';
import { clamp, useTime } from '../lib/time';
import { C } from '../theme';

/** 0–3 s. Dragging a picture across a cluttered desktop: the window you need is buried. */
export const Hook: React.FC = () => {
  const t = useTime();
  const dim = clamp((t - 1.42) / 0.12);
  return (
    <AbsoluteFill>
      <Footage
        clip="01-friction"
        map={[
          [0, 3.0],
          [1.5, 5.9],
          [3.2, 6.45],
        ]}
        cam={[
          { t: 0, ...rect(60, 200, 860, 640) },
          { t: 1.4, ...rect(700, 130, 1760, 700) },
          { t: 3.2, ...rect(760, 160, 1700, 680) },
        ]}
        brightness={1 - dim * 0.68}
        blur={dim * 7}
      />
      <SpringWord words={['Moving', 'files', 'between', 'windows?']} at={0.1} until={1.3} y={0.14} size={0.7} highlight={['windows?']} accent={C.peach} />
      <ImpactText text="WINDOWS SHOULD" at={1.5} y={0.33} size={0.62} />
      <ImpactText text="ALREADY HAVE" at={2.0} y={0.5} size={0.62} tilt={2} />
      <ImpactText text="THIS." at={2.5} y={0.69} size={0.75} color={C.pink} accent={C.peach} />
      <Flash at={1.5} peak={0.25} />
      <Flash at={2.5} peak={0.35} color={C.pink} />
    </AbsoluteFill>
  );
};

/** 3–10 s. The magic moment: grab, shake, Pouchy appears, park the file. */
export const Magic: React.FC = () => (
  <AbsoluteFill>
    <Footage
      clip="02-summon"
      map={[
        [0, 2.85],
        [1.2, 4.05],
        [2.6, 6.75],
        [3.0, 7.15],
        [5.75, 9.9],
        [6.2, 10.35],
        [7.4, 11.55],
      ]}
      cam={[
        { t: 0, ...rect(520, 300, 1180, 700) },
        { t: 1.3, ...rect(620, 260, 1180, 690) },
        { t: 2.55, ...rect(630, 260, 1190, 690) },
        { t: 3.3, ...rect(660, 250, 1520, 760) },
        { t: 4.6, ...rect(900, 300, 1500, 720) },
        { t: 6.1, ...rect(935, 315, 1490, 705) },
        { t: 7.4, ...rect(950, 320, 1480, 700) },
      ]}
    >
      <ShakeMarks x={900} y={472} at={1.45} until={2.55} size={150} />
    </Footage>
    <ImpactText text="SHAKE" at={1.5} until={2.5} y={0.16} size={0.62} accent={C.pink} />
    <Flash at={2.62} peak={0.3} color={C.violet} />
    <ImpactText text="PARK." at={6.0} until={6.95} y={0.15} size={0.62} />
  </AbsoluteFill>
);

/** 10–14 s. Drag the picture out of the pouch, straight into Paint. */
export const DropAnywhere: React.FC = () => {
  const t = useTime();
  const dim = clamp((t - 2.5) / 0.15);
  return (
    <AbsoluteFill>
      <Footage
        clip="03-drop-anywhere"
        map={[
          [0, 5.5],
          [1.0, 6.4],
          [2.4, 7.85],
          [2.6, 8.05],
          [4.1, 9.5],
        ]}
        cam={[
          { t: 0, ...rect(630, 280, 1180, 660) },
          { t: 0.9, ...rect(620, 260, 1200, 680) },
          { t: 2.1, ...rect(600, 120, 1900, 900) },
          { t: 2.7, ...rect(1020, 125, 1900, 890) },
          { t: 4.1, ...rect(1060, 150, 1880, 880) },
        ]}
        brightness={1 - dim * 0.35}
      >
        <DoodleArrow from={[760, 520]} to={[1420, 560]} at={0.55} dur={0.45} until={2.35} bend={-0.22} color={C.peach} width={8} />
      </Footage>
      <ImpactText text="ANYWHERE." at={2.5} until={3.9} y={0.5} size={0.82} color={C.white} accent={C.pink} />
      <Flash at={2.5} peak={0.3} />
    </AbsoluteFill>
  );
};
