"""Assemble the sampled Unity wind animation without changing asset data."""
from pathlib import Path
from PIL import Image, ImageChops

root=Path(__file__).resolve().parents[2]
paths=sorted((root/'Temp/BushWind').glob('frame-*.png'))
if len(paths)!=72:
    raise RuntimeError('Capture the 72 Unity wind frames first.')
with Image.open(paths[0]) as source:
    first=source.convert('RGB').resize((960,540),Image.Resampling.LANCZOS)
palette=first.quantize(colors=256)
frames=[]
for path in paths:
    with Image.open(path) as source:
        frames.append(source.convert('RGB').resize((960,540),Image.Resampling.LANCZOS).quantize(palette=palette,dither=Image.Dither.NONE))
output=root/'Documentation/FrutigerAero/Foliage/Previews/Bush-Wind.gif'
frames[0].save(output,save_all=True,append_images=frames[1:],duration=83,loop=0,disposal=1)
with Image.open(paths[18]) as source:
    difference=ImageChops.difference(first,source.convert('RGB').resize((960,540),Image.Resampling.LANCZOS))
    print({'output':str(output),'motionBounds':difference.getbbox()})
