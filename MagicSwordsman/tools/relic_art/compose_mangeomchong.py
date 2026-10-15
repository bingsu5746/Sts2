import sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import numpy as np
SW='/home/user/Sts2/MagicSwordsman/MagicSwordsman/images/swords/'
base=Image.open('raw/gate0.webp').convert('RGBA').resize((768,768),Image.LANCZOS); W,H=base.size
def sword(name,h,rot,tip_down=True):
    s=Image.open(SW+name+'.png').convert('RGBA'); s=s.crop(s.getbbox())
    if tip_down: s=s.rotate(180)
    sc=h/s.height; s=s.resize((max(1,round(s.width*sc)),h),Image.LANCZOS)
    return s.rotate(rot,expand=True,resample=Image.BICUBIC)
def stick(img,name,cx,ground,h,rot,sink=0.22):
    s=sword(name,h,rot,True); w,hh=s.size
    cut=s.crop((0,0,w,int(hh*(1-sink))))
    sh=Image.new('RGBA',img.size,(0,0,0,0)); ImageDraw.Draw(sh).ellipse((cx-22,ground-6,cx+22,ground+6),fill=(20,16,20,140))
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(3)))
    img.alpha_composite(cut,(cx-w//2,ground-cut.height))
def plaque(img,cy=118):
    f=ImageFont.truetype('/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc',58)
    pl=Image.new('RGBA',img.size,(0,0,0,0)); d=ImageDraw.Draw(pl)
    x0,x1=W//2-140,W//2+140; y0,y1=cy-40,cy+40
    for cx in (x0+50,x1-50): d.line([(cx,y0+4),(W//2,y0-46)],fill=(28,26,30,255),width=5)
    d.ellipse((W//2-7,y0-53,W//2+7,y0-39),fill=(60,56,60,255),outline=(20,18,20,255),width=2)
    d.rounded_rectangle((x0+6,y0+9,x1+6,y1+9),8,fill=(0,0,0,130))
    d.rounded_rectangle((x0,y0,x1,y1),8,fill=(56,34,24,255),outline=(18,11,8,255),width=7)
    rng=np.random.default_rng(3)
    for i in range(24):
        yy=int(rng.integers(y0+10,y1-10)); d.line([(x0+10,yy),(x1-10,yy+int(rng.integers(-3,4)))],fill=(42,26,19,140),width=int(rng.integers(1,3)))
    d.rounded_rectangle((x0+13,y0+13,x1-13,y1-13),5,outline=(176,132,62,255),width=3)
    txt='萬劍塚'; bb=d.textbbox((0,0),txt,font=f); tw,th=bb[2]-bb[0],bb[3]-bb[1]
    tx,ty=(x0+x1-tw)//2-bb[0],(y0+y1-th)//2-bb[1]
    d.text((tx+2,ty+3),txt,font=f,fill=(18,10,6,210)); d.text((tx,ty),txt,font=f,fill=(222,178,90,255))
    img.alpha_composite(pl.filter(ImageFilter.GaussianBlur(0.5)))
def glow(img,cx,cy,rx,ry,col,a):
    g=Image.new('RGBA',img.size,(0,0,0,0)); ImageDraw.Draw(g).ellipse((cx-rx,cy-ry,cx+rx,cy+ry),fill=col+(a,))
    img.alpha_composite(g.filter(ImageFilter.GaussianBlur(max(rx,ry)*0.45)))
mode=sys.argv[1]
img=base.copy()
a=np.array(img).astype(float); r,g,b=a[...,0],a[...,1],a[...,2]
yy,xx=np.mgrid[0:H,0:W]
# the warm light in the door gap (bright, orange-ish) -> recolour to violet
gap=(xx>338)&(xx<428)&(yy>110)&(yy<660)&(r>150)&(r>b+25)
lum=(r+g+b)/3
if mode=='open':
    col=np.stack([0.75*lum+40,0.45*lum+20,lum*1.0+55],-1)
else:
    col=np.stack([0.5*lum,0.33*lum,0.75*lum+20],-1)
a[gap,:3]=col[gap]
img=Image.fromarray(a.clip(0,255).astype('uint8'))
if mode=='open':
    glow(img,383,380,90,270,(175,115,255),170)
    for n,x,y,h,rot in (('gram',366,215,190,5),('durandal',401,225,185,-5),('skofnung',383,180,215,0)):
        s=sword(n,h,rot,False); glow(img,x,y+h//2,26,h//2,(190,140,255),110); img.alpha_composite(s,(x-s.width//2,y))
    glow(img,383,420,260,300,(160,100,255),60)
else:
    glow(img,383,380,50,250,(150,90,240),90)
for n,x,gr,h,rot in (('tyrfing',150,730,250,-14),('onimaru',640,728,260,13),('dainsleif',245,742,210,7),('kusanagi',540,742,220,-8)):
    stick(img,n,x,gr,h,rot)
plaque(img)
img.convert('RGB').save(f'final_{mode}.png')
