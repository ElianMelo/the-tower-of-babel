"""Generate tileable technical flow and ripple fields; no external image assets."""
from pathlib import Path
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parents[2]
out=root/'Assets/_Project/Models/Buildings/FrutigerAero'
size=512
y,x=np.mgrid[:size,:size].astype(np.float32)/size
rng=np.random.default_rng(7814)
height=np.zeros_like(x);gx=np.zeros_like(x);gy=np.zeros_like(x)
for _ in range(18):
    kx,ky=rng.integers(-6,7,2)
    if kx==ky==0:kx=2
    amplitude=rng.uniform(.35,1)/(kx*kx+ky*ky)
    phase=(kx*x+ky*y)*2*np.pi+rng.uniform(0,2*np.pi)
    height+=amplitude*np.sin(phase)
    gx+=amplitude*kx*np.cos(phase)
    gy+=amplitude*ky*np.cos(phase)
scale=max(float(np.percentile(np.sqrt(gx*gx+gy*gy),98)),.001)
flow=np.stack((-gy,gx),axis=-1)/scale
flow=np.clip(flow,-1,1)*.8
noise=(height-height.min())/(height.max()-height.min())
rgba=np.dstack((flow*.5+.5,noise,noise))
Image.fromarray(np.uint8(np.clip(rgba,0,1)*255)).save(out/'Aero_AquaticFlow.png')
normal=np.dstack((-gx/scale*.65,-gy/scale*.65,np.ones_like(x)))
normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
Image.fromarray(np.uint8(np.clip(normal*.5+.5,0,1)*255)).save(out/'Aero_AquaticRipple_Normal.png')
print('Generated 512-square seamless RG flow / B phase / A height field and ripple normal.')
