import { Config } from '@remotion/cli/config';

// Masters are encoded once more by render.ps1 (with the mixed audio), so render near-lossless here.
Config.setVideoImageFormat('jpeg');
Config.setJpegQuality(95);
Config.setCodec('h264');
Config.setCrf(12);
Config.setPixelFormat('yuv420p');
Config.setConcurrency(6);
