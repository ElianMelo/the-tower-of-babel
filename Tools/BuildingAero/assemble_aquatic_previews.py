"""Assemble captured Unity frames and record image differences for visual review."""
from pathlib import Path
import json
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parents[2]
notes=root/'Documentation/FrutigerAero/Buildings'
previews=notes/'Previews'
capture=json.loads((notes/'AeroFlowCaptureInspection.json').read_text())
paths=sorted((root/'Temp/BuildingAero/AeroFlow/Frames').glob('Flow-*.png'))
frames=[Image.open(p).convert('RGB') for p in paths]
frames[0].save(previews/'AeroFlow-Motion.gif',save_all=True,append_images=frames[1:],duration=160,loop=0,optimize=False)
c=capture['crop']
box=(c['x'],c['y'],c['x']+c['width'],c['y']+c['height'])
motion_box=(360,231,585,279) # Interior of the angled arch's inset; comparison copies use a different camera.
def difference(a,b,roi=box):
    x=np.array(Image.open(a).convert('RGB').crop(roi),dtype=np.int16)
    y=np.array(Image.open(b).convert('RGB').crop(roi),dtype=np.int16)
    delta=np.abs(x-y)
    return dict(meanChannelDifference=float(delta.mean()),pixelsChangedOver2=int((delta.max(axis=2)>2).sum()),pixels=int(delta.shape[0]*delta.shape[1]))
instance=root/'Temp/BuildingAero/AeroFlow/Frames'
live0=json.loads((notes/'AeroFlowLiveFrame0.json').read_text())
live1=json.loads((notes/'AeroFlowLiveFrame1.json').read_text())
report=dict(
    method='Fixed-camera RGB image inspection of the inset only; this is not a test-runner result or a performance benchmark.',
    frames=len(frames),durationSeconds=len(frames)*.16,instanceCrop=c,motionCrop=list(motion_box),
    authoringMotion=difference(paths[0],paths[-1],motion_box),
    instanceVariation=difference(instance/'Instance-Varied-0.png',instance/'Instance-Varied-1.png'),
    variationDisabledControl=difference(instance/'Instance-Control-0.png',instance/'Instance-Control-1.png'),
    liveClockMotion=difference(previews/'AeroFlow-Live-0.png',previews/'AeroFlow-Live-1.png',motion_box),
    liveFrames=[live0['frame'],live1['frame']],liveTimes=[live0['time'],live1['time']],
    liveClockOverrides=[live0['clock'],live1['clock']])
(notes/'AeroFlowImageInspection.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
