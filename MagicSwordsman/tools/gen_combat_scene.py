"""Generates MagicSwordsman/scenes/magic_swordsman_combat.tscn: the split Ensifer rig + the motion library.

Rig (Visuals/Rig, shown at 0.5 scale): Body, Head, CoatL, CoatR, UpperL > ForeL > HandL, UpperR > ForeR > HandR, Sigil.
HandL/HandR: the palm magic circles he steers his swords with (no object in his hands; user request 2026-10-08):
a hexagram core + a rune ring facing the camera + two tilted orbit rings (gyroscope / 'higher-dimension' look).
The clips drive each hand group's scale + color; a second AnimationPlayer (HandSpin) spins the layers forever.
Joints: head, ul, fl, ur, fr, cl, cr (radians, + = clockwise on screen), plus root x/y/rot/scale and tint.
Sign guide (front view, enemies to the right): ur/fr negative = right arm swings out to the right / up;
ul/fl positive = left arm swings out to the left / up; cl positive / cr negative = coat flaps flare outward.
Clips: see MOTIONS below. The game triggers Attack / Cast / Hit / Dead; MotionDirector (C#) swaps those for a
variant from this library (by current sword, card type, damage...) and fires the extra ones (Summon_*, Swap_*,
Block_*, Power_*, Victory...). Re-run after editing:  python3 tools/gen_combat_scene.py
"""
import os,json,math
HERE=os.path.dirname(__file__)
OUT=os.path.join(HERE,'..','MagicSwordsman','scenes','magic_swordsman_combat.tscn')
PARTS=json.load(open(os.path.join(HERE,'..','MagicSwordsman','images','character','parts','parts.json')))
CW,CH=PARTS['_size']; CX,CY=CW/2,CH/2
CHEST=(380*800/1216-CX,318*800/1216-CY)  # the glowing gem on his chest (split_body.py source px)
REST_Y=-200
JOINTS={'head':'Visuals/Rig/Head','ul':'Visuals/Rig/UpperL','fl':'Visuals/Rig/UpperL/ForeL',
        'ur':'Visuals/Rig/UpperR','fr':'Visuals/Rig/UpperR/ForeR','cl':'Visuals/Rig/CoatL','cr':'Visuals/Rig/CoatR'}
SWORD_COL={'Gram':(0.95,0.78,0.30),'Ganjiang':(0.85,0.38,0.26),'Moye':(0.55,0.78,0.98),'Kusanagi':(0.45,0.80,0.55),'Tyrfing':(1.0,0.55,0.20),
 'Dainsleif':(0.75,0.15,0.20),'Durandal':(0.98,0.95,0.85),'Skofnung':(0.65,0.85,1.0),'Onimaru':(0.92,0.92,0.95),'ClaiomhSolais':(1.0,1.0,0.75),'Caladbolg':(0.40,0.90,0.95)}
PURPLE=(0.85,0.65,1.0)
HAND={'L':'Visuals/Rig/UpperL/ForeL/HandL','R':'Visuals/Rig/UpperR/ForeR/HandR'}
HAND_REST=(0.45,0.0)  # (scale, alpha) at rest: no circle; a gesture summons it and it fades out after
def V(x,y): return f'Vector2({x:.3f}, {y:.3f})'
def C(r,g,b,a=1.0): return f'Color({r:.3f}, {g:.3f}, {b:.3f}, {a:.3f})'

# ---------------------------------------------------------------- clip builder
class Clip:
    def __init__(s,name,length,loop=False): s.name=name; s.length=length; s.loop=loop; s.tr={}
    def key(s,track,t,val): s.tr.setdefault(track,[]).append((round(t,3),val)); return s
    def pose(s,t,**j):
        """Keyframe joints at time t. Keys: x,y (root offset), rot, sx, sy, tint=(r,g,b), and joint names."""
        for k,v in j.items():
            if k in JOINTS: s.key(JOINTS[k]+':rotation',t,f'{v:.4f}')
            elif k=='rot': s.key('Visuals:rotation',t,f'{v:.4f}')
        if 'x' in j or 'y' in j: s.key('Visuals:position',t,V(j.get('x',0),REST_Y+j.get('y',0)))
        if 'sx' in j or 'sy' in j: s.key('Visuals:scale',t,V(j.get('sx',1),j.get('sy',1)))
        if 'tint' in j: s.key('Visuals:modulate',t,C(*j['tint']))
        return s
    def fx(s,node,t,a,col=PURPLE,**kw):
        s.key(node+':modulate',t,C(*col,a))
        if 'scale' in kw: s.key(node+':scale',t,V(*kw['scale']))
        if 'rotation' in kw: s.key(node+':rotation',t,f'{kw["rotation"]:.4f}')
        if 'pos' in kw: s.key(node+':position',t,V(*kw['pos']))
        return s
    def hand(s,side,t,scale,a,col=PURPLE):
        """Palm magic circle(s): side 'L', 'R' or 'LR'."""
        # colour comes from the current sword at runtime (HandX/Tint, SwordVisuals); clips only drive size + alpha
        for h in side: s.key(HAND[h]+':scale',t,V(scale,scale)); s.key(HAND[h]+':modulate',t,C(1,1,1,a))
        return s
