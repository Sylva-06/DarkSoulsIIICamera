"""Render the selected flame/shutter mark from native vector paths (Python + Pillow)."""
from pathlib import Path
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parent
COLOR='#B76B42'
# Curves traced for the approved flame/shutter silhouette. No background or edge specks.
PATHS=[[
('M',694,122),('C',617,239,649,287,746,357),('C',846,440,816,530,770,586),('C',733,638,750,693,819,729),('C',792,661,803,569,878,520),('C',832,602,891,681,933,750),('C',1022,901,887,1068,710,1140),('C',831,1020,756,915,653,818),('C',718,816,795,837,863,864),('C',793,714,652,736,531,823),('C',563,745,603,672,666,623),('C',535,666,416,732,333,685),('C',364,582,438,532,505,478),('C',542,440,556,415,540,379),('C',578,442,557,515,531,554),('C',669,467,609,402,600,342),('C',581,261,628,182,694,122),('Z',)],
[('M',409,624),('C',350,686,289,783,288,863),('C',286,934,331,1008,385,1055),('C',345,982,345,927,365,882),('C',409,917,472,947,530,958),('C',482,887,459,823,445,752),('C',495,704,560,666,607,646),('C',519,661,458,646,409,624),('Z',)],
[('M',416,1063),('C',481,1042,592,996,689,939),('L',582,1162),('C',506,1148,450,1111,416,1063),('Z',)]]
def sample(ops):
 p=[]
 for op in ops:
  if op[0] in ('M','L'):p.append((op[1],op[2]))
  elif op[0]=='C':
   x,y=p[-1]
   for i in range(1,101):
    t=i/100;u=1-t;p.append((u*u*u*x+3*u*u*t*op[1]+3*u*t*t*op[3]+t*t*t*op[5],u*u*u*y+3*u*u*t*op[2]+3*u*t*t*op[4]+t*t*t*op[6]))
 return p
svg='<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1280 1280"><title>Dark Souls III Camera</title><g fill="'+COLOR+'">'+''.join('<path d="'+' '.join(op[0]+' '.join(str(n) for n in op[1:]) for op in ops)+'"/>' for ops in PATHS)+'</g></svg>\n'
(ROOT/'camera-icon.svg').write_text(svg,encoding='utf8')
im=Image.new('RGBA',(2048,2048),(0,0,0,0));draw=ImageDraw.Draw(im)
for ops in PATHS:draw.polygon([(x*2048/1280,y*2048/1280) for x,y in sample(ops)],fill=COLOR)
im=im.resize((512,512),Image.Resampling.LANCZOS);im.save(ROOT/'camera-icon.png');im.save(ROOT/'camera-icon.ico',sizes=[(n,n) for n in (16,20,24,32,40,48,64,128,256)])
print('Generated transparent flame/shutter SVG, PNG and multi-size ICO.')
