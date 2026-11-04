"""Labelled contact sheets for the sky concept rounds (run after the matching render).

  python assemble_sky_processor_concepts.py [processor01 | condenser02 | essence01]   (default: all)
Each sheet is a grid of the close-ups with a number, name and a short note, written next to the renders.
"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

SHOTS = Path(__file__).resolve().parents[2] / '_Screenshots-and-previews-to-delete'
SHEETS = {
    'processor01': ('SkyProcessorConcepts/Round01', 'SkyProcessor', 'SkyProcessor_Concepts_Round01.png',
                    'Sky essence processor - concepts, round 01', 'Sky Essence in (cloud tuft)  ->  Sky Block out (front)', {
        '01': ('Bubble Still', 'Iridescent soap bubble on a chrome hourglass'),
        '02': ('Cloud Lava Lamp', 'Cobalt candy metal, cloud wax in blue liquid'),
        '03': ('Wind Condenser', 'White turbine, frosted blades, grass ring'),
        '04': ('Gel Capsule', 'Translucent bondi-blue pill on chrome legs'),
        '05': ('Sky Balloon', 'Striped glossy envelope over a basket'),
        '06': ('Gyro Orb', 'Gel rings around a glowing cloud core'),
        '07': ('Terrarium Dome', 'Glass dome, grassy island, tree and clouds'),
        '08': ('Bloom Flower', 'Translucent leaf petals round a glass bulb'),
        '09': ('Aurora Arch', 'Emissive aurora arch over a crystal prism'),
        '10': ('Jelly Levitator', 'Thin-film jelly bell over a magnetic pad')}),
    'condenser02': ('SkyProcessorConcepts/Round02', 'WindCondenser', 'WindCondenser_Variants_Round02.png',
                    'Wind Condenser (03) - variants, round 02', 'Richer green bases, more cloud and wind effects', {
        '03A': ('Bliss Hill', 'Rolling hill, daisies; cloud ring and wind spiral'),
        '03B': ('Leaf Rotor Pond', 'Mirror pond, reeds; leaf blades trail cloud'),
        '03C': ('Helix Vortex', 'Grass terraces; helix rotor, cloud tornado'),
        '03D': ('Twin Rotor Grove', 'Knoll with trees; two rotors, cumulus cap'),
        '03E': ('Sky Island', 'Floating island; clouds pour off its rim')}),
    'essence01': ('SkyEssenceConcepts/Round01', 'SkyEssence', 'SkyEssence_Concepts_Round01.png',
                  'Sky essence resource - concepts, round 01', 'Cloud = gathered essence  /  ground, grass, stone = base that stays', {
        '01': ('Cumulus Nest', 'Cumulus in a grass nest, white pebbles'),
        '02': ('Dust Devil', 'Cloud spiral and wind rising from swirled grass'),
        '03': ('Cloud Dandelions', 'Cloud seed heads; leaf rosette stays'),
        '04': ('Cotton Cloud Bush', 'Pale twig shrub fruiting cloud bolls'),
        '05': ('Sky Vent', 'Stone vent with crystals breathing cloud'),
        '06': ('Mirror Pond', 'Cloud resting on a sky-mirror pond'),
        '07': ('Crystal Geode', 'Sky crystals with cloud wisps and bubbles'),
        '08': ('Breeze Sheaf', 'Wind-bent tall grass, cloud streaming through'),
        '09': ('Cloud Bubbles', 'Iridescent bubbles holding clouds, on moss'),
        '10': ('Wind Arch', 'Wind-carved arch with a cloud stream')}),
}
W, H, BAR, PAD, HEAD = 480, 600, 92, 14, 74
font = lambda name, size: ImageFont.truetype('C:/Windows/Fonts/' + name, size)
BIG, TITLE, SMALL = font('segoeuib.ttf', 34), font('segoeuib.ttf', 24), font('segoeui.ttf', 18)


def sheet(key):
    folder, prefix, out_name, heading, subtitle, notes = SHEETS[key]
    shots = SHOT_DIR = SHOTS / folder
    files = sorted(SHOT_DIR.glob(prefix + '_*_*.png'))
    cols = min(5, len(files)); rows = (len(files) + cols - 1) // cols
    img = Image.new('RGB', (W * cols + PAD * (cols + 1), HEAD + (H + BAR) * rows + PAD * (rows + 1)), (226, 240, 252))
    d = ImageDraw.Draw(img)
    d.text((PAD + 4, 18), heading, fill=(10, 60, 140), font=BIG)
    d.text((img.width - PAD - 4, 30), subtitle, fill=(40, 90, 160), font=SMALL, anchor='ra')
    for i, f in enumerate(files):
        num = f.stem.split('_')[1]; name, note = notes[num]
        x = PAD + (i % cols) * (W + PAD); y = HEAD + PAD + (i // cols) * (H + BAR + PAD)
        img.paste(Image.open(f).convert('RGB').resize((W, H), Image.LANCZOS), (x, y))
        d.rectangle((x, y + H, x + W, y + H + BAR), fill=(255, 255, 255))
        badge = 50 + 14 * (len(num) - 2)
        d.rounded_rectangle((x + 12, y + H + 14, x + 12 + badge, y + H + 54), 12, fill=(20, 110, 220))
        d.text((x + 12 + badge / 2, y + H + 34), num, fill='white', font=TITLE, anchor='mm')
        d.text((x + 24 + badge, y + H + 14), name, fill=(10, 50, 110), font=TITLE)
        d.text((x + 24 + badge, y + H + 50), note, fill=(70, 100, 140), font=SMALL)
    out = shots / out_name; img.save(out); print(out)


for key in (sys.argv[1:] or SHEETS): sheet(key)
