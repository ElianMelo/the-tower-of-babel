"""Assemble the Unity construction review captures into a step-by-step preview."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
folder = root / 'Documentation/FrutigerAero/Buildings/Previews'
images = [Image.open(folder / f'Pillar-Stage-{stage:02}.png').convert('RGB') for stage in range(11)]
palette = images[-1].quantize(colors=256)
frames = [image.quantize(palette=palette, dither=Image.Dither.NONE) for image in images]
frames[0].save(folder / 'Pillar-Construction-Sequence.gif', save_all=True, append_images=frames[1:],
               duration=[1100]+[650]*9+[1600], loop=0, disposal=2)
sheet = Image.new('RGB', (360*6,760*2),(148,184,206))
for stage,image in enumerate(images):
    sheet.paste(image,((stage%6)*360,(stage//6)*760))
sheet.save(folder / 'Pillar-Construction-Stages.png')
for image in images:
    image.close()
print(folder / 'Pillar-Construction-Sequence.gif')
