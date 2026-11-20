"""Rebuild the camera mark as SVG, PNG and multi-size ICO (Python + Pillow)."""
from pathlib import Path
import math
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
GRAY = '#C7CCD1'
EMBER = '#B76B42'
CORNER = [('M',48,288),('L',48,128),('C',48,72,72,48,128,48),
          ('L',232,48),('L',132,116),('C',86,162,77,242,64,314),
          ('C',55,307,48,299,48,288),('Z',)]

def corner_points():
    points = []
    for op in CORNER:
        if op[0] in ('M','L'):
            points.append((op[1],op[2]))
        elif op[0] == 'C':
            x,y = points[-1]
            for step in range(1,81):
                t=step/80; u=1-t
                points.append((u**3*x+3*u*u*t*op[1]+3*u*t*t*op[3]+t**3*op[5],
                               u**3*y+3*u*u*t*op[2]+3*u*t*t*op[4]+t**3*op[6]))
    return points

def ring_points():
    angles=[math.radians(94+352*i/720) for i in range(721)]
    return ([(256+139*math.cos(a),256+139*math.sin(a)) for a in angles]
            +[(256+108*math.cos(a),256+108*math.sin(a)) for a in reversed(angles)])

corner=corner_points(); ring=ring_points()
path=' '.join(op[0]+' '.join(str(n) for n in op[1:]) for op in CORNER)
ring_path='M'+' L'.join(f'{x:.4f},{y:.4f}' for x,y in ring)+' Z'
svg=f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">
  <title>Dark Souls III Camera</title>
  <g fill="{GRAY}"><path d="{path}"/><path d="{path}" transform="rotate(180 256 256)"/></g>
  <path fill="{EMBER}" d="{ring_path}"/>
</svg>
'''
(ROOT/'camera-icon.svg').write_text(svg,encoding='utf-8')
scale=4
image=Image.new('RGBA',(512*scale,512*scale),(0,0,0,0))
draw=ImageDraw.Draw(image)
for points,color in [(corner,GRAY), ([(512-x,512-y) for x,y in corner],GRAY),(ring,EMBER)]:
    draw.polygon([(round(x*scale),round(y*scale)) for x,y in points],fill=color)
image=image.resize((512,512),Image.Resampling.LANCZOS)
image.save(ROOT/'camera-icon.png')
image.save(ROOT/'camera-icon.ico',sizes=[(n,n) for n in (16,20,24,32,40,48,64,128,256)])
print('Generated SVG, transparent 512px PNG and nine-size ICO.')
