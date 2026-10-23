"""Assemble the disposable Unity asset-motion review frames."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
paths = sorted((root / "Temp/InteractionWeave").glob("frame-*.png"))
if len(paths) != 84:
    raise RuntimeError("Capture the 84 Unity weave review frames first.")
with Image.open(paths[12]) as source:
    palette = source.convert("RGB").quantize(colors=256)
frames = []
for path in paths:
    with Image.open(path) as source:
        frames.append(source.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE))
output = root / "Documentation/FrutigerAero/Previews/Interaction-Weave.gif"
frames[0].save(output, save_all=True, append_images=frames[1:], duration=42, loop=0, disposal=1)
print(output)
