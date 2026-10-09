import sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops
import numpy as np
SW='/home/user/Sts2/MagicSwordsman/MagicSwordsman/images/swords/'
base=Image.open('raw2/closed0.webp').convert('RGBA'); W,H=base.size
def sword(name,h,rot,tip_down=True):
    s=Image.open(SW+name+'.png').convert('RGBA'); s=s.crop(s.getbbox())
    if tip_down: s=s.rotate(180)
    sc=h/s.height; s=s.resize((max(1,round(s.width*sc)),h),Image.LANCZOS)
    if tip_down: pass
    return s.rotate(rot,expand=True,resample=Image.BICUBIC)
def stick(img,name,cx,ground,h,rot,sink=0.22):
    s=sword(name,h,rot,True); w,hh=s.size
    cut=s.crop((0,0,w,int(hh*(1-sink))))  # the buried part is cut off
    # a darker ground line where it enters the earth
    img.alpha_composite(cut,(cx-w//2,ground-cut.height))
def plaque(img,y0=96):
    f=ImageFont.truetype('/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc',60)
    pl=Image.new('RGBA',img.size,(0,0,0,0)); d=ImageDraw.Draw(pl)
    x0,x1,y1=W//2-150,W//2+150,y0+88
    for cx in (x0+42,x1-42): d.line([(cx,y0+4),(cx,y0-34)],fill=(28,26,30,255),width=6)
    d.rounded_rectangle((x0+5,y0+8,x1+5,y1+8),9,fill=(0,0,0,120))
    d.rounded_rectangle((x0,y0,x1,y1),9,fill=(58,36,26,255),outline=(20,12,8,255),width=7)
    # wood grain + worn edges
    rng=np.random.default_rng(3)
    for i in range(26):
        yy=rng.integers(y0+10,y1-10); d.line([(x0+10,yy),(x1-10,yy+rng.integers(-3,4))],fill=(44,27,20,140),width=int(rng.integers(1,3)))
    d.rounded_rectangle((x0+13,y0+13,x1-13,y1-13),5,outline=(170,128,60,255),width=3)
    txt='萬劍塚'; bb=d.textbbox((0,0),txt,font=f); tw,th=bb[2]-bb[0],bb[3]-bb[1]
    d.text(((x0+x1-tw)//2-bb[0]+2,(y0+y1-th)//2-bb[1]+3),txt,font=f,fill=(20,12,8,200))
    d.text(((x0+x1-tw)//2-bb[0],(y0+y1-th)//2-bb[1]),txt,font=f,fill=(214,170,84,255))
    pl=pl.filter(ImageFilter.GaussianBlur(0.6))
    img.alpha_composite(pl)
def glow(img,cx,cy,rx,ry,col,a):
    g=Image.new('RGBA',img.size,(0,0,0,0)); d=ImageDraw.Draw(g)
    d.ellipse((cx-rx,cy-ry,cx+rx,cy+ry),fill=col+(a,)); g=g.filter(ImageFilter.GaussianBlur(max(rx,ry)*0.45))
    img.alpha_composite(g)
mode=sys.argv[1]
img=base.copy()
# remove the AI signature in the bottom-right corner
ImageDraw.Draw(img).rectangle((660,700,768,768),fill=img.getpixel((740,600)))
if mode=='open':
    # the doors swing inward: the doorway becomes a dark violet glow with swords floating inside
    a=np.array(img).astype(int); r,g,b=a[...,0],a[...,1],a[...,2]
    yy,xx=np.mgrid[0:H,0:W]
    cx,top=W//2,175; rad=190
    inside=((xx-cx)**2/(195**2)+(yy-(top+rad))**2/(rad**2)<=1)|((abs(xx-cx)<195)&(yy>top+rad)&(yy<700))
    leaf=(abs(xx-cx)>150)  # the two door leaves, seen edge-on at the sides
    hole=inside&~leaf
    t=np.clip(1-np.hypot((xx-cx)/190,(yy-430)/260),0,1)
    a[hole,0]=(30+120*t[hole]).astype(int); a[hole,1]=(16+60*t[hole]).astype(int); a[hole,2]=(48+170*t[hole]).astype(int)
    img=Image.fromarray(a.clip(0,255).astype('uint8'))
    for n,x,y,h,rot in (('gram',cx-70,250,250,12),('durandal',cx+70,230,260,-10),('skofnung',cx,200,280,0)):
        s=sword(n,h,rot,False); 
        sh=s.copy(); sh.putalpha(s.getchannel('A').point(lambda v:v*0.9))
        glow(img,x,y+h//2,70,h//2,(170,110,255),120); img.alpha_composite(s,(x-s.width//2,y))
    glow(img,cx,450,230,260,(160,100,255),70)
else:
    for wd,bl,col in ((46,22,(170,110,255,170)),(10,4,(200,150,255,230)),(3,1,(245,230,255,255))):
        g=Image.new('RGBA',img.size,(0,0,0,0)); ImageDraw.Draw(g).line([(W//2,190),(W//2,690)],fill=col,width=wd)
        img.alpha_composite(g.filter(ImageFilter.GaussianBlur(bl)))  # light from the gap between the doors
for n,x,h,rot in (('tyrfing',140,300,-16),('onimaru',630,310,14),('dainsleif',245,250,8),('kusanagi',530,260,-9)):
    stick(img,n,x,712,h,rot)
plaque(img)
img.convert('RGB').save(f'final_{mode}.png')
