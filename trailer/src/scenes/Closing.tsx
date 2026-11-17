// Act three: make it yours, the three verbs, the end card.
import React from 'react';
import { AbsoluteFill, Img, Sequence, staticFile, useVideoConfig } from 'remotion';
import { Backdrop, Flash } from '../components/Backdrop';
import { FeatureLabel, FileChip } from '../components/Chips';
import { Footage, TimeMap } from '../components/Footage';
import { ImpactText } from '../components/ImpactText';
import { ParallaxLayer, Tag } from '../components/Layout';
import { LogoReveal } from '../components/LogoReveal';
import { SpringWord } from '../components/SpringWord';
import { CamKey, rect } from '../lib/camera';
import { clamp, easeInCubic, springAt, useTime } from '../lib/time';
import { C, text } from '../theme';

// Fanned out from the middle card; seven of the nine built-in themes.
const THEMES = ['terminal', 'bubblegum', 'aurora', 'midnight', 'sunset', 'frost', 'acid'];

/** The built-in themes, rendered by Pouchy's own views, fanned out like cards. */
export const Themes: React.FC = () => {
  const t = useTime();
  const { width, height } = useVideoConfig();
  const landscape = width >= height;
  // Tall formats: bigger cards, fanned more by rotation than by spacing.
  const cardH = Math.min(height * 0.58, width * (landscape ? 0.4 : 0.56) * (1240 / 884));
  const cardW = cardH * (884 / 1240);
  const mid = (THEMES.length - 1) / 2;
  const spread = Math.min(0.5, (width * 0.94 - cardW) / ((THEMES.length - 1) * cardW));
  return (
    <AbsoluteFill>
      <Backdrop />
      <ParallaxLayer depth={0.6} drift={[-40, -10]}>
        {THEMES.map((id, i) => {
          const k = i - mid;
          // Middle card first, then outwards in pairs, on the 8th notes.
          const order = Math.abs(k);
          const s = springAt(t - 0.2 - order * 0.25, { stiffness: 170, damping: 13 });
          return (
            <Img
              key={id}
              src={staticFile(`renders/${id}-grid.png`)}
              style={{
                position: 'absolute',
                left: width / 2 - cardW / 2 + k * cardW * spread,
                top: height * 0.58 - cardH / 2 + Math.abs(k) * cardH * 0.05,
                width: cardW,
                height: cardH,
                zIndex: 10 - Math.abs(k),
                transform: `translateY(${(1 - s) * height * 0.6}px) rotate(${k * (landscape ? 5 : 9) * s}deg) scale(${0.9 + 0.1 * s})`,
                filter: 'drop-shadow(0 30px 50px rgba(5,2,20,0.6))',
              }}
            />
          );
        })}
      </ParallaxLayer>
      <ImpactText text="MAKE IT YOURS." at={0} y={0.14} size={0.55} />
    </AbsoluteFill>
  );
};

/** The theme editor: the accent and background change in the pouch while they're typed. */
export const Editor: React.FC<{ until: number }> = ({ until }) => (
  <AbsoluteFill>
    <Footage
      clip="13-theme-editor"
      // Two hard cuts skip the moments the pouch redraws itself mid-edit.
      map={[
        [0, 2.9],
        [0.8, 4.4],
        [0.8001, 6.4],
        [1.6, 8.2],
        [1.6001, 9.0],
        [2.3, 10.6],
      ]}
      cam={[
        { t: 0, ...rect(300, 30, 1730, 1010) },
        { t: 2.3, ...rect(310, 60, 1720, 990) },
      ]}
    />
    <FeatureLabel title="Your colours" detail="Edit any theme and watch the pouch change" at={0.08} until={until} />
  </AbsoluteFill>
);

/** Sound packs in Settings. */
export const Sounds: React.FC<{ until: number }> = ({ until }) => (
  <AbsoluteFill>
    <Footage
      clip="14-sounds"
      map={[
        [0, 1.2],
        [1.3, 3.75],
      ]}
      cam={[
        { t: 0, ...rect(600, 470, 1380, 910) },
        { t: 1.3, ...rect(640, 480, 1360, 900) },
      ]}
    />
    <FeatureLabel title="Sound packs" detail="Soft, Bubbly, Clicky — or your own" at={0.05} until={until} pos="tl" />
  </AbsoluteFill>
);

/** The mascot's moods (rendered by Pouchy's Mascot control), popping in one by one. */
export const Mascot: React.FC = () => {
  const t = useTime();
  const { width, height } = useVideoConfig();
  const R = Math.min(width, height);
  const size = R * (width >= height ? 0.24 : 0.36);
  const landscape = width >= height;
  return (
    <AbsoluteFill>
      <Backdrop tint={C.pink} />
      {[0, 1, 2, 3].map((i) => {
        const s = springAt(t - 0.05 - i * 0.2, { stiffness: 260, damping: 11 });
        const col = landscape ? i : i % 2;
        const row = landscape ? 0 : Math.floor(i / 2);
        const cols = landscape ? 4 : 2;
        const x = width / 2 + (col - (cols - 1) / 2) * size * 1.05;
        const y = height * (landscape ? 0.47 : 0.42) + (row - (landscape ? 0 : 0.5)) * size * 1.05;
        const bob = Math.sin((t - i * 0.2) * 7) * size * 0.015;
        return (
          <Img
            key={i}
            src={staticFile(`renders/mascot-${i}.png`)}
            style={{
              position: 'absolute',
              left: x - size / 2,
              top: y - size / 2 + bob,
              width: size,
              height: size,
              objectFit: 'contain',
              transform: `scale(${s}) rotate(${(1 - s) * 30}deg)`,
            }}
          />
        );
      })}
      <SpringWord words={['It', 'reacts', 'to', 'what', 'you', 'drop.']} at={0.1} y={landscape ? 0.8 : 0.78} size={0.6} highlight={['drop.']} accent={C.pink} />
    </AbsoluteFill>
  );
};

