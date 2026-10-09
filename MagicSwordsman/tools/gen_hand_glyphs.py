"""images/vfx/glyph_<sword>.png: the centre layer of Ensifer's palm circle, one per sword (user request 2026-10-09:
"마법진도 검마다 다르게"). Each = a star polygon with its own point count and rune ring + the sword's own silhouette
in the middle, white on transparent (premultiplied); tinted in game with the sword's colour (SwordVisuals).
Re-run after changing a sword sprite:  python3 tools/gen_hand_glyphs.py"""
import os,math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
HERE=os.path.dirname(os.path.abspath(__file__)); A=os.path.join(HERE,'..','MagicSwordsman','images')
# (star points, step, inner ring count) — chosen to read differently at a glance
SHAPE={'gram':(5,2,1),'ganjiang':(6,2,2),'moye':(6,2,2),'kusanagi':(8,3,1),'tyrfing':(7,3,1),'dainsleif':(9,4,1),
       'durandal':(4,1,2),'skofnung':(12,5,1),'onimaru':(3,1,2),'claiomhsolais':(16,7,1),'caladbolg':(10,3,2)}
S=512; c=S/2
def pm(img):
    a=np.array(img).astype(np.float32); a[...,:3]*=a[...,3:4]/255; return Image.fromarray(a.astype(np.uint8))
for name,(n,step,rings) in SHAPE.items():
    im=Image.new('RGBA',(S,S)); d=ImageDraw.Draw(im); r=200
    pts=[(c+r*math.cos(2*math.pi*k/n-math.pi/2),c+r*math.sin(2*math.pi*k/n-math.pi/2)) for k in range(n)]
    for k in range(n): d.line((pts[k],pts[(k+step)%n]),fill=(255,255,255,255),width=5)
    for k in range(rings): rr=r-k*26; d.ellipse((c-rr,c-rr,c+rr,c+rr),outline=(255,255,255,230),width=4)
    for x,y in pts: d.ellipse((x-10,y-10,x+10,y+10),outline=(255,255,255,255),width=3)
    # the sword itself as a silhouette in the middle
    sw=Image.open(f'{A}/swords/{name}.png').convert('RGBA'); sw.thumbnail((int(S*0.22),int(S*0.55)),Image.LANCZOS)
    sil=Image.new('RGBA',sw.size,(255,255,255,0)); sil.putalpha(sw.getchannel('A').point(lambda v:255 if v>90 else 0))
    im.alpha_composite(sil,((S-sw.width)//2,(S-sw.height)//2))
    g=im.filter(ImageFilter.GaussianBlur(6)); out=Image.new('RGBA',(S,S)); out.alpha_composite(g); out.alpha_composite(im)
    pm(out.resize((256,256),Image.LANCZOS)).save(f'{A}/vfx/glyph_{name}.png'); print(name)
