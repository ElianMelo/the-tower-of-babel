"""Generate refracted wave-light density; the Unity shader supplies color/motion."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter

root = Path(__file__).resolve().parents[2]
size = 512
samples = 1536
rng = np.random.default_rng(731)
v, u = np.mgrid[0:samples, 0:samples].astype(np.float32) / samples
sx = np.zeros_like(u)
sy = np.zeros_like(v)
# Integer wave vectors make the height field and its refraction tile seamlessly.
# Accumulating refracted photon positions produces actual focusing bands rather
# than polygon-cell outlines. The shader deforms two samples of this field.
for _ in range(18):
    kx, ky = rng.integers(-10, 11, 2)
    if abs(kx)+abs(ky) < 4:
        kx += 5
    amplitude = .00125 * rng.uniform(.65, 1.3)
    phase = 2*np.pi*(kx*u+ky*v) + rng.uniform(0, 2*np.pi)
    slope = amplitude * 2*np.pi * np.cos(phase)
    sx += slope*kx
    sy += slope*ky
x = ((u + sx*.10) % 1 * size).ravel()
y = ((v + sy*.10) % 1 * size).ravel()
ix, iy = np.floor(x).astype(np.int32), np.floor(y).astype(np.int32)
fx, fy = x-ix, y-iy
density = np.zeros(size*size, np.float32)
for dx, dy, weight in ((0,0,(1-fx)*(1-fy)), (1,0,fx*(1-fy)),
                       (0,1,(1-fx)*fy), (1,1,fx*fy)):
    np.add.at(density, ((iy+dy)%size)*size+(ix+dx)%size, weight)
density = density.reshape(size,size) / (samples/size)**2
# Periodic blur softens the water's light falloff without introducing tile seams.
for _ in range(2):
    density = (density*4 + np.roll(density,1,0) + np.roll(density,-1,0)
               + np.roll(density,1,1) + np.roll(density,-1,1)) / 8
field = np.clip((density-.45)/(np.percentile(density,99.5)-.45), 0, 1)**.85
image = Image.fromarray(np.uint8(field*255)).convert('RGB')
path = root/'Assets/_Project/Models/Buildings/FrutigerAero/Aero_WaterCaustics.png'
image.save(path)
print(path)