REST={**{JOINTS[j]+':rotation':'0.0000' for j in JOINTS},'Visuals:position':V(0,REST_Y),'Visuals:rotation':'0.0000',
      'Visuals:scale':V(1,1),'Visuals:modulate':C(1,1,1),'Circle:modulate':C(*PURPLE,0),
      'Shield:modulate':C(1,1,1,0),'Visuals/Rig/Sigil:modulate':C(1,1,1,0.4),'Visuals/Rig/Sigil:scale':V(1,1),
      **{HAND[h]+':scale':V(HAND_REST[0],HAND_REST[0]) for h in HAND},**{HAND[h]+':modulate':C(1,1,1,HAND_REST[1]) for h in HAND}}
def emit(clip,aid):
    tracks=dict(clip.tr)
    for k,v in REST.items():  # every clip resets what it does not animate
        if k not in tracks: tracks[k]=[(0.0,v)]
    out=[f'[sub_resource type="Animation" id="{aid}"]',f'resource_name = "{clip.name}"',f'length = {clip.length}']
    if clip.loop: out.append('loop_mode = 1')
    for i,(path,keys) in enumerate(tracks.items()):
        keys=sorted(dict(keys).items())
        out.append(f'''tracks/{i}/type = "value"
tracks/{i}/imported = false
tracks/{i}/enabled = true
tracks/{i}/path = NodePath("{path}")
tracks/{i}/interp = 2
tracks/{i}/loop_wrap = true
tracks/{i}/keys = {{
"times": PackedFloat32Array({', '.join(str(t) for t,_ in keys)}),
"transitions": PackedFloat32Array({', '.join('1' for _ in keys)}),
"update": 0,
"values": [{', '.join(v for _,v in keys)}]
}}''')
    return '\n'.join(out)+'\n'
R0=dict(x=0,y=0,rot=0,head=0,ul=0,fl=0,ur=0,fr=0,cl=0,cr=0)
def rest(c,t): c.hand('LR',t,*HAND_REST); return c.pose(t,**R0,sx=1,sy=1,tint=(1,1,1))
M=[]

# ---------------------------------------------------------------- idle family (looping / fidgets)
c=Clip('idle',3.0,loop=True)
for t,b in ((0,0),(1.5,1),(3.0,0)):
    c.pose(t,y=-4*b,sx=1+0.006*b,sy=1+0.014*b,head=0.02*b,ul=0.035*b,ur=-0.035*b,fl=0.02*b,fr=-0.02*b)
c.pose(0.9,cl=0.03,cr=-0.012); c.pose(2.1,cl=-0.012,cr=-0.035); c.pose(0,cl=0,cr=0); c.pose(3.0,cl=0,cr=0)
c.fx('Visuals/Rig/Sigil',0,0.35,(1,1,1)).fx('Visuals/Rig/Sigil',1.5,0.9,(1,1,1)).fx('Visuals/Rig/Sigil',3.0,0.35,(1,1,1)); M.append(c)
c=Clip('Idle_Look',2.4); rest(c,0); c.pose(0.5,head=0.12,y=-2); c.pose(1.3,head=0.12,y=-2); c.pose(1.8,head=-0.05); rest(c,2.4); M.append(c)
c=Clip('Idle_Flex',2.2); rest(c,0); c.pose(0.5,fr=-0.5,ur=-0.15); c.pose(0.8,fr=-0.42,ur=-0.15); c.pose(1.1,fr=-0.5,ur=-0.15); rest(c,2.2)
c.fx('Visuals/Rig/Sigil',0.8,0.9,(1,1,1)); M.append(c)
# Idle_Weave: raises the left palm and turns the circle over like a dial, the right hand adjusting it
c=Clip('Idle_Weave',2.6); rest(c,0); c.pose(0.5,ul=0.25,fl=0.9,ur=-0.15,fr=-0.7,head=0.08); c.hand('L',0.5,0.55,0.8)
c.pose(1.1,ul=0.3,fl=1.0,ur=-0.2,fr=-0.95); c.hand('L',1.1,0.55,0.9)
c.pose(1.7,ul=0.25,fl=0.85,ur=-0.12,fr=-0.6); c.hand('L',1.9,0.55,0.0); rest(c,2.6); M.append(c)
c=Clip('Idle_Breath',3.0); rest(c,0); c.pose(1.2,y=-8,sy=1.03,ul=0.07,ur=-0.07,head=-0.04,cl=0.03,cr=-0.03); rest(c,3.0); M.append(c)

