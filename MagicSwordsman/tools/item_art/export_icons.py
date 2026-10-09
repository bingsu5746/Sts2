import sys,glob,os
from rembg import remove, new_session
from PIL import Image, ImageFilter
import numpy as np, cv2
I='/home/user/Sts2/MagicSwordsman/MagicSwordsman/images/'
ses=new_session('isnet-general-use')
def cutout(path):
    rgb=Image.open(path).convert('RGB'); a=np.array(remove(rgb,session=ses))
    m=(a[...,3]>40).astype(np.uint8)*255; h,w=m.shape; mask=np.zeros((h+2,w+2),np.uint8); ff=m.copy(); cv2.floodFill(ff,mask,(0,0),128)
    alpha=np.where(ff!=128,np.maximum(a[...,3],0),0).astype(np.uint8)
    alpha=np.where((ff!=128)&(a[...,3]<=40),255,alpha)  # fill enclosed holes (glass etc.)
    c=Image.fromarray(np.dstack([np.array(rgb),alpha])); return c.crop(c.getbbox())
def fit(c,size,margin):
    sc=(size-2*margin)/max(c.size); c=c.resize((max(1,round(c.width*sc)),max(1,round(c.height*sc))),Image.LANCZOS)
    o=Image.new('RGBA',(size,size),(0,0,0,0)); o.alpha_composite(c,((size-c.width)//2,(size-c.height)//2)); return o
def outline(o,grow):
    al=o.getchannel('A').point(lambda v:255 if v>30 else 0).filter(ImageFilter.MaxFilter(grow))
    ol=Image.new('RGBA',o.size,(255,255,255,0)); ol.putalpha(al); return ol
for f in sys.argv[1:]:
    n=os.path.basename(f)[:-5]; kind,name=n[0],n[2:]; c=cutout(f)
    if kind=='r':
        fit(c,256,10).save(I+f'relics/big/{name}.png'); o=fit(c,94,4); o.save(I+f'relics/{name}.png'); outline(o,5).save(I+f'relics/{name}_outline.png')
    else:
        o=fit(c,256,12); o.save(I+f'potions/{name}.png'); outline(o,9).save(I+f'potions/outline/{name}.png')
    print('ok',n)
