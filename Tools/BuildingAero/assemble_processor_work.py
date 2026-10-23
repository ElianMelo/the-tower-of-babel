"""Assemble the sampled Unity processor poses into a review animation."""
from pathlib import Path
from PIL import Image, ImageChops

root = Path(__file__).resolve().parents[2]
paths = sorted((root / 'Temp/ProcessorWork').glob('frame-*.png'))
if len(paths) != 96:
    raise RuntimeError('Capture the 96 Unity processor frames first.')
with Image.open(paths[0]) as source:
    first = source.convert('RGB').resize((960, 600), Image.Resampling.LANCZOS)
palette = first.quantize(colors=256)
frames = []
for path in paths:
    with Image.open(path) as source:
        frames.append(source.convert('RGB').resize((960, 600), Image.Resampling.LANCZOS)
                      .quantize(palette=palette, dither=Image.Dither.NONE))
output = root / 'Documentation/FrutigerAero/Resources/Previews/Processor-Working.gif'
frames[0].save(output, save_all=True, append_images=frames[1:],
               duration=[40, 40, 40, 40, 40, 50] * 16, loop=0, disposal=1)
with Image.open(paths[26]) as source:
    difference = ImageChops.difference(first, source.convert('RGB').resize((960, 600), Image.Resampling.LANCZOS))
    print({'output': str(output), 'motionBounds': difference.getbbox()})
