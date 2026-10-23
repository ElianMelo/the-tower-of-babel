"""Assemble the disposable Unity UI motion review frames."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
paths = sorted((root / "Temp/UIJuice").glob("frame-*.png"))
if len(paths) != 96:
    raise RuntimeError("Capture the 96 Unity UI review frames first.")
with Image.open(paths[26]) as source:
    palette = source.convert("RGB").resize((960, 540), Image.Resampling.LANCZOS).quantize(colors=256)
frames = []
for path in paths:
    with Image.open(path) as source:
        frames.append(source.convert("RGB").resize((960, 540), Image.Resampling.LANCZOS).quantize(palette=palette, dither=Image.Dither.NONE))
output = root / "Documentation/FrutigerAero/Previews/UI-Feedback.gif"
frames[0].save(output, save_all=True, append_images=frames[1:], duration=42, loop=0, disposal=1)
print(output)
