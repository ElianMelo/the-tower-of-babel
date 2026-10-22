"""Summarize the authoring transmission and instanced-render comparison images."""
import json
from pathlib import Path
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parents[2]
notes=root/'Documentation/FrutigerAero/Buildings'
previews=notes/'Previews'
c=json.loads((notes/'BorderGlassCaptureInspection.json').read_text())['crop']
panel=(c['x'],c['y'],c['x']+c['width'],c['y']+c['height'])
core=(340,275,650,325)
def difference(a,b,box):
    x=np.array(Image.open(previews/f'BorderGlass-{a}.png').convert('RGB').crop(box),dtype=np.int16)
    y=np.array(Image.open(previews/f'BorderGlass-{b}.png').convert('RGB').crop(box),dtype=np.int16)
    delta=np.abs(x-y)
    return dict(meanChannelDifference=float(delta.mean()),pixelsChangedOver2=int((delta.max(axis=2)>2).sum()),pixels=int(delta.shape[0]*delta.shape[1]))
report=dict(method='Fixed-camera authoring image inspection; no automated test runner or performance measurement.',panelCrop=panel,whiteCoreCrop=core,
    glassBackgroundResponse=difference('Transmission-A','Transmission-B',panel),
    opaqueBackgroundResponse=difference('Opaque-Control-A','Opaque-Control-B',panel),
    whiteCoreBackgroundResponse=difference('Transmission-A','Transmission-B',core),
    instancedVsMeshRenderer=difference('Transmission-A','Instanced-Transmission',panel),
    distantVsOpaque=difference('Opaque-Control-A','Instanced-DistantFallback',panel))
(notes/'BorderGlassImageInspection.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