# ---------------------------------------------------------------- attacks
def attack(name,col,wind,strike,dur=0.6,lunge=55,slash_rot=0.35,slash_scale=1.1,arm=('ur','fr'),both=False,heavy=False,hits=1):
    """A caster's attack: raise the hand, a magic circle blooms at the palm, a flick of the wrist sends the sword
    (SwordVisuals.Strike flies the real sword), the circle fades. Arm angles are softened to a gesture."""
    c=Clip(name,dur); rest(c,0)
    a,f=arm
    side=('LR' if both else ('R' if a=='ur' else 'L'))
    k=0.55 if not heavy else 0.7           # gesture, not a sword swing
    t_w=0.1*dur/0.6*(1.4 if heavy else 1)
    w={a:wind[0]*k,f:wind[1]*k-0.25*(1 if a=='ur' else -1),'x':-4,'rot':-0.02,'head':-0.03}
    if both: w.update(ul=-wind[0]*k,fl=-wind[1]*k)
    c.pose(t_w,**w); c.hand(side,0,0.35,0.0,col); c.hand(side,t_w,0.6,0.9,col)
    hit_t=0.22*dur/0.6*(1.3 if heavy else 1)
    for h in range(hits):
        t=hit_t+h*0.12
        st={a:strike[0]*k,f:strike[1]*k+0.35*(1 if a=='ur' else -1),'x':min(lunge,12)*0.5,'rot':0.03,'head':0.04,'cl':0.03,'cr':0.03}
        if both: st.update(ul=-strike[0]*k,fl=-strike[1]*k)
        c.pose(t,**st)
        c.hand(side,t-0.03,0.7,1.0,col); c.hand(side,t+0.05,0.8,1.0,col)
        if hits>1 and h<hits-1: c.pose(t+0.06,**{a:strike[0]*k*0.8,f:strike[1]*k*0.8})
    c.hand(side,dur*0.8,0.6,0.0,col)
    rest(c,dur); return c
