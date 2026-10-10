"""Power icons (images/powers/<id>.png 64px + images/powers/big/<id>.png 256px), drawn from the sword sprites so
every power shows the sword it belongs to (user request 2026-10-08: no placeholder face).
  current_sword_<sword>.png : CurrentSwordPower, swapped when the current sword changes
  current_sword_onimaru_<kind>.png / onimaru_<kind>_power.png : Onimaru's katana + a stance glyph badge
                              (거합 iai / 난무 ranbu / 베기 giri / 퇴마 taima / 호위 goei)
  <sword>_*_power           : that sword's sprite
  skofnung_wound_power      : violet scar marks (망령 상흔)
  other (shared) powers     : three swords fanned over a magic circle
Re-run after adding a power:  python3 tools/gen_power_icons.py
"""
import os,re,glob,math
from PIL import Image, ImageDraw, ImageFilter
HERE=os.path.dirname(os.path.abspath(__file__)); A=os.path.join(HERE,'..','MagicSwordsman','images'); CODE=os.path.join(HERE,'..','MagicSwordsmanCode')
COL={'gram':(242,199,77),'ganjiang':(217,97,66),'moye':(140,199,250),'kusanagi':(115,204,140),'tyrfing':(255,140,51),'dainsleif':(191,38,51),
     'durandal':(250,242,217),'skofnung':(166,217,255),'onimaru':(235,235,242),'claiomhsolais':(255,255,191),'caladbolg':(102,230,242)}
PREFIX={'solais':'claiomhsolais','moyes':'moye','balmung':'gram','nuadas':'claiomhsolais','unstruck':'durandal','twin':'ganjiang'}
def sword(name,size,rot=-45):
    s=Image.open(f'{A}/swords/{name}.png').convert('RGBA'); s=s.rotate(rot,expand=True,resample=Image.BICUBIC); s=s.crop(s.getbbox())
    s.thumbnail((int(size*0.9),int(size*0.9)),Image.LANCZOS); return s
def glow(base,spr,pos,col,r):
    a=spr.getchannel('A').filter(ImageFilter.GaussianBlur(r)); g=Image.new('RGBA',spr.size,col+(0,)); g.putalpha(a.point(lambda v:min(255,int(v*1.6))))
    base.alpha_composite(g,pos); base.alpha_composite(spr,pos)
