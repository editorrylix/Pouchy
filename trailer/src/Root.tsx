import React from 'react';
import { Composition } from 'remotion';
import { Trailer, TrailerProps } from './Trailer';
import timeline from './timeline.json';

const defaults: TrailerProps = { captions: true, audio: 'none' };

// One edit, several deliveries: the camera reframes the footage for each aspect ratio.
const FORMATS = [
  { id: 'Master60', width: 1920, height: 1080, fps: 60 },
  { id: 'Master30', width: 1920, height: 1080, fps: 30 },
  { id: 'Vertical', width: 1080, height: 1920, fps: 30 },
  { id: 'Square', width: 1080, height: 1080, fps: 30 },
  { id: 'Portrait', width: 1080, height: 1350, fps: 30 },
];

export const Root: React.FC = () => (
  <>
    {FORMATS.map((f) => (
      <Composition
        key={f.id}
        id={f.id}
        component={Trailer}
        width={f.width}
        height={f.height}
        fps={f.fps}
        durationInFrames={Math.round(timeline.duration * f.fps)}
        defaultProps={defaults}
      />
    ))}
  </>
);
