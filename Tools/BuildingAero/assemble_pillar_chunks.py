"""Assemble the rendered chunk formation and arrival review."""
from pathlib import Path
from PIL import Image

root=Path(__file__).resolve().parents[2]
paths=sorted((root/'Temp/PillarChunks').glob('frame-*.png'))
if len(paths)!=90:
    raise RuntimeError('Capture the 90 Unity chunk-flight frames first.')
with Image.open(paths[30]) as source:
    palette=source.convert('RGB').resize((960,720),Image.Resampling.LANCZOS).quantize(colors=256)
frames=[]
for path in paths:
    with Image.open(path) as source:
        frames.append(source.convert('RGB').resize((960,720),Image.Resampling.LANCZOS)
                      .quantize(palette=palette,dither=Image.Dither.NONE))
output=root/'Documentation/FrutigerAero/Buildings/Previews/Pillar-Chunks-Arrival.gif'
frames[0].save(output,save_all=True,append_images=frames[1:],duration=[30,30,40]*30,loop=0,disposal=1)
print(output)