def sword_icon(name,S):
    im=Image.new('RGBA',(S,S)); s=sword(name,S); glow(im,s,((S-s.width)//2,(S-s.height)//2),COL[name],max(2,S//24)); return im
def fan_icon(S):
    im=Image.new('RGBA',(S,S)); d=ImageDraw.Draw(im); c=S/2; r=S*0.42; w=max(1,S//40)
    d.ellipse((c-r,c-r,c+r,c+r),outline=(190,140,255,200),width=w); d.ellipse((c-r*0.7,c-r*0.7,c+r*0.7,c+r*0.7),outline=(190,140,255,140),width=w)
    for n,rot in (('gram',38),('skofnung',0),('tyrfing',-38)):
        s=sword(n,int(S*0.95),rot)
        a=s.getchannel('A').point(lambda v:255 if v>60 else 0); s.putalpha(a)  # crisp edges, no smeared halo
        glow(im,s,((S-s.width)//2,(S-s.height)//2),(150,100,230),max(1,S//64))
    return im
def scar_icon(S):
    im=Image.new('RGBA',(S,S)); d=ImageDraw.Draw(im)
    for k in range(3):
        off=(k-1)*S*0.2; w=max(2,S//14)
        d.line((S*0.25+off,S*0.15,S*0.62+off,S*0.85),fill=(185,120,255,255),width=w)
    g=im.filter(ImageFilter.GaussianBlur(max(2,S//20))); out=Image.new('RGBA',(S,S)); out.alpha_composite(g); out.alpha_composite(g); out.alpha_composite(im); return out
STANCE={'iai':(255,226,140),'ranbu':(255,110,96),'giri':(255,170,70),'taima':(200,150,255),'goei':(120,190,255)}
def stance_glyph(kind,S):
    """A round badge in the lower-right corner with a simple glyph per Onimaru stance."""
    im=Image.new('RGBA',(S,S)); d=ImageDraw.Draw(im); col=STANCE[kind]
    r=S*0.25; cx=S-r-S*0.02; cy=S-r-S*0.02; w=max(2,round(S/22))
    d.ellipse((cx-r,cy-r,cx+r,cy+r),fill=(22,16,34,235),outline=col+(255,),width=max(1,round(S/40)))
    g=r*0.62
    if kind=='iai':     # one quick draw-cut
        d.line((cx-g,cy+g*0.55,cx+g,cy-g*0.55),fill=col+(255,),width=w)
        d.ellipse((cx+g-w,cy-g*0.55-w,cx+g+w,cy-g*0.55+w),fill=(255,255,255,255))
    elif kind=='ranbu': # a flurry: three short slashes
        for k in (-1,0,1):
            o=k*g*0.62; d.line((cx-g*0.55+o,cy+g*0.75,cx+g*0.35+o,cy-g*0.75),fill=col+(255,),width=max(1,w-1))
    elif kind=='giri':  # a wide sweep
        d.arc((cx-g*1.05,cy-g*0.9,cx+g*1.05,cy+g*1.2),200,340,fill=col+(255,),width=w)
        d.line((cx-g,cy+g*0.15,cx+g,cy+g*0.15),fill=col+(170,),width=max(1,w//2))
    elif kind=='taima': # an exorcism talisman (ofuda)
        d.rectangle((cx-g*0.42,cy-g*0.85,cx+g*0.42,cy+g*0.85),fill=(245,236,210,255),outline=col+(255,),width=max(1,w//2))
        for k in (-0.45,0,0.45): d.line((cx-g*0.2,cy+g*k,cx+g*0.2,cy+g*k),fill=(190,40,60,255),width=max(1,w//2))
    elif kind=='goei':  # a guard: shield
        d.polygon([(cx-g*0.8,cy-g*0.7),(cx+g*0.8,cy-g*0.7),(cx+g*0.7,cy+g*0.2),(cx,cy+g*0.95),(cx-g*0.7,cy+g*0.2)],fill=col+(255,))
        d.line((cx,cy-g*0.6,cx,cy+g*0.75),fill=(22,16,34,255),width=max(1,w//2))
    return im
def onimaru_stance_icon(kind,S):
    im=sword_icon('onimaru',S); im.alpha_composite(stance_glyph(kind,S)); return im
def save(img_fn,fn):
    for S,sub in ((64,''),(256,'big/')):
        img_fn(S).save(f'{A}/powers/{sub}{fn}.png')
ids=set()
for f in glob.glob(f'{CODE}/**/*.cs',recursive=True):
    for m in re.finditer(r'class (\w+Power)\b[^:{]*:\s*(MagicSwordsmanPower|OnimaruKindPower)',open(f).read()):
        if m.group(1)!='OnimaruKindPower': ids.add(re.sub(r'(?<!^)([A-Z])',r'_\1',m.group(1)).lower())
for sw in COL: save(lambda S,sw=sw: sword_icon(sw,S),f'current_sword_{sw}')
for kind in STANCE:
    save(lambda S,kind=kind: onimaru_stance_icon(kind,S),f'current_sword_onimaru_{kind}')
for i in sorted(ids):
    head=i.split('_')[0]; sw=PREFIX.get(head,head)
    if i=='skofnung_wound_power': save(scar_icon,i)
    elif i=='current_sword_power': save(lambda S: sword_icon('gram',S),i)
    elif re.fullmatch(r'onimaru_(\w+)_power',i) and i.split('_')[1] in STANCE: save(lambda S,k=i.split('_')[1]: onimaru_stance_icon(k,S),i)
    elif sw in COL: save(lambda S,sw=sw: sword_icon(sw,S),i)
    else: save(fan_icon,i)
    print(i, sw if sw in COL else '-')
