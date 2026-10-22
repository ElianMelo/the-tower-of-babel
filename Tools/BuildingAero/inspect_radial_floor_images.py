"""Inspect renderer agreement and compose the floor authoring comparison."""
from pathlib import Path
import json
import numpy as np
from PIL import Image,ImageDraw,ImageFont
root=Path(__file__).resolve().parents[2]
notes=root/'Documentation/FrutigerAero/Buildings'
previews=notes/'Previews'
a=np.array(Image.open(previews/'RadialFloor-CPU-Sector.png').convert('RGB'),dtype=np.int16)
b=np.array(Image.open(previews/'RadialFloor-GPU-Sector.png').convert('RGB'),dtype=np.int16)
delta=np.abs(a-b)
report=dict(method='Fixed-camera authoring comparison of the actual shared CPU sector mesh and GPU instanced patch. Not an automated test or performance benchmark.',
    meanChannelDifference=float(delta.mean()),pixelsChangedOver2=int((delta.max(axis=2)>2).sum()),pixels=int(delta.shape[0]*delta.shape[1]))
(notes/'RadialFloorImageInspection.json').write_text(json.dumps(report,indent=2)+'\n')
sheet=Image.new('RGB',(2000,790),'#143644');draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',29)
for x,name,title in [(0,'Before-Joins','Before: overlapping square tiles'),(1000,'After-Joins','After: fitted curved tiles')]:
    image=Image.open(previews/f'RadialFloor-{name}.png').convert('RGB').resize((1000,750),Image.Resampling.LANCZOS)
    sheet.paste(image,(x,40));draw.text((x+20,1),title,font=font,fill='white')
sheet.save(previews/'RadialFloor-Comparison.png')
print(json.dumps(report,indent=2))