M.append(attack('Attack',PURPLE,(0.18,0.1),(-1.0,-0.2)))
M.append(attack('Attack_Thrust',PURPLE,(0.25,0.4),(-1.45,-0.05),lunge=70,slash_rot=0.0,slash_scale=0.8))
M.append(attack('Attack_Overhead',PURPLE,(-2.2,-0.6),(-0.6,-0.1),dur=0.7,slash_rot=1.3,heavy=True))
M.append(attack('Attack_Backhand',PURPLE,(-1.3,-0.5),(0.15,0.1),lunge=40,slash_rot=-0.6))
M.append(attack('Attack_Double',PURPLE,(0.18,0.1),(-1.0,-0.25),dur=0.75,hits=2))
M.append(attack('Attack_Flurry',PURPLE,(0.1,0.1),(-0.9,-0.3),dur=0.9,hits=3,lunge=40))
M.append(attack('Attack_Heavy',PURPLE,(-1.9,-0.4),(-0.4,0.0),dur=0.85,lunge=75,slash_rot=1.0,slash_scale=1.5,heavy=True))
M.append(attack('Attack_Sweep',PURPLE,(0.5,0.2),(-1.7,-0.3),dur=0.75,lunge=30,slash_rot=-0.2,slash_scale=1.8,both=True))
M.append(attack('Attack_Left',PURPLE,(-0.3,-0.2),(1.1,0.3),lunge=35,slash_rot=2.6,arm=('ul','fl')))
M.append(attack('Attack_Command',PURPLE,(0.1,0.0),(-1.3,-0.1),lunge=10,slash_rot=0.2))
STYLE={'Gram':dict(wind=(-2.0,-0.5),strike=(-0.5,-0.1),heavy=True,dur=0.75,slash_scale=1.3,slash_rot=1.2),
 'Ganjiang':dict(wind=(0.2,0.1),strike=(-1.0,-0.25),hits=2,dur=0.75,both=True),
 'Moye':dict(wind=(0.2,0.1),strike=(-0.9,-0.3),hits=2,dur=0.75,both=True,lunge=35),
 'Kusanagi':dict(wind=(0.4,0.2),strike=(-1.6,-0.2),slash_scale=1.6,slash_rot=-0.1,lunge=45),
 'Tyrfing':dict(wind=(-1.6,-0.6),strike=(-0.8,0.0),slash_rot=0.9,slash_scale=1.3,lunge=65),
 'Dainsleif':dict(wind=(0.15,0.1),strike=(-1.1,-0.3),hits=3,dur=0.9,lunge=45),
 'Durandal':dict(wind=(-1.2,-0.3),strike=(-0.7,-0.1),heavy=True,dur=0.8,slash_rot=0.8),
 'Skofnung':dict(wind=(0.3,0.3),strike=(-1.35,-0.15),lunge=60,slash_rot=0.1),
 'Onimaru':dict(wind=(0.0,0.0),strike=(-1.2,-0.1),lunge=8,slash_rot=0.3,slash_scale=1.2),
 'ClaiomhSolais':dict(wind=(0.6,0.5),strike=(-1.5,-0.05),lunge=50,slash_rot=0.0,slash_scale=1.6),
 'Caladbolg':dict(wind=(-2.4,-0.4),strike=(-0.3,0.1),heavy=True,dur=0.9,lunge=40,slash_rot=1.4,slash_scale=2.0)}
for sw,st in STYLE.items(): M.append(attack('Attack_'+sw,SWORD_COL[sw],**st))

# ---------------------------------------------------------------- skills / powers
def cast(name,dur,peak,col=PURPLE,circle=0.75,sigil=1.0,y=-12,glow=(1.3,1.18,1.55)):
    c=Clip(name,dur); rest(c,0); t=dur*0.35
    c.pose(t,y=y,tint=glow,**peak); c.pose(dur*0.6,y=y*0.6,**{k:v*0.8 for k,v in peak.items()})
    hs=0.55+0.25*max(circle,0.4); c.hand('LR',t*0.6,hs*0.85,0.85,col); c.hand('LR',t,hs,1.0,col); c.hand('LR',dur*0.85,hs,0.0,col)
    c.fx('Visuals/Rig/Sigil',0,0.4,(1,1,1)).fx('Visuals/Rig/Sigil',t,sigil,(1,1,1)).fx('Visuals/Rig/Sigil',dur,0.4,(1,1,1))
    if circle: c.fx('Circle',0,0,col,scale=(circle*0.75,circle*0.26)).fx('Circle',t*0.6,0.8,col).fx('Circle',dur,0,col,scale=(circle*0.75,circle*0.26))
    rest(c,dur); return c
M.append(cast('Cast',0.7,dict(ul=0.4,ur=-0.4,cl=0.04,cr=-0.04,fl=0.1,fr=-0.1)))
M.append(cast('Cast_Point',0.6,dict(ur=-1.4,fr=-0.05,head=0.06),circle=0.5))
M.append(cast('Cast_LeftHand',0.65,dict(ul=1.2,fl=0.3,head=-0.06),circle=0.5))
M.append(cast('Cast_Chest',0.7,dict(fr=-1.6,ur=-0.1,fl=1.4,ul=0.1,head=0.08),circle=0.4,sigil=1.4))
M.append(cast('Cast_Wide',0.75,dict(ul=0.9,ur=-0.9,fl=0.2,fr=-0.2,cl=0.06,cr=-0.06,head=-0.08),circle=0.9))
M.append(cast('Cast_Draw',0.55,dict(ur=-0.6,fr=-1.2,head=0.1),circle=0.0,y=-6))
M.append(cast('Cast_Ward',0.7,dict(ul=0.5,fl=1.2,ur=-0.5,fr=-1.2),circle=0.6))
M.append(cast('Cast_Debuff',0.65,dict(ur=-1.2,fr=-0.4,head=0.12,rot=0.04),col=(0.6,0.9,0.4),circle=0.5,glow=(1.1,1.3,1.0)))
M.append(cast('Power_Rise',1.0,dict(ul=1.4,ur=-1.4,fl=0.3,fr=-0.3,head=-0.12,cl=0.07,cr=-0.07),circle=1.1,sigil=1.6,y=-24,glow=(1.5,1.25,1.9)))
M.append(cast('Power_Focus',0.95,dict(ul=0.3,fl=1.5,ur=-0.3,fr=-1.5,head=0.2),circle=0.8,sigil=1.5,y=4,glow=(1.35,1.2,1.7)))
M.append(cast('Power_Burst',0.9,dict(ul=0.8,ur=-0.8,cl=0.08,cr=-0.08,head=-0.05),circle=1.2,sigil=1.8,y=-16,glow=(1.7,1.4,2.1)))
M.append(cast('Heal',0.9,dict(ul=0.5,fl=1.0,ur=-0.5,fr=-1.0,head=0.1),col=(0.5,1.0,0.6),circle=0.7,glow=(1.1,1.4,1.1)))
M.append(cast('Buff',0.7,dict(ur=-0.3,fr=-1.4,head=-0.05),col=(1.0,0.85,0.4),circle=0.6,glow=(1.4,1.3,1.0)))

