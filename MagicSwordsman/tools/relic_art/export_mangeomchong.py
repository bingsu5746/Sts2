from rembg import remove, new_session
from PIL import Image, ImageFilter
import numpy as np, cv2
R='/home/user/Sts2/MagicSwordsman/MagicSwordsman/images/relics/'
ses=new_session('isnet-general-use')
for src,name in (('final_closed.png','mangeomchong'),('final_open.png','opened_mangeomchong')):
    rgb=Image.open(src).convert('RGB'); cut=remove(rgb,session=ses)
    a=np.array(cut); m=(a[...,3]>40).astype(np.uint8)*255
    h,w=m.shape; mask=np.zeros((h+2,w+2),np.uint8); ff=m.copy(); cv2.floodFill(ff,mask,(0,0),128)
    filled=(ff!=128); alpha=np.where(filled,255,0).astype(np.uint8)
    cut=Image.fromarray(np.dstack([np.array(rgb),alpha])); cut=cut.crop(cut.getbbox())
    for size,margin,path in ((256,10,R+'big/'+name+'.png'),(94,4,R+name+'.png')):
        c=cut.copy(); sc=(size-2*margin)/max(c.size); c=c.resize((round(c.width*sc),round(c.height*sc)),Image.LANCZOS)
        o=Image.new('RGBA',(size,size),(0,0,0,0)); o.alpha_composite(c,((size-c.width)//2,(size-c.height)//2)); o.save(path)
        if size==94:
            al=o.getchannel('A').point(lambda v:255 if v>30 else 0).filter(ImageFilter.MaxFilter(5))
            ol=Image.new('RGBA',o.size,(255,255,255,0)); ol.putalpha(al); ol.save(R+name+'_outline.png')
s=Image.new('RGBA',(256*2+94*2+40,256),(30,30,36,255))
s.alpha_composite(Image.open(R+'big/mangeomchong.png'),(0,0)); s.alpha_composite(Image.open(R+'big/opened_mangeomchong.png'),(256,0))
s.alpha_composite(Image.open(R+'mangeomchong.png'),(532,80)); s.alpha_composite(Image.open(R+'opened_mangeomchong.png'),(640,80))
s.convert('RGB').save('icons.jpg')