const Verb: React.FC<{ word: string; clip: string; map: TimeMap; cam: CamKey[] }> = ({ word, clip, map, cam }) => {
  return (
    <AbsoluteFill>
      <Footage clip={clip} map={map} cam={cam} brightness={0.38} blur={2} />
      <AbsoluteFill style={{ background: `radial-gradient(ellipse at center, transparent 30%, rgba(11,9,24,0.75) 100%)` }} />
      <ImpactText text={word} at={0} size={1.25} tilt={-2} accent={C.pink} />
      <Flash at={0} peak={0.4} />
    </AbsoluteFill>
  );
};

/** SHAKE. PARK. DROP. — one word every second, each over its own moment of footage. */
export const Triad: React.FC = () => {
  const { fps } = useVideoConfig();
  const verbs: { word: string; clip: string; map: TimeMap; cam: CamKey[] }[] = [
    { word: 'SHAKE.', clip: '02-summon', map: [[0, 5.4], [1.1, 6.5]], cam: [{ t: 0, ...rect(640, 270, 1160, 680) }, { t: 1.1, ...rect(700, 300, 1120, 640) }] },
    { word: 'PARK.', clip: '02-summon', map: [[0, 9.85], [1.1, 10.9]], cam: [{ t: 0, ...rect(940, 320, 1480, 700) }, { t: 1.1, ...rect(980, 340, 1440, 680) }] },
    { word: 'DROP.', clip: '03-drop-anywhere', map: [[0, 7.6], [1.1, 8.6]], cam: [{ t: 0, ...rect(1020, 130, 1900, 890) }, { t: 1.1, ...rect(1100, 180, 1840, 860) }] },
  ];
  return (
    <AbsoluteFill>
      <Backdrop />
      {verbs.map((v, i) => (
        <Sequence key={v.word} from={i * fps} durationInFrames={Math.round((i === 2 ? 1.05 : 1) * fps)}>
          <Verb {...v} />
        </Sequence>
      ))}
    </AbsoluteFill>
  );
};

/** The end card. */
export const EndCard: React.FC = () => {
  const t = useTime();
  const { width, height } = useVideoConfig();
  const R = Math.min(width, height);
  const landscape = width >= height;
  const fade = easeInCubic(clamp((t - 3.0) / 0.5));
  const chips: { kind: 'image' | 'text' | 'link' | 'color' | 'folder'; label: string; x: number; y: number; r: number; swatch?: string }[] = [
    { kind: 'image', label: 'Sunset.jpg', x: 0.13, y: 0.2, r: -8 },
    { kind: 'link', label: 'github.com', x: 0.86, y: 0.18, r: 7 },
    { kind: 'text', label: 'Design review…', x: 0.1, y: 0.62, r: 6 },
    { kind: 'color', label: '#FF8A5B', x: 0.9, y: 0.6, r: -6, swatch: C.peach },
    { kind: 'folder', label: 'Project', x: 0.2, y: 0.88, r: -4 },
    { kind: 'image', label: 'Meeting note.png', x: 0.8, y: 0.88, r: 5 },
  ];
  return (
    <AbsoluteFill>
      <Backdrop glow={1.3} />
      <ParallaxLayer depth={1} drift={[-30, -18]}>
        {chips.map((c, i) => (
          <FileChip
            key={i}
            kind={c.kind}
            label={c.label}
            swatch={c.swatch}
            x={width * (landscape ? c.x : 0.5 + (c.x - 0.5) * 0.9)}
            y={height * (landscape ? c.y : 0.06 + c.y * 0.88)}
            at={0.35 + i * 0.12}
            size={R * 0.03}
            rotate={c.r}
          />
        ))}
      </ParallaxLayer>
      <LogoReveal at={0} y={landscape ? 0.3 : 0.32} />
      <SpringWord
        words={['Shake', 'your', 'mouse.', 'Park', 'anything.', 'Drop', 'it', 'anywhere.']}
        at={0.9}
        stagger={0.06}
        y={landscape ? 0.69 : 0.62}
        size={0.42}
        weight={600}
        highlight={['mouse.', 'anything.', 'anywhere.']}
        accent={C.violet}
      />
      <div style={{ position: 'absolute', left: 0, right: 0, top: height * (landscape ? 0.79 : 0.72), display: 'flex', justifyContent: 'center' }}>
        <Tag label="Free for Windows 10 & 11" at={1.6} color={C.pink} size={R * 0.03} style={{ transformOrigin: 'center' }} />
      </div>
      <div
        style={{
          position: 'absolute',
          left: 0,
          right: 0,
          top: height * (landscape ? 0.89 : 0.81),
          textAlign: 'center',
          fontFamily: text,
          fontWeight: 500,
          fontSize: R * 0.024,
          color: C.dim,
          opacity: clamp((t - 2.2) / 0.4),
          letterSpacing: '0.02em',
        }}
      >
        github.com/editorrylix/Pouchy · Small Thunder Works
      </div>
      <AbsoluteFill style={{ backgroundColor: '#000', opacity: fade }} />
    </AbsoluteFill>
  );
};
