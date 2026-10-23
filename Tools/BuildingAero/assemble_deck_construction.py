"""Assemble floor/step stage sheets and incoming-piece animations from Unity renders."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parents[2]
folder=root/'Documentation/FrutigerAero/Buildings/Previews'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26)
paths_by_kind={}
for kind in ('Floor_Tile','Step_Tile'):
    stages=[]
    sheet=Image.new('RGB',(2560,1950),(143,191,219))
    for i in range(11):
        with Image.open(folder/f'{kind}-Stage-{i:02}.png') as source: stage=source.convert('RGB')
        stages.append(stage)
        tile=Image.new('RGB',(640,650),(143,191,219)); tile.paste(stage,(0,0))
        label='Blueprint' if i==0 else 'Complete' if i==10 else f'Random fill {i*10}%'
        ImageDraw.Draw(tile).text((320,612),f'{i:02}  {label}',fill=(20,65,90),font=font,anchor='mm')
        sheet.paste(tile,((i%4)*640,(i//4)*650))
    sheet.save(folder/f'{kind}-Construction-Stages.png')
    palette=stages[-1].quantize(colors=256)
    sequence=[s.quantize(palette=palette,dither=Image.Dither.NONE) for s in stages]
    sequence[0].save(folder/f'{kind}-Construction-Sequence.gif',save_all=True,append_images=sequence[1:],
                     duration=[1100]+[650]*9+[1600],loop=0,disposal=2)
    paths=sorted((root/f'Temp/{kind}Chunks').glob('frame-*.png')); paths_by_kind[kind]=paths
    if len(paths)!=90: raise RuntimeError(f'Capture the 90 Unity {kind} frames first.')
    with Image.open(paths[30]) as source: palette=source.convert('RGB').resize((960,720),Image.Resampling.LANCZOS).quantize(colors=256)
    frames=[]
    for path in paths:
        with Image.open(path) as source:
            frames.append(source.convert('RGB').resize((960,720),Image.Resampling.LANCZOS).quantize(palette=palette,dither=Image.Dither.NONE))
    frames[0].save(folder/f'{kind}-Chunks-Arrival.gif',save_all=True,append_images=frames[1:],duration=[30,30,40]*30,loop=0,disposal=1)
    print(folder/f'{kind}-Chunks-Arrival.gif')
combined=[]
for i in range(90):
    frame=Image.new('RGB',(1280,520),(143,191,219))
    for index,kind in enumerate(('Floor_Tile','Step_Tile')):
        with Image.open(paths_by_kind[kind][i]) as source:
            frame.paste(source.convert('RGB').resize((640,480),Image.Resampling.LANCZOS),(index*640,0))
        ImageDraw.Draw(frame).text((320+index*640,498),'Floor' if index==0 else 'Step',fill=(20,65,90),font=font,anchor='mm')
    combined.append(frame)
palette=combined[30].quantize(colors=256)
combined=[f.quantize(palette=palette,dither=Image.Dither.NONE) for f in combined]
output=folder/'Deck-Chunks-Arrival.gif'
combined[0].save(output,save_all=True,append_images=combined[1:],duration=[30,30,40]*30,loop=0,disposal=1)
print(output)
