"""Assemble the Unity-rendered caustic frames and record live image differences."""
import json
import argparse
from pathlib import Path

import numpy as np
from PIL import Image

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
mode = parser.add_mutually_exclusive_group()
mode.add_argument('--inner', action='store_true', help='Use the corrected Unity inner-water captures')
mode.add_argument('--layered', action='store_true', help='Use the randomized layered-water captures')
args = parser.parse_args()
prefix = 'LayeredWater' if args.layered else ('WaterGlass-Inner' if args.inner else 'WaterGlass')
frame_dir = 'LayeredFrames' if args.layered else ('InnerFrames' if args.inner else 'Frames')
frame_seconds = .25 if args.inner or args.layered else .5
output = root / 'Documentation/FrutigerAero/Buildings/Previews'
output.mkdir(parents=True, exist_ok=True)
frames = [Image.open(p).convert('RGB') for p in sorted(
    (root / 'Temp/BuildingAero/Glass' / frame_dir).glob('Water-*.png'))]
frames[0].save(output / f'{prefix}-Motion.gif', save_all=True,
               append_images=frames[1:], duration=round(frame_seconds*1000), loop=0, optimize=False)
live_dir = root / 'Assets/Documentation/FrutigerAero/Buildings/Previews'
if args.inner or args.layered or not live_dir.exists():
    live_dir = output
live_prefix = f'{prefix}-Live' if args.inner or args.layered else f'{prefix}-Motion'
a, b = [np.array(Image.open(live_dir / f'{live_prefix}-{label}.png').convert('RGB')).astype(float)
        for label in ('A', 'B')]
delta = np.max(np.abs(a-b), axis=2)
report = {'captureSize': [a.shape[1], a.shape[0]],
          'pixelsChangedAbove2Levels': int((delta > 2).sum()),
          'meanAbsoluteChannelDifference': float(np.abs(a-b).mean()),
          'previewFrames': len(frames), 'previewFrameSeconds': frame_seconds,
          'note': 'Live captures use engine time, fixed camera, and _MotionTime=-1. GIF samples a fixed clock for presentation.'}
if args.inner or args.layered:
    live_name = 'LayeredLive' if args.layered else 'InnerLive'
    report['liveCaptures'] = [json.loads((root / f'Temp/BuildingAero/Glass/{live_name}-{label}.json').read_text()) for label in ('A', 'B')]
report_name = 'LayeredWaterMotionInspection.json' if args.layered else ('InnerWaterMotionInspection.json' if args.inner else 'WaterGlassMotionInspection.json')
(output.parent / report_name).write_text(json.dumps(report, indent=2))
if not args.inner and not args.layered:
    for name in ('WaterGlass-Kit.png', 'WaterGlass-Motion-A.png', 'WaterGlass-Motion-B.png'):
        Image.open(live_dir / name).save(output / name)
frames[0].save(output / f'{prefix}-Arch.png')
print(json.dumps(report))
