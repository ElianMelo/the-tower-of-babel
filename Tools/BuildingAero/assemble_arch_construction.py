"""Assemble rendered arch stages and the incoming-chunk motion review."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parents[2]
folder=root/'Documentation/FrutigerAero/Buildings/Previews'
stages=[Image.open(folder/f'Arch-Stage-{i:02}.png').convert('RGB') for i in range(11)]
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26)
sheet=Image.new('RGB',(640*4,650*3),(143,191,219))
for i,source in enumerate(stages):
    tile=Image.new('RGB',(640,650),(143,191,219)); tile.paste(source,(0,0))
    label='Blueprint' if i==0 else 'Complete' if i==10 else f'Random fill {i*10}%'
    ImageDraw.Draw(tile).text((320,612),f'{i:02}  {label}',fill=(20,65,90),font=font,anchor='mm')
    sheet.paste(tile,((i%4)*640,(i//4)*650))
sheet.save(folder/'Arch-Construction-Stages.png')
palette=stages[-1].quantize(colors=256)
sequence=[s.quantize(palette=palette,dither=Image.Dither.NONE) for s in stages]
sequence[0].save(folder/'Arch-Construction-Sequence.gif',save_all=True,append_images=sequence[1:],
                 duration=[1100]+[650]*9+[1600],loop=0,disposal=2)
paths=sorted((root/'Temp/ArchChunks').glob('frame-*.png'))
if len(paths)!=90: raise RuntimeError('Capture the 90 Unity arch motion frames first.')
with Image.open(paths[30]) as source:
    palette=source.convert('RGB').resize((960,720),Image.Resampling.LANCZOS).quantize(colors=256)
frames=[]
for path in paths:
    with Image.open(path) as source:
        frames.append(source.convert('RGB').resize((960,720),Image.Resampling.LANCZOS).quantize(palette=palette,dither=Image.Dither.NONE))
output=folder/'Arch-Chunks-Arrival.gif'
frames[0].save(output,save_all=True,append_images=frames[1:],duration=[30,30,40]*30,loop=0,disposal=1)
print(output)
