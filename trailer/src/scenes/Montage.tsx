// Act two: everything else Pouchy does, one beat each. Every shot is the real app, recorded.
import React from 'react';
import { AbsoluteFill } from 'remotion';
import { Backdrop } from '../components/Backdrop';
import { FeatureBurst, FeatureLabel } from '../components/Chips';
import { Footage } from '../components/Footage';
import { SplitScene } from '../components/Layout';
import { Spotlight } from '../components/Spotlight';
import { SpringWord } from '../components/SpringWord';
import { rect } from '../lib/camera';
import { C } from '../theme';

export const DropActions: React.FC<{ until: number }> = ({ until }) => (
  <AbsoluteFill>
    <Footage
      clip="04-drop-actions"
      map={[
        [0, 8.9],
        [1.3, 12.2],
        [1.5, 12.6],
        [2.4, 13.8],
      ]}
      cam={[
        { t: 0, ...rect(1080, 250, 1630, 850) },
        { t: 1.2, ...rect(1110, 300, 1610, 820) },
        { t: 1.55, ...rect(1110, 300, 1610, 820) },
        { t: 2.1, ...rect(140, 110, 1640, 900) },
      ]}
    />
    <FeatureLabel n={1} title="Drop actions" detail="Resize, zip, convert or copy to a folder — as you drop" at={0.1} until={until} />
  </AbsoluteFill>
);

export const TextFromImages: React.FC<{ until: number }> = ({ until }) => (
  <AbsoluteFill>
    <Footage
      clip="05-ocr"
      map={[
        [0, 7.25],
        [0.75, 8.2],
        [0.9, 8.5],
        [1.9, 9.8],
      ]}
      cam={[
        { t: 0, ...rect(1080, 230, 1640, 690) },
        { t: 0.85, ...rect(1080, 230, 1640, 690) },
        { t: 1.4, ...rect(1300, 300, 1640, 600) },
      ]}
    >
      <Spotlight x={1515} y={440} w={170} h={190} at={0.95} until={1.6} />
    </Footage>
    <FeatureLabel n={2} title="Text from images" detail="Drop a picture on Text — out come the words" at={0.08} until={until} />
  </AbsoluteFill>
);

export const Captures: React.FC<{ until: number }> = ({ until }) => (
  <AbsoluteFill>
    <Backdrop />
    <SpringWord words={['Snip', 'it.', 'Copy', 'it.', "It's", 'in', 'the', 'pouch.']} at={0.05} until={until - 0.2} y={0.1} size={0.5} highlight={['pouch.']} accent={C.pink} />
    <SplitScene at={0.05} labels={['Screenshots', 'Clipboard history']}>
      {[
        (size) => (
          <Footage
            clip="06-screenshot"
            width={size.width}
            height={size.height}
            map={[
              [0, 3.3],
              [0.6, 4.4],
              [0.75, 5.0],
              [0.95, 6.9],
              [2.6, 8.6],
            ]}
            cam={[
              { t: 0, ...rect(250, 240, 1330, 900) },
              { t: 1.0, ...rect(330, 260, 1330, 900) },
              { t: 1.6, ...rect(700, 280, 1330, 880) },
            ]}
          />
        ),
        (size) => (
          <Footage
            clip="07-clipboard"
            width={size.width}
            height={size.height}
            map={[
              [0, 2.2],
              [2.6, 9.45],
            ]}
            cam={[
              { t: 0, ...rect(80, 140, 1630, 770) },
              { t: 2.6, ...rect(80, 140, 1630, 770) },
            ]}
          />
        ),
      ]}
    </SplitScene>
  </AbsoluteFill>
);

