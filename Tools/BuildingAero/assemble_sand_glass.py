"""Assemble the disposable Unity Edit Mode samples into a compact motion preview."""
from pathlib import Path
from PIL import Image, ImageChops

root = Path(__file__).resolve().parents[2]
paths = sorted((root / 'Temp/SandGlassWork').glob('frame-*.png'))
if len(paths) != 84:
    raise RuntimeError('Capture all 84 Unity workshop frames first.')
with Image.open(paths[0]) as image:
    first = image.convert('RGB').resize((960, 600), Image.Resampling.LANCZOS)
palette = first.quantize(colors=256)
frames = []
for path in paths:
    with Image.open(path) as image:
        frames.append(image.convert('RGB').resize((960, 600), Image.Resampling.LANCZOS)
                      .quantize(palette=palette, dither=Image.Dither.NONE))
output = root / 'Documentation/FrutigerAero/Resources/Previews/Sand-Glass-Working.gif'
frames[0].save(output, save_all=True, append_images=frames[1:], duration=50, loop=0, disposal=1)
with Image.open(paths[36]) as image:
    bounds = ImageChops.difference(first, image.convert('RGB').resize((960, 600), Image.Resampling.LANCZOS)).getbbox()
print({'frames': len(frames), 'output': str(output), 'motion_bounds': bounds})