# ---------------------------------------------------------------- summons (one per sword) / swaps
for sw,col in SWORD_COL.items():
    c=Clip('Summon_'+sw,0.95); rest(c,0)
    c.pose(0.3,y=-20,ul=0.6,ur=-0.6,fl=0.15,fr=-0.15,cl=0.05,cr=-0.05,head=-0.1,tint=(1.45,1.22,1.85))
    c.pose(0.6,y=-16,ul=0.55,ur=-0.55,head=-0.06)
    c.hand('LR',0.15,0.6,0.9,col); c.hand('LR',0.3,0.85,1.0,col); c.hand('LR',0.6,0.85,1.0,col); c.hand('LR',0.9,0.85,0.0,col)
    c.fx('Circle',0,0,col,scale=(0.8,0.27)).fx('Circle',0.2,0.9,col).fx('Circle',0.95,0,col,scale=(0.8,0.27))
    c.fx('Visuals/Rig/Sigil',0,0.4,(1,1,1),scale=(1,1)).fx('Visuals/Rig/Sigil',0.3,1,col,scale=(1.5,1.5)).fx('Visuals/Rig/Sigil',0.95,0.4,(1,1,1),scale=(1,1))
    rest(c,0.95); M.append(c)
c=Clip('Summon',0.95); [c.tr.update({k:list(v)}) for k,v in M[-1].tr.items()]; M.append(c)  # generic = last sword's
for i,(peak,dur) in enumerate(((dict(ur=-1.1,fr=-0.3,ul=0.3),0.5),(dict(ul=1.1,fl=0.3,ur=-0.3),0.5),(dict(ur=-0.4,fr=-1.5,ul=0.4,fl=1.5,head=0.1),0.55)),1):
    c=Clip(f'Swap_{i}',dur); rest(c,0); c.pose(dur*0.4,y=-6,**peak); c.hand('R' if i==1 else 'L' if i==2 else 'LR',dur*0.4,0.7,1.0); c.hand('LR',dur*0.9,0.7,0.0); c.fx('Visuals/Rig/Sigil',dur*0.4,1,(1,1,1)); rest(c,dur); M.append(c)

# ---------------------------------------------------------------- block / hit / death / victory
def block(name,peak,dur=0.55,back=-10,shield=0.55):
    c=Clip(name,dur); rest(c,0); c.pose(0.12,x=back,**peak); c.pose(dur*0.6,x=back*0.5,**{k:v*0.6 for k,v in peak.items()})
    c.hand('LR',0.1,0.75,1.0,(0.8,0.85,1.0)); c.hand('LR',dur*0.85,0.75,0.0,(0.8,0.85,1.0))
    c.fx('Shield',0,0,(1,1,1),scale=(0.55,0.55)).fx('Shield',0.1,shield,(1,1,1),scale=(0.7,0.7)).fx('Shield',dur,0,(1,1,1),scale=(0.74,0.74))
    rest(c,dur); return c
