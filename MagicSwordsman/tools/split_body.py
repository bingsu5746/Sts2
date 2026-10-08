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
PARTS={  # name: (polygon in source px, pivot = joint the part rotates around)
 'head':([(168,0),(298,0),(298,118),(268,148),(232,166),(200,150),(170,112)],(230,150)),
 'upper_r':([(335,200),(405,188),(419,262),(413,360),(348,362),(337,282)],(372,215)),
 'fore_r':([(348,350),(415,350),(419,470),(393,548),(340,548),(338,470)],(382,358)),
 'upper_l':([(84,212),(162,198),(160,300),(141,394),(78,394),(83,300)],(128,215)),
 'fore_l':([(58,384),(143,384),(131,470),(119,522),(113,548),(48,548),(52,470)],(100,388)),
 'amulet':([(24,540),(116,540),(110,728),(26,728)],(80,546)),
 'coat_l':([(70,470),(168,470),(162,850),(100,852),(64,785)],(118,470)),
 'coat_r':([(292,458),(412,458),(446,780),(422,834),(300,832)],(352,460)),
}
ARMS=('upper_l','fore_l','amulet','upper_r','fore_r')
# cut out but not exported: the pendant that hung from his left hand looked like a held blade; his palms now carry
# magic circles instead (tools/gen_combat_scene.py HandL/HandR, user request 2026-10-08)
DISCARD=('amulet',)
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
for n in ARMS: ImageDraw.Draw(armmask).polygon(PARTS[n][0],fill=255)
for name,(poly,piv) in PARTS.items():
    m=mask(poly)
    if name in ('upper_l','upper_r'):  # forearm/amulet own their pixels; the upper arm stops at the elbow
        own={'upper_l':('fore_l','amulet'),'upper_r':('fore_r',)}[name]
        sub=Image.new('L',(W,H),0)
        for o in own: ImageDraw.Draw(sub).polygon(PARTS[o][0],fill=255)
        m=Image.fromarray(np.where(np.array(sub)>0,0,np.array(m)).astype('uint8'))
    if name=='fore_l':
        sub=Image.new('L',(W,H),0); ImageDraw.Draw(sub).polygon(PARTS['amulet'][0],fill=255)
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
outside=arms&(((xx<150)|(xx>345))&(yy<740))
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
