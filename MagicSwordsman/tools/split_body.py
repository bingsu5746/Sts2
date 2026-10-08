"""Splits the full-body Ensifer art into animation parts (body, arm_l, arm_r, coat_l, coat_r) with Godot pivots.
Input: a background-removed full-body PNG (coords below are for that image, 499x980). Output: MagicSwordsman/images/character/parts/*.png
and parts.json (pivot + offset per part, in output pixels). The base 'body' has the moving parts inpainted out so
no hole shows when an arm or coat flap swings. Usage: python3 tools/split_body.py body_cut.png
"""
import sys,os,json
import numpy as np,cv2
from PIL import Image,ImageDraw
SRC=sys.argv[1]; OUT=os.path.join(os.path.dirname(__file__),'..','MagicSwordsman','images','character','parts')
os.makedirs(OUT,exist_ok=True)
S=800/980  # output scale (parts are 800 px tall; the scene shows them at 0.5)
PARTS={  # name: (polygon, pivot)
 'arm_r':([(330,205),(400,185),(418,300),(414,420),(404,535),(342,535),(336,420),(330,300)],(372,215)),
 'arm_l':([(88,212),(162,198),(166,300),(152,420),(118,495),(112,725),(30,725),(28,505),(72,420),(88,320)],(128,215)),
 'coat_l':([(70,470),(168,470),(162,850),(100,852),(64,785)],(118,470)),
 'coat_r':([(292,458),(412,458),(446,780),(422,834),(300,832)],(352,460)),
}
im=Image.open(SRC).convert('RGBA'); W,H=im.size
# drop the glowing ground circle below the boots
a=np.array(im); yy,xx=np.mgrid[0:H,0:W]
boots=((xx>140)&(xx<208))|((xx>292)&(xx<366))
a[(yy>922)&~boots,3]=0
a[yy>952,3]=0
purple=(a[:,:,2].astype(int)-a[:,:,1].astype(int)>35)&(a[:,:,2]>70)
a[(yy>895)&purple,3]=0
im=Image.fromarray(a)
def mask(poly):
    m=Image.new('L',(W,H),0); ImageDraw.Draw(m).polygon(poly,fill=255); return m
meta={}
union=Image.new('L',(W,H),0)
armmask=Image.new('L',(W,H),0)
for n in ('arm_l','arm_r'): ImageDraw.Draw(armmask).polygon(PARTS[n][0],fill=255)
for name,(poly,piv) in PARTS.items():
    m=mask(poly)
    if name.startswith('coat'):  # arms are drawn over the coat: never copy arm pixels into a coat flap
        m=Image.fromarray(np.where(np.array(armmask)>0,0,np.array(m)).astype('uint8'))
    part=Image.new('RGBA',(W,H),(0,0,0,0)); part.paste(im,(0,0),m)
    part.putalpha(Image.fromarray(np.minimum(np.array(part.getchannel('A')),np.array(m))))
    bb=part.getbbox(); crop=part.crop(bb)
    crop=crop.resize((max(1,round(crop.width*S)),max(1,round(crop.height*S))),Image.LANCZOS); crop.save(f'{OUT}/{name}.png')
    meta[name]={'bbox':[round(v*S) for v in bb],'pivot':[round(piv[0]*S),round(piv[1]*S)]}
    union=Image.fromarray(np.maximum(np.array(union),np.array(m)))
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
for n in ('arm_l','arm_r'): arms|=np.array(mask(PARTS[n][0]))>0
outside=arms&(((xx<150)|(xx>345))&(yy<740))
alpha[outside]=0
base.putalpha(Image.fromarray(alpha))
bb=(0,0,W,H)
base=base.resize((round(W*S),round(H*S)),Image.LANCZOS); base.save(f'{OUT}/body.png')
meta['body']={'bbox':[0,0,round(W*S),round(H*S)],'pivot':[round(W*S/2),round(H*S/2)]}
meta['_size']=[round(W*S),round(H*S)]
json.dump(meta,open(f'{OUT}/parts.json','w'),indent=1)
print(json.dumps(meta))
