"""Splits the full-body Ensifer art into animation parts with Godot pivots.
Input: the background-removed full-body PNG of the game-style Ensifer (side-on, facing right; 776x1216 — source:
AI Horde side-view i2 (img2img of the previous body, head turned toward the enemy), see docs/card-art-credits.md). Output: MagicSwordsman/images/character/parts/*.png
and parts.json (pivot + offset per part, in output pixels). The base 'body' has the moving parts inpainted out so no
hole shows when an arm or coat flap swings. The painted magic swirls of the source are removed (the rig draws its
own palm circles). Usage: python3 tools/split_body.py body_cut.png
"""
import sys,os,json
import numpy as np,cv2
from PIL import Image,ImageDraw
SRC=sys.argv[1]; OUT=os.path.join(os.path.dirname(__file__),'..','MagicSwordsman','images','character','parts')
os.makedirs(OUT,exist_ok=True)
S=800/1216  # output scale (parts are 800 px tall; the scene shows them at 0.5)
# R = the arm toward the enemy (screen right), L = the back arm (screen left)
PARTS={  # name: (polygon in source px, pivot = joint the part rotates around)
 'head':([(268,22),(432,22),(432,118),(402,172),(372,192),(330,198),(296,150),(268,100)],(352,192)),
 'upper_r':([(398,212),(452,212),(502,326),(532,418),(502,462),(458,420),(418,322),(398,268)],(428,240)),
 'fore_r':([(468,398),(560,416),(642,436),(732,466),(732,562),(640,562),(588,522),(518,482),(468,460)],(494,430)),
 'upper_l':([(204,214),(262,228),(252,330),(202,440),(150,482),(118,450),(168,330)],(230,250)),
 'fore_l':([(108,430),(192,452),(170,562),(122,642),(104,702),(14,702),(6,612),(58,520)],(154,462)),
 'coat_l':([(130,600),(238,520),(290,700),(262,900),(180,940),(128,800)],(248,540)),
 'coat_r':([(440,560),(560,560),(662,700),(652,842),(560,802),(470,762)],(452,562)),
}
ARMS=('upper_l','fore_l','upper_r','fore_r')
DISCARD=()
HANDS=[(4,596,134,712),(586,436,734,566)]  # glowing hands: keep their magenta glow
im=Image.open(SRC).convert('RGBA'); W,H=im.size
a=np.array(im); yy,xx=np.mgrid[0:H,0:W]
# painted magic swirls / wisps: magenta strokes outside the hands, and faint semi-transparent smoke
mag=(a[...,0].astype(int)>140)&(a[...,2].astype(int)>140)&(a[...,1].astype(int)<130)
keep=np.zeros((H,W),bool)
for x0,y0,x1,y1 in HANDS: keep[y0:y1,x0:x1]=True
# the face (pink make-up) and the chest gem are magenta too: keep them; every other magenta stroke is painted magic
for x0,y0,x1,y1 in ((280,40,440,210),(318,196,424,352)): keep[y0:y1,x0:x1]=True
a[mag&~keep,3]=0
a[a[...,3]<90,3]=0
# the painted grey smoke around the hands and coat hem: light, unsaturated pixels (the figure itself is dark)
rgb=a[...,:3].astype(int); light=(rgb.min(-1)>120)&((rgb.max(-1)-rgb.min(-1))<40)
a[light,3]=0
a[(yy>1120)&(xx>405),3]=0  # cast shadow beside the front boot
grey=(rgb.min(-1)>55)&((rgb.max(-1)-rgb.min(-1))<32)
for x0,y0,x1,y1 in ((560,420,776,680),(0,540,160,740),(420,740,700,880)):  # darker smoke near the hands and hem
    z=np.zeros((H,W),bool); z[y0:y1,x0:x1]=True; a[z&grey,3]=0
# ground shadow under the boots
a[(yy>1100)&((xx<280)|(xx>440)),3]=0
n,lab,st,_=cv2.connectedComponentsWithStats((a[...,3]>0).astype(np.uint8),8)
a[lab!=1+np.argmax(st[1:,4])]=0
im=Image.fromarray(a)
def mask(poly):
    m=Image.new('L',(W,H),0); ImageDraw.Draw(m).polygon(poly,fill=255); return m
meta={}
union=Image.new('L',(W,H),0)
armmask=Image.new('L',(W,H),0)
for n in ARMS: ImageDraw.Draw(armmask).polygon(PARTS[n][0],fill=255)
for name,(poly,piv) in PARTS.items():
    m=mask(poly)
    if name in ('upper_l','upper_r'):  # forearm/amulet own their pixels; the upper arm stops at the elbow
        own={'upper_l':('fore_l',),'upper_r':('fore_r',)}[name]
        sub=Image.new('L',(W,H),0)
        for o in own: ImageDraw.Draw(sub).polygon(PARTS[o][0],fill=255)
        m=Image.fromarray(np.where(np.array(sub)>0,0,np.array(m)).astype('uint8'))
    if name.startswith('coat'):  # arms are drawn over the coat: never copy arm pixels into a coat flap
        m=Image.fromarray(np.where(np.array(armmask)>0,0,np.array(m)).astype('uint8'))
    part=Image.new('RGBA',(W,H),(0,0,0,0)); part.paste(im,(0,0),m)
    part.putalpha(Image.fromarray(np.minimum(np.array(part.getchannel('A')),np.array(m))))
    union=Image.fromarray(np.maximum(np.array(union),np.array(m)))
    if name in DISCARD: continue
    bb=part.getbbox(); crop=part.crop(bb)
    crop=crop.resize((max(1,round(crop.width*S)),max(1,round(crop.height*S))),Image.LANCZOS); crop.save(f'{OUT}/{name}.png')
    meta[name]={'bbox':[round(v*S) for v in bb],'pivot':[round(piv[0]*S),round(piv[1]*S)]}
# base: inpaint colour under the moving parts (only inside the body silhouette), keep alpha of the original body
rgb=cv2.cvtColor(np.array(im.convert('RGB')),cv2.COLOR_RGB2BGR)
um=(np.array(union)>0).astype('uint8')*255
inner=cv2.dilate(um,np.ones((5,5),np.uint8))
filled=cv2.inpaint(rgb,inner,9,cv2.INPAINT_TELEA)
base=Image.fromarray(cv2.cvtColor(filled,cv2.COLOR_BGR2RGB)).convert('RGBA')
alpha=np.array(im.getchannel('A')).copy()
# shrink the silhouette where an arm hung outside the torso (those pixels belong to the arm only)
# pixels of an arm that hang outside the torso belong to the arm only (no copy left in the base)
arms=np.zeros((H,W),bool)
for n in ARMS: arms|=np.array(mask(PARTS[n][0]))>0
outside=arms&((xx<196)|(xx>468))
alpha[outside]=0
base.putalpha(Image.fromarray(alpha))
bb=(0,0,W,H)
base=base.resize((round(W*S),round(H*S)),Image.LANCZOS)
ba=np.array(base); n_,lab,st,_=cv2.connectedComponentsWithStats((ba[...,3]>10).astype(np.uint8),8)
ba[lab!=1+np.argmax(st[1:,4])]=0  # drop stray specks left around the removed parts
Image.fromarray(ba).save(f'{OUT}/body.png')
meta['body']={'bbox':[0,0,round(W*S),round(H*S)],'pivot':[round(W*S/2),round(H*S/2)]}
meta['_size']=[round(W*S),round(H*S)]
json.dump(meta,open(f'{OUT}/parts.json','w'),indent=1)
print(json.dumps(meta))