M.append(block('Block',dict(ur=-0.55,ul=0.2,cl=0.05,cr=0.05)))
M.append(block('Block_Cross',dict(ur=-0.3,fr=-1.3,ul=0.3,fl=1.3,head=0.08),shield=0.65))
M.append(block('Block_Brace',dict(ur=-0.2,ul=0.2,head=0.1,sy=0.97,y=6),back=-16))
M.append(block('Block_Palm',dict(ur=-1.35,fr=-0.2),shield=0.7))
M.append(block('Block_Big',dict(ul=0.7,ur=-0.7,fl=0.4,fr=-0.4,cl=0.06,cr=-0.06),dur=0.7,shield=0.85))
def hit(name,dur,back,spin,arms,red=(1.8,0.75,0.75)):
    c=Clip(name,dur); rest(c,0)
    c.pose(0.06,x=back,rot=spin,tint=red,**arms); c.pose(0.12,x=back*0.65,rot=spin*0.5); c.pose(0.18,x=back*0.9,rot=spin*0.8,tint=(1.2,1,1))
    rest(c,dur); return c
M.append(hit('Hit',0.45,-28,-0.05,dict(ul=0.18,ur=-0.12,cl=0.045,cr=0.045,head=-0.08)))
M.append(hit('Hit_Light',0.3,-14,-0.02,dict(head=-0.06,ul=0.08,ur=-0.06)))
M.append(hit('Hit_Heavy',0.7,-48,-0.12,dict(ul=0.5,ur=-0.4,fl=0.3,fr=-0.3,cl=0.07,cr=0.07,head=-0.18,y=8)))
M.append(hit('Hit_Stagger',0.6,-36,0.08,dict(ul=-0.2,ur=-0.5,head=0.15,y=4)))
# Blocked completely: no red, no recoil of the head — he braces, the palm ward flares and he slides back a step
c=Clip('Hit_Guarded',0.5); rest(c,0)
c.pose(0.05,x=-8,ur=-0.35,fr=-0.55,ul=0.1,head=0.04,cl=0.02,cr=0.04,sy=0.985,tint=(1.15,1.15,1.35))
c.pose(0.16,x=-16,ur=-0.4,fr=-0.6,head=0.05,cl=0.03,cr=0.06,sy=0.98)
c.pose(0.32,x=-10,ur=-0.25,fr=-0.35,head=0.02)
c.hand('R',0.0,0.5,0.0,(0.75,0.85,1.0)); c.hand('R',0.05,0.85,1.0,(0.75,0.85,1.0)); c.hand('R',0.4,0.85,0.0,(0.75,0.85,1.0))
c.fx('Shield',0,0,(1,1,1),scale=(0.62,0.62)).fx('Shield',0.05,0.85,(0.85,0.9,1),scale=(0.72,0.72)).fx('Shield',0.45,0,(1,1,1),scale=(0.78,0.78))
rest(c,0.5); M.append(c)
# Death: struck, knees buckle, he falls on his side and stays there as a darkened body (no fade-out). The rig is a
# flat picture rotating around its centre (REST_Y above the feet), so lying flat = rotate ~86 deg and lower the centre
# to about 70 px above the ground (y offset +128).
DEAD_TINT=(0.55,0.48,0.62)
c=Clip('Dead',1.7); rest(c,0)
c.pose(0.12,x=-14,head=-0.18,ul=0.25,ur=-0.2,cl=0.06,cr=0.06,tint=(1.6,0.8,0.9))
c.pose(0.45,x=-18,y=36,sy=0.93,rot=-0.12,head=0.25,ul=-0.08,ur=0.1,fl=0.1,fr=-0.1,tint=(1.1,0.85,1.0))
c.pose(0.8,x=-30,y=95,sy=0.95,rot=-0.85,head=0.15,ul=-0.15,ur=0.2,cl=0.12,cr=-0.05)
c.pose(1.05,x=-38,y=134,sy=1,rot=-1.52,head=0.05,ul=-0.25,ur=0.35,cl=0.18,cr=-0.1)
c.pose(1.2,x=-40,y=126,rot=-1.47,head=0.1,ul=-0.22,ur=0.3)
c.pose(1.7,x=-40,y=128,rot=-1.5,head=0.12,ul=-0.24,ur=0.32,cl=0.16,cr=-0.08,tint=DEAD_TINT)
c.fx('Visuals/Rig/Sigil',0,0.4,(1,1,1)).fx('Visuals/Rig/Sigil',0.6,0,(1,1,1))
M.append(c)
# Variant: drops to his knees, slumps, then topples forward onto his face
c=Clip('Dead_Kneel',1.9); rest(c,0)
c.pose(0.12,x=-10,head=-0.15,ul=0.2,ur=-0.15,tint=(1.6,0.8,0.9))
c.pose(0.5,y=70,sy=0.86,head=0.35,ul=0.05,ur=-0.05,fl=0.2,fr=-0.2,rot=0.05,tint=(1.1,0.85,1.0))
c.pose(0.95,y=78,sy=0.85,head=0.5,rot=0.12,ul=0.0,ur=0.0)
c.pose(1.3,x=30,y=120,sy=0.95,rot=0.9,head=0.3)
c.pose(1.5,x=44,y=134,sy=1,rot=1.52,head=0.1,ul=0.2,ur=-0.3)
c.pose(1.62,x=44,y=126,rot=1.47)
c.pose(1.9,x=44,y=128,rot=1.5,head=0.12,ul=0.22,ur=-0.3,tint=DEAD_TINT)
c.fx('Visuals/Rig/Sigil',0,0.4,(1,1,1)).fx('Visuals/Rig/Sigil',0.6,0,(1,1,1))
M.append(c)
c=Clip('Victory',1.4); rest(c,0); c.pose(0.4,y=-10,ur=-2.3,fr=-0.2,head=-0.1,cl=0.04,cr=-0.04); c.pose(1.0,y=-8,ur=-2.2,fr=-0.25)
c.fx('Visuals/Rig/Sigil',0.4,1,(1,1,1),scale=(1.4,1.4)); c.hand('R',0.4,0.9,1.0); c.hand('R',1.2,0.9,0.0); rest(c,1.4); M.append(c)
c=Clip('TurnStart',0.6); rest(c,0); c.pose(0.25,y=-6,head=-0.05,ul=0.1,ur=-0.1); c.hand('LR',0.25,0.55,0.8); c.hand('LR',0.55,0.55,0.0); rest(c,0.6); M.append(c)

