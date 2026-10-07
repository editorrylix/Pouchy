import { loadFont as loadDisplay } from '@remotion/google-fonts/BricolageGrotesque';
import { loadFont as loadText } from '@remotion/google-fonts/Inter';

// Pouchy's own palette: Midnight's accent, the mascot's pink, the sample swatch's peach.
export const C = {
  ink: '#0B0918',
  ink2: '#151027',
  violet: '#8B7CFF',
  violetDeep: '#5B4BE0',
  pink: '#FF5C8A',
  peach: '#FF8A5B',
  cream: '#FFF4EC',
  white: '#FFFFFF',
  dim: 'rgba(255,255,255,0.62)',
};

export const display = loadDisplay('normal', { weights: ['600', '800'], subsets: ['latin'] }).fontFamily;
export const text = loadText('normal', { weights: ['500', '700'], subsets: ['latin'] }).fontFamily;
