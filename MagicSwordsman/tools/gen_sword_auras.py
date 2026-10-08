"""images/swords/aura/<sword>.png: a soft white glow around each sword's silhouette (premultiplied, for an additive
sprite tinted violet behind the floating sword). Same canvas as the sword art plus PAD px on every side.
Re-run after changing a sword sprite:  python3 tools/gen_sword_auras.py"""
import os,glob
import numpy as np
from PIL import Image, ImageFilter
HERE=os.path.dirname(os.path.abspath(__file__)); D=os.path.join(HERE,'..','MagicSwordsman','images','swords'); PAD=60
os.makedirs(D+'/aura',exist_ok=True)
for f in glob.glob(D+'/*.png'):
    s=Image.open(f).convert('RGBA'); a=s.getchannel('A')
    big=Image.new('L',(s.width+2*PAD,s.height+2*PAD)); big.paste(a,(PAD,PAD))
    g=np.array(big.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(26))).astype(np.float32)/255
    g=np.clip(g*1.4,0,1)**1.3
    out=np.zeros(g.shape+(4,),np.uint8); out[...,:3]=(g*255)[...,None]; out[...,3]=(g*255)
    Image.fromarray(out).save(D+'/aura/'+os.path.basename(f)); print(os.path.basename(f))