export const ExplorerMenu: React.FC<{ until: number }> = ({ until }) => (
  <AbsoluteFill>
    <Footage
      clip="08-explorer-menu"
      map={[
        [0, 3.6],
        [0.75, 5.6],
        [0.8, 6.1],
        [1.25, 7.6],
        [2.1, 8.7],
      ]}
      cam={[
        { t: 0, ...rect(400, 360, 1000, 860) },
        { t: 0.85, ...rect(400, 360, 1000, 860) },
        { t: 1.3, ...rect(520, 300, 1400, 870) },
        { t: 2.1, ...rect(560, 320, 1380, 860) },
      ]}
    >
      <Spotlight x={715} y={556} w={262} h={34} at={0.25} until={0.8} radius={6} />
    </Footage>
    <FeatureLabel n={3} title="Right-click → Add to Pouchy" detail="Straight from File Explorer, or Send to" at={0.08} until={until} pos="br" />
  </AbsoluteFill>
);

export const SmartShelves: React.FC<{ until: number }> = ({ until }) => (
  <AbsoluteFill>
    <Footage
      clip="09-smart-shelves"
      map={[
        [0, 5.9],
        [0.6, 6.9],
        [1.0, 9.3],
        [1.3, 9.9],
        [2.1, 12.6],
      ]}
      cam={[
        { t: 0, ...rect(1080, 230, 1640, 690) },
        { t: 2.1, ...rect(1100, 240, 1620, 680) },
      ]}
    />
    <FeatureLabel n={4} title="Smart shelves" detail="Pictures sort themselves. Ctrl+F finds anything." at={0.08} until={until} />
  </AbsoluteFill>
);

export const QuickLook: React.FC<{ until: number }> = ({ until }) => (
  <AbsoluteFill>
    <Footage
      clip="15-quicklook"
      map={[
        [0, 2.9],
        [0.5, 3.5],
        [1.6, 4.65],
      ]}
      cam={[
        { t: 0, ...rect(20, 120, 1500, 960) },
        { t: 1.6, ...rect(60, 140, 1480, 940) },
      ]}
    />
    <FeatureLabel n={5} title="Quick Look" detail="Peek at files without opening them" keys="Space" at={0.08} until={until} pos="tr" />
  </AbsoluteFill>
);

export const Palette: React.FC<{ until: number }> = ({ until }) => (
  <AbsoluteFill>
    <Footage
      clip="10-palette-themes"
      map={[
        [0, 1.3],
        [1.6, 3.75],
      ]}
      cam={[
        { t: 0, ...rect(520, 110, 1660, 790) },
        { t: 1.6, ...rect(560, 130, 1640, 770) },
      ]}
    />
    <FeatureLabel n={6} title="Command palette" detail="Commands, themes and your items — one search away" keys="Ctrl+K" at={0.08} until={until} />
  </AbsoluteFill>
);

export const Organise: React.FC<{ until: number }> = ({ until }) => (
  <AbsoluteFill>
    <Footage
      clip="11-organise"
      map={[
        [0, 3.3],
        [0.55, 5.6],
        [0.75, 7.2],
        [1.05, 8.6],
        [1.35, 11.1],
        [1.55, 12.1],
        [2.1, 15.4],
      ]}
      cam={[
        { t: 0, ...rect(1060, 100, 1740, 940) },
        { t: 1.2, ...rect(1080, 100, 1640, 760) },
        { t: 2.1, ...rect(1080, 100, 1640, 760) },
      ]}
    />
    <FeatureLabel n={7} title="Label, pin, undo" detail="Grid, list or compact — your pouch, your way" at={0.08} until={until} />
  </AbsoluteFill>
);

export const AndMore: React.FC<{ until: number }> = ({ until }) => (
  <AbsoluteFill>
    <Backdrop tint={C.pink} />
    <FeatureBurst
      at={0.02}
      until={until}
      center="+ more"
      words={['Quick Look', 'Command palette', 'Labels & pins', 'Sound packs', 'Stacks', 'Notes', 'Link previews', 'Undo', 'Multiple shelves', 'Your own scripts', 'Auto-clear', 'Hotkeys']}
    />
  </AbsoluteFill>
);
