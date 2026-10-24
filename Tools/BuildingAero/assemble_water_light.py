"""Assemble Unity's sampled Edit Mode aquatic-light review frames."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
sources = sorted((root / "Temp/AeroWaterLight").glob("frame-*.png"))
if len(sources) != 60:
    raise RuntimeError("Run AeroWaterLightPreview.Capture(true) in Unity first (60 frames).")
frames = []
for source in sources:
    with Image.open(source) as image:
        frames.append(image.convert("RGB").resize((864, 540), Image.Resampling.LANCZOS).quantize(colors=128))
output = root / "Documentation/FrutigerAero/Lighting/Previews/Nearby-Motion.gif"
frames[0].save(output, save_all=True, append_images=frames[1:], duration=50, loop=0, optimize=False)
print(f"Saved {output.name}: {len(frames)} frames, {output.stat().st_size} bytes")