assert len({c.name for c in M})==len(M), 'duplicate clip names'
# ---------------------------------------------------------------- scene text
P='res://MagicSwordsman/images/'
ORDER=[('Body','body',None),('Head','head',None),('CoatL','coat_l',None),('CoatR','coat_r',None),
       ('UpperL','upper_l',None),('ForeL','fore_l','UpperL'),('UpperR','upper_r',None),('ForeR','fore_r','UpperR')]
PARENT_PART={'ForeL':'upper_l','ForeR':'upper_r'}
PALM={'L':('fore_l',(95*800/1216,690*800/1216)),'R':('fore_r',(672*800/1216,575*800/1216))}  # palms, output canvas px
# layers of one hand circle: (name, texture, tilt rotation, tilt y-squash, spin turns per 6 s)
LAYERS=[('Ring','h_ring',0.0,1.0,1),('Glyph','h_glyph',0.0,1.0,-1),('Core','h_core',0.0,1.0,0)]  # one slow turn per 12 s
res='\n'.join(f'[ext_resource type="Texture2D" path="{P}character/parts/{f}.png" id="p_{f}"]' for _,f,_ in ORDER)
anims='\n'.join(emit(c,f'a{i}') for i,c in enumerate(M))
lib='[sub_resource type="AnimationLibrary" id="lib"]\n_data = {\n'+',\n'.join(f'&"{c.name}": SubResource("a{i}")' for i,c in enumerate(M))+'\n}\n'
nodes='''[node name="MagicSwordsman" type="Node2D"]

[node name="Bounds" type="Control" parent="."]
layout_mode = 3
offset_left = -130.0
offset_top = -410.0
offset_right = 130.0
mouse_filter = 2

[node name="Circle" type="Sprite2D" parent="."]
material = SubResource("add")
modulate = Color(1, 1, 1, 0)
position = Vector2(0, -8)
scale = Vector2(0.75, 0.26)
texture = ExtResource("2_circle")

[node name="Visuals" type="Node2D" parent="."]
unique_name_in_owner = true
position = Vector2(0, -200)

[node name="Rig" type="Node2D" parent="Visuals"]
scale = Vector2(0.5, 0.5)

'''
for n,f,par in ORDER:
    m=PARTS[f]; px,py=m['pivot']; bx,by=m['bbox'][:2]
    if par is None: pos=(px-CX,py-CY); parent='Visuals/Rig'
    else:
        pp=PARTS[PARENT_PART[n]]['pivot']; pos=(px-pp[0],py-pp[1]); parent='Visuals/Rig/'+par
    nodes+=f'[node name="{n}" type="Sprite2D" parent="{parent}"]\nposition = Vector2({pos[0]}, {pos[1]})\ncentered = false\noffset = Vector2({bx-px}, {by-py})\ntexture = ExtResource("p_{f}")\n\n'
