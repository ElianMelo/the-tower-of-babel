"""Assemble the Unity Edit Mode visual samples into a compact looping review."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
paths = sorted((root / "Temp/ExperienceFeedback").glob("frame-*.png"))
if len(paths) != 72:
    raise RuntimeError("Capture all 72 visual frames in Unity before assembling the preview.")
with Image.open(paths[34]) as reference:
    palette = reference.convert("RGB").resize((960, 540), Image.Resampling.LANCZOS).quantize(colors=256)
frames = []
for path in paths:
    with Image.open(path) as source:
        frames.append(source.convert("RGB").resize((960, 540), Image.Resampling.LANCZOS)
                      .quantize(palette=palette, dither=Image.Dither.NONE))
destination = root / "Documentation/FrutigerAero/Previews/Experience-Sequence.gif"
frames[0].save(destination, save_all=True, append_images=frames[1:],
               duration=[300] + [42] * 70 + [650], loop=0, optimize=True, disposal=1)
print(destination)
