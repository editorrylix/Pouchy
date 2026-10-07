// The trailer: scenes at fixed times on a 120 BPM grid (see timeline.json), real footage inside,
// motion graphics around it. Audio is mixed separately from the same timeline (audio/*.py) and
// muxed in by render.ps1, so each audio variant doesn't need its own video render.
import React from 'react';
import { AbsoluteFill, Audio, staticFile } from 'remotion';
import { Grain, Vignette } from './components/Backdrop';
import { Caption } from './components/Caption';
import { Scene, TransitionSpec } from './components/Transitions';
import { Hook, Magic, DropAnywhere } from './scenes/Opening';
import { DropActions, TextFromImages, Captures, ExplorerMenu, SmartShelves, AndMore } from './scenes/Montage';
import { Themes, Editor, Triad, EndCard } from './scenes/Closing';
import timeline from './timeline.json';
import voTimings from './vo-timings.json';

export type TrailerProps = {
  captions: boolean;
  /** Preview only: play the mixed soundtrack from public/audio (the final files are muxed by render.ps1). */
  audio: 'none' | 'full';
};

type Cut = { id: keyof typeof timeline.scenes; enter: TransitionSpec; render: (until: number) => React.ReactNode };

const CUTS: Cut[] = [
  { id: 'hook', enter: { kind: 'cut' }, render: () => <Hook /> },
  { id: 'magic', enter: { kind: 'zoom', dur: 0.3 }, render: () => <Magic /> },
  { id: 'drop', enter: { kind: 'whip', dur: 0.25, dir: 1 }, render: () => <DropAnywhere /> },
  { id: 'm1', enter: { kind: 'mask', dur: 0.4, x: 70, y: 50 }, render: (u) => <DropActions until={u} /> },
  { id: 'm2', enter: { kind: 'whip', dur: 0.22, dir: -1 }, render: (u) => <TextFromImages until={u} /> },
  { id: 'm3', enter: { kind: 'zoom', dur: 0.25 }, render: (u) => <Captures until={u} /> },
  { id: 'm4', enter: { kind: 'whip', dur: 0.22, dir: 1 }, render: (u) => <ExplorerMenu until={u} /> },
  { id: 'm5', enter: { kind: 'mask', dur: 0.32, shape: 'pill' }, render: (u) => <SmartShelves until={u} /> },
  { id: 'burst', enter: { kind: 'zoom', dur: 0.22 }, render: (u) => <AndMore until={u} /> },
  { id: 'themes', enter: { kind: 'mask', dur: 0.4 }, render: () => <Themes /> },
  { id: 'editor', enter: { kind: 'whip', dur: 0.25, dir: 1 }, render: (u) => <Editor until={u} /> },
  { id: 'triad', enter: { kind: 'cut' }, render: () => <Triad /> },
  { id: 'end', enter: { kind: 'mask', dur: 0.5 }, render: () => <EndCard /> },
];

/** The outgoing half of a transition: whips and zooms move both scenes, masks cover the old one. */
const exitFor = (next: TransitionSpec): TransitionSpec => (next.kind === 'whip' || next.kind === 'zoom' ? next : { kind: 'cut' });
const durOf = (s: TransitionSpec) => (s.kind === 'cut' ? 0 : s.dur);

export const Trailer: React.FC<TrailerProps> = ({ captions, audio }) => {
  return (
    <AbsoluteFill style={{ backgroundColor: '#0B0918' }}>
      {CUTS.map((cut, i) => {
        const [from] = timeline.scenes[cut.id];
        const next = CUTS[i + 1];
        const nextFrom = next ? timeline.scenes[next.id][0] : timeline.duration;
        const to = next ? nextFrom + durOf(next.enter) : timeline.duration;
        return (
          <Scene key={cut.id} name={cut.id} from={from} to={to} enter={cut.enter} exit={next ? exitFor(next.enter) : { kind: 'cut' }}>
            {cut.render(nextFrom - from)}
          </Scene>
        );
      })}
      <Vignette strength={0.35} />
      <Grain opacity={0.05} />
      {captions && (
        <Scene from={0} to={timeline.duration} name="captions">
          {/* The later lines repeat what's already on screen in big type, so they get no caption. */}
          {timeline.vo.filter((v) => v.t < timeline.scenes.burst[0]).map((v) => (
            <Caption key={v.id} text={v.text} at={v.t} dur={(voTimings as Record<string, number>)[v.id] ?? 1.2} />
          ))}
        </Scene>
      )}
      {audio === 'full' && <Audio src={staticFile('audio/mix-full.wav')} />}
    </AbsoluteFill>
  );
};