spin=[]
for h,(part,(hx,hy)) in PALM.items():
    piv=PARTS[part]['pivot']; par=HAND[h]; fore=par.rsplit('/',1)[0]
    nodes+=f'[node name="Hand{h}" type="Node2D" parent="{fore}"]\nmodulate = {C(*PURPLE,HAND_REST[1])}\nposition = {V(hx-piv[0],hy-piv[1])}\nscale = {V(HAND_REST[0],HAND_REST[0])}\n\n'
    nodes+=f'[node name="Tint" type="Node2D" parent="{par}"]\nmodulate = {C(*PURPLE)}\n\n'
    for ln,tex,tilt,sq,turns in LAYERS:
        nodes+=f'[node name="{ln}Tilt" type="Node2D" parent="{par}/Tint"]\nrotation = {tilt}\nscale = {V(1,sq)}\n\n'
        sc=0.45 if ln=='Core' else 0.62 if ln=='Glyph' else 1.0
        nodes+=f'[node name="{ln}" type="Sprite2D" parent="{par}/Tint/{ln}Tilt"]\nmaterial = SubResource("add")\nscale = {V(sc,sc)}\ntexture = ExtResource("{tex}")\n\n'
        if turns: spin.append((f'{par}/Tint/{ln}Tilt/{ln}',turns if h=='R' else -turns))  # the two hands spin mirrored
spin_tracks=[]
for i,(path,turns) in enumerate(spin):
    spin_tracks.append(f'''tracks/{i}/type = "value"
tracks/{i}/imported = false
tracks/{i}/enabled = true
tracks/{i}/path = NodePath("{path}:rotation")
tracks/{i}/interp = 1
tracks/{i}/loop_wrap = true
tracks/{i}/keys = {{
"times": PackedFloat32Array(0, 12),
"transitions": PackedFloat32Array(1, 1),
"update": 0,
"values": [0.0, {turns*2*math.pi:.5f}]
}}''')
nodes+=f'''[node name="Sigil" type="Sprite2D" parent="Visuals/Rig"]
material = SubResource("add")
modulate = Color(1, 1, 1, 0.4)
position = Vector2({CHEST[0]}, {CHEST[1]})
texture = ExtResource("5_glow")

[node name="Shield" type="Sprite2D" parent="."]
material = SubResource("add")
modulate = Color(1, 1, 1, 0)
position = Vector2(0, -200)
scale = Vector2(0.74, 0.74)
texture = ExtResource("4_shield")

[node name="AnimationPlayer" type="AnimationPlayer" parent="."]
libraries = {{
&"": SubResource("lib")
}}
autoplay = "idle"

[node name="HandSpin" type="AnimationPlayer" parent="."]
libraries = {{
&"": SubResource("spinlib")
}}
autoplay = "spin"
'''
spin_res='[sub_resource type="Animation" id="spin"]\nresource_name = "spin"\nlength = 12.0\nloop_mode = 1\n'+'\n'.join(spin_tracks)+'\n\n[sub_resource type="AnimationLibrary" id="spinlib"]\n_data = {\n&"spin": SubResource("spin")\n}\n'
head=f'''[gd_scene load_steps={len(M)+len(ORDER)+13} format=3]

{res}
[ext_resource type="Texture2D" path="{P}vfx/magic_circle.png" id="2_circle"]
[ext_resource type="Texture2D" path="{P}vfx/shield.png" id="4_shield"]
[ext_resource type="Texture2D" path="{P}charselect/glow.png" id="5_glow"]
[ext_resource type="Texture2D" path="{P}vfx/hand_ring.png" id="h_ring"]
[ext_resource type="Texture2D" path="{P}vfx/hand_glyph.png" id="h_glyph"]
[ext_resource type="Texture2D" path="{P}vfx/hand_core.png" id="h_core"]

[sub_resource type="CanvasItemMaterial" id="add"]
blend_mode = 1

'''
open(OUT,'w').write(head+anims+'\n'+spin_res+'\n'+lib+'\n'+nodes)
open(os.path.join(HERE,'motion_list.txt'),'w').write('\n'.join(f'{c.name}\t{c.length}s' for c in M)+'\n')
print(len(M),'clips ->',OUT)
