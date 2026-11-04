"""Motion review for SkyWorkshopPreview.Capture(pass): a side-by-side GIF and a labelled key-frame sheet.

  python assemble_sky_workshop_preview.py Pass1 [fps]
Reads _Screenshots-and-previews-to-delete/SkyWorkshop/<pass>/Condenser_###.png and Node_###.png and writes
SkyWorkshop_<pass>.gif and SkyWorkshop_<pass>_Keyframes.png into the same folder.
"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

PASS = sys.argv[1] if len(sys.argv) > 1 else 'Pass1'
FPS = float(sys.argv[2]) if len(sys.argv) > 2 else 12
DIR = Path(__file__).resolve().parents[2] / '_Screenshots-and-previews-to-delete/SkyWorkshop' / PASS
font = lambda n, s: ImageFont.truetype('C:/Windows/Fonts/' + n, s)
BOLD, SMALL = font('segoeuib.ttf', 22), font('segoeui.ttf', 16)

cond = sorted(DIR.glob('Condenser_*.png')); node = sorted(DIR.glob('Node_*.png'))
n = min(len(cond), len(node)); S = 420


def phase(t):
    if t < 1.6: return 'Idle'
    if t < 2.65: return 'Activation: essence enters'
    if t < 5.1: return 'Working'
    if t < 6.4: return 'Complete'
    return 'Settling / respawn'


frames = []
for i in range(n):
    t = i / FPS
    img = Image.new('RGB', (S * 2, S + 34), (226, 240, 252)); d = ImageDraw.Draw(img)
    img.paste(Image.open(cond[i]).convert('RGB').resize((S, S), Image.LANCZOS), (0, 34))
    img.paste(Image.open(node[i]).convert('RGB').resize((S, S), Image.LANCZOS), (S, 34))
    d.text((10, 6), f'Wind Condenser  |  Cloud Dandelions      t = {t:4.1f} s   {phase(t)}', fill=(10, 60, 140), font=SMALL)
    frames.append(img.quantize(colors=192, method=Image.Quantize.FASTOCTREE))
gif = DIR / f'SkyWorkshop_{PASS}.gif'
frames[0].save(gif, save_all=True, append_images=frames[1:], duration=int(1000 / FPS), loop=0, optimize=False)

keys = [(1.0, 'Idle'), (2.1, 'Essence enters'), (3.4, 'Working'), (4.8, 'Late work'), (5.35, 'Complete pop'), (6.0, 'Settling')]
W = 300
sheet = Image.new('RGB', (W * len(keys) + 20, 2 * W + 110), (226, 240, 252)); d = ImageDraw.Draw(sheet)
d.text((12, 10), f'Sky workshop motion review - {PASS}', fill=(10, 60, 140), font=BOLD)
for k, (t, label) in enumerate(keys):
    i = min(n - 1, round(t * FPS)); x = 10 + k * W
    d.text((x + 6, 44), f'{label}  ({t:.1f} s)', fill=(30, 80, 150), font=SMALL)
    sheet.paste(Image.open(cond[i]).convert('RGB').resize((W - 6, W - 6), Image.LANCZOS), (x, 70))
    sheet.paste(Image.open(node[i]).convert('RGB').resize((W - 6, W - 6), Image.LANCZOS), (x, 70 + W))
out = DIR / f'SkyWorkshop_{PASS}_Keyframes.png'; sheet.save(out)
print(gif); print(out)
