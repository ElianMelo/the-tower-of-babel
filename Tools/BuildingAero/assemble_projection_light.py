"""Assemble the sampled Unity water-projector preview with a stable shared palette."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
paths = sorted((root / "Temp/AquaProjection").glob("frame-*.png"))
if len(paths) != 60:
    raise RuntimeError("Run AeroAtmospherePreview.Capture(true) in Unity first.")
frames = []
palette = None
for path in paths:
    with Image.open(path) as image:
        frame = image.convert("RGB").resize((864, 540), Image.Resampling.LANCZOS)
        if palette is None:
            palette = frame.quantize(colors=128)
        frames.append(frame.quantize(palette=palette, dither=Image.Dither.NONE))
output = root / "Documentation/FrutigerAero/Lighting/Previews/Projection-Motion.gif"
frames[0].save(output, save_all=True, append_images=frames[1:], duration=50, loop=0, optimize=True)
print(f"Saved {output.name}: {len(frames)} frames, {output.stat().st_size} bytes")
