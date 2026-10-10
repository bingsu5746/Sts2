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
CHEST=(365*800/1216-CX,262*800/1216-CY)  # the glowing gem on his chest (split_body.py source px)
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
        """Keyframe joints at time t. Keys: x,y (root offset), rot, tint=(r,g,b), and joint names.
        No sx/sy: the body is never squashed or stretched (user 2026-10-10 "줄어들었다가 커지는데 이게 말이 된다고
        생각해? 절대 하지 말고 현실성 있게") — Visuals:scale stays at 1 in every clip (REST)."""
        assert 'sx' not in j and 'sy' not in j, 'no body scaling'
        for k,v in j.items():
            if k in JOINTS: s.key(JOINTS[k]+':rotation',t,f'{v:.4f}')
            elif k=='rot': s.key('Visuals:rotation',t,f'{v:.4f}')
        if 'x' in j or 'y' in j: s.key('Visuals:position',t,V(j.get('x',0),REST_Y+j.get('y',0)))
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
BODY_W,BODY_H=PARTS['body']['bbox'][2]-PARTS['body']['bbox'][0],PARTS['body']['bbox'][3]-PARTS['body']['bbox'][1]
def BODY_RECT(cut=0.0): return f'Rect2(0, 0, {BODY_W}, {BODY_H-cut:.1f})'
REST={**{JOINTS[j]+':rotation':'0.0000' for j in JOINTS},'Visuals/Rig/Body:region_rect':BODY_RECT(),'Visuals:position':V(0,REST_Y),'Visuals:rotation':'0.0000',
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
def rest(c,t): c.hand('LR',t,*HAND_REST); return c.pose(t,**R0,tint=(1,1,1))
M=[]

# ---------------------------------------------------------------- idle family (looping / fidgets)
# idle: a slow 4.2 s breath; the head and arms lag the chest a little and the coat sways on its own rhythm, so the
# loop never moves everything at once (feedback 2026-10-09: make motions more natural)
c=Clip('idle',4.2,loop=True)
for t,b in ((0,0),(2.1,1),(4.2,0)):
    c.pose(t,y=-4*b)  # the breath lifts the chest a little; no scaling
for t,b in ((0,0.15),(0.5,0),(2.6,1),(4.2,0.15)):
    c.pose(t,head=0.025*b,ul=0.035*b,ur=-0.03*b,fl=0.025*b,fr=-0.03*b)
for t,(l,r) in ((0,(0.0,0.0)),(1.0,(0.03,-0.01)),(2.0,(0.005,-0.03)),(3.2,(-0.012,-0.012)),(4.2,(0.0,0.0))):
    c.pose(t,cl=l,cr=r)
c.pose(0,rot=0.0); c.pose(2.3,rot=0.006); c.pose(4.2,rot=0.0)
c.fx('Visuals/Rig/Sigil',0,0.35,(1,1,1)).fx('Visuals/Rig/Sigil',2.1,0.9,(1,1,1)).fx('Visuals/Rig/Sigil',4.2,0.35,(1,1,1)); M.append(c)
c=Clip('Idle_Look',2.4); rest(c,0); c.pose(0.5,head=0.12,y=-2); c.pose(1.3,head=0.12,y=-2); c.pose(1.8,head=-0.05); rest(c,2.4); M.append(c)
c=Clip('Idle_Flex',2.2); rest(c,0); c.pose(0.5,fr=-0.5,ur=-0.15); c.pose(0.8,fr=-0.42,ur=-0.15); c.pose(1.1,fr=-0.5,ur=-0.15); rest(c,2.2)
c.fx('Visuals/Rig/Sigil',0.8,0.9,(1,1,1)); M.append(c)
# Idle_Weave: raises the left palm and turns the circle over like a dial, the right hand adjusting it
c=Clip('Idle_Weave',2.6); rest(c,0); c.pose(0.5,ul=0.25,fl=0.9,ur=-0.15,fr=-0.7,head=0.08); c.hand('L',0.5,0.55,0.8)
c.pose(1.1,ul=0.3,fl=1.0,ur=-0.2,fr=-0.95); c.hand('L',1.1,0.55,0.9)
c.pose(1.7,ul=0.25,fl=0.85,ur=-0.12,fr=-0.6); c.hand('L',1.9,0.55,0.0); rest(c,2.6); M.append(c)
c=Clip('Idle_Shift',2.4); rest(c,0); c.pose(0.8,x=-6,rot=-0.015,head=0.04,cl=0.02); c.pose(1.6,x=-5,rot=-0.012); rest(c,2.4); M.append(c)   # weight shift
c=Clip('Idle_Glance',2.6); rest(c,0); c.pose(0.6,head=-0.16,y=-2); c.pose(1.5,head=-0.14); c.pose(1.9,head=0.02); rest(c,2.6); M.append(c)  # looks up at his swords
c=Clip('Idle_Wrist',2.2); rest(c,0); c.pose(0.5,fr=-0.3,ur=-0.05); c.pose(0.9,fr=-0.15); c.pose(1.3,fr=-0.3); rest(c,2.2)
c.hand('R',0.5,0.3,0.0); c.hand('R',0.9,0.35,0.5); c.hand('R',1.5,0.35,0.0); M.append(c)         # turns the wrist, a faint circle
c=Clip('Idle_Roll',2.4); rest(c,0); c.pose(0.5,ul=-0.06,ur=0.06,y=-3,head=-0.04); c.pose(1.0,ul=0.05,ur=-0.05,y=1,head=0.03); c.pose(1.5,ul=-0.03,ur=0.03,y=-1); rest(c,2.4); M.append(c)  # shoulder roll
c=Clip('Idle_Ready',2.6); rest(c,0); c.pose(0.6,x=10,rot=0.02,head=0.05,ur=-0.25,fr=-0.3,cl=-0.03,cr=0.04); c.pose(1.6,x=9,rot=0.018,ur=-0.22,fr=-0.28)
c.hand('R',0.6,0.4,0.0); c.hand('R',0.9,0.45,0.45); c.hand('R',1.6,0.45,0.0); rest(c,2.6); M.append(c)  # leans into a ready stance
c=Clip('Idle_Gem',2.6); rest(c,0); c.pose(0.6,fl=-0.75,ul=-0.2,head=0.12,y=-1); c.pose(1.7,fl=-0.7,ul=-0.18,head=0.1)
c.fx('Visuals/Rig/Sigil',0.6,1.2,(1,1,1)).fx('Visuals/Rig/Sigil',1.7,1.0,(1,1,1)); rest(c,2.6); M.append(c)  # touches the gem on his chest
c=Clip('Idle_Hair',1.8); rest(c,0); c.pose(0.25,head=-0.09,y=-2); c.pose(0.45,head=0.05); c.pose(0.8,head=0.0); rest(c,1.8); M.append(c)  # tosses the hair out of his eyes
c=Clip('Idle_Breath',3.0); rest(c,0); c.pose(1.2,y=-8,ul=0.07,ur=-0.07,head=-0.04,cl=0.03,cr=-0.03); rest(c,3.0); M.append(c)

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
ARM=0.6  # arm angles of gestures are scaled down: big two-arm raises read as unnatural (feedback 2026-10-09)
def cast(name,dur,peak,col=PURPLE,circle=0.75,sigil=1.0,y=-12,glow=(1.3,1.18,1.55)):
    """A casting gesture: upper arms lead, forearms follow ~0.06 s later (follow-through), a small lean forward."""
    c=Clip(name,dur); rest(c,0); t=dur*0.35
    upper={k:(v*ARM if k in ('ul','ur') else v) for k,v in peak.items() if k not in ('fl','fr')}
    fore={k:v*ARM for k,v in peak.items() if k in ('fl','fr')}
    c.pose(t*0.85,y=y*0.8,x=4,tint=glow,**upper)
    if fore: c.pose(t*0.85,**{k:v*0.4 for k,v in fore.items()}); c.pose(min(dur*0.55,t+0.06),**fore)
    c.pose(dur*0.62,y=y*0.5,x=2,**{k:v*0.75 for k,v in {**upper,**fore}.items()})
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
M.append(cast('Cast_Flick',0.45,dict(ur=-0.4,fr=-1.2,head=0.05),circle=0,sigil=0.8,y=-3))           # a flick of the wrist
M.append(cast('Cast_Gather',0.7,dict(ul=-0.2,fl=1.0,ur=0.2,fr=-1.0,head=0.12),circle=0.5,sigil=1.5,y=-4))  # hands draw in to the chest
M.append(cast('Cast_Push',0.6,dict(ur=-1.1,fr=0.2,head=0.05,cr=-0.04),circle=0.5,y=-2))             # pushes the palm forward
M.append(cast('Cast_Point2',0.5,dict(ur=-1.0,fr=-0.3,head=0.08),circle=0,sigil=0.9,y=-2))          # two fingers point
M.append(cast('Cast_Snap',0.45,dict(ur=-0.7,fr=-0.6,head=0.04),circle=0,sigil=1.1,y=-2))          # a finger snap
M.append(cast('Cast_Sweep',0.7,dict(ur=-0.9,fr=0.3,ul=0.2,head=0.06,cr=-0.05),circle=0.7,y=-4))      # palm sweeps sideways
M.append(cast('Cast_Raise',0.6,dict(ul=0.8,fl=0.6,head=-0.08),circle=0.6,sigil=1.2,y=-8))             # back hand raised
c=Clip('Cast_Beckon',0.7); rest(c,0)                                                                # palm up, fingers curl in
c.pose(0.18,x=3,ur=-0.35,fr=-0.6,head=-0.05); c.pose(0.32,fr=-0.95); c.pose(0.44,fr=-0.7); c.pose(0.54,fr=-0.95)
c.hand('R',0.18,0.45,0.8); c.hand('R',0.54,0.6,1.0); c.hand('R',0.7,0.6,0.0); rest(c,0.7); M.append(c)
c=Clip('Cast_Trace',0.9); rest(c,0)                                                                 # traces a circle in the air
for k,tt in enumerate((0.15,0.3,0.45,0.6,0.75)):
    a=k*1.4; c.pose(tt,x=3,ur=-0.45+0.15*math.cos(a),fr=-0.6+0.25*math.sin(a),head=0.04)
c.hand('R',0.15,0.3,0.6); c.hand('R',0.75,0.7,1.0); c.hand('R',0.9,0.7,0.0); rest(c,0.9); M.append(c)

# ---------------------------------------------------------------- summons (one per sword) / swaps
for sw,col in SWORD_COL.items():
    c=Clip('Summon_'+sw,0.95); rest(c,0)
    # one hand calls the sword out of the air, palm up; the other stays low (no two-arm raise)
    c.pose(0.22,y=-8,x=4,ur=-0.45,fr=-0.35,ul=0.12,head=-0.08,cr=-0.03,tint=(1.35,1.18,1.7))
    c.pose(0.32,fr=-0.7)
    c.pose(0.6,y=-6,ur=-0.4,fr=-0.6,ul=0.1,head=-0.05)
    c.hand('LR',0.15,0.6,0.9,col); c.hand('LR',0.3,0.85,1.0,col); c.hand('LR',0.6,0.85,1.0,col); c.hand('LR',0.9,0.85,0.0,col)
    c.fx('Circle',0,0,col,scale=(0.8,0.27)).fx('Circle',0.2,0.9,col).fx('Circle',0.95,0,col,scale=(0.8,0.27))
    c.fx('Visuals/Rig/Sigil',0,0.4,(1,1,1),scale=(1,1)).fx('Visuals/Rig/Sigil',0.3,1,col,scale=(1.5,1.5)).fx('Visuals/Rig/Sigil',0.95,0.4,(1,1,1),scale=(1,1))
    rest(c,0.95); M.append(c)
c=Clip('Summon',0.95); [c.tr.update({k:list(v)}) for k,v in M[-1].tr.items()]; M.append(c)  # generic = last sword's
for i,(peak,dur) in enumerate(((dict(ur=-1.1,fr=-0.3,ul=0.3),0.5),(dict(ul=1.1,fl=0.3,ur=-0.3),0.5),(dict(ur=-0.4,fr=-1.5,ul=0.4,fl=1.5,head=0.1),0.55)),1):
    c=Clip(f'Swap_{i}',dur); rest(c,0); c.pose(dur*0.4,y=-6,**peak); c.hand('R' if i==1 else 'L' if i==2 else 'LR',dur*0.4,0.7,1.0); c.hand('LR',dur*0.9,0.7,0.0); c.fx('Visuals/Rig/Sigil',dur*0.4,1,(1,1,1)); rest(c,dur); M.append(c)

# ---------------------------------------------------------------- block / hit / death / victory
def block(name,peak,dur=0.6,back=-9,lean=-0.035):
    """Gaining Block: his swords cross into an X in front of him (SwordVisuals.Guard: crossed at 0.14 s, pushed into
    the blow at 0.22 s, home by 0.6 s). The body only braces like a person would: weight onto the back foot (x),
    shoulders lean back a little (rot < 0 tilts the top away from the enemy), the palm steering the swords. No scaling,
    no shield flash."""
    c=Clip(name,dur); rest(c,0)
    c.pose(0.1,x=back*0.6,rot=lean*0.6,**{k:v*0.7 for k,v in peak.items()})
    c.pose(0.22,x=back,rot=lean,**peak)                     # the blow meets the swords
    c.pose(0.3,x=back*0.85,rot=lean*0.8)
    c.pose(dur*0.75,x=back*0.3,rot=lean*0.25,**{k:v*0.3 for k,v in peak.items()})
    c.hand('LR',0.08,0.6,0.85,(0.8,0.85,1.0)); c.hand('LR',0.22,0.7,1.0,(0.8,0.85,1.0)); c.hand('LR',dur*0.85,0.7,0.0,(0.8,0.85,1.0))
    rest(c,dur); return c
M.append(block('Block',dict(ur=-0.55,fr=-0.2,ul=0.15,head=0.05,cl=0.03,cr=0.04)))
M.append(block('Block_Cross',dict(ur=-0.3,fr=-0.7,ul=0.25,fl=0.6,head=0.07,cr=0.04)))
M.append(block('Block_Brace',dict(ur=-0.2,ul=0.2,head=0.09,cl=0.04,cr=0.05,y=4),back=-13))  # bends the knees a little
M.append(block('Block_Palm',dict(ur=-0.85,fr=-0.25,head=0.04,cr=0.03)))
M.append(block('Block_Big',dict(ul=0.35,ur=-0.45,fl=0.3,fr=-0.35,cl=0.05,cr=-0.05,head=0.08,y=3),back=-14,lean=-0.05))
def hit(name,dur,back,spin,arms,red=(1.8,0.75,0.75)):
    c=Clip(name,dur); rest(c,0)
    c.pose(0.06,x=back,rot=spin,tint=red,**arms); c.pose(0.12,x=back*0.65,rot=spin*0.5); c.pose(0.18,x=back*0.9,rot=spin*0.8,tint=(1.2,1,1))
    rest(c,dur); return c
M.append(hit('Hit',0.45,-28,-0.05,dict(ul=0.18,ur=-0.12,cl=0.045,cr=0.045,head=-0.08)))
M.append(hit('Hit_Light',0.3,-14,-0.02,dict(head=-0.06,ul=0.08,ur=-0.06)))
M.append(hit('Hit_Heavy',0.7,-48,-0.12,dict(ul=0.5,ur=-0.4,fl=0.3,fr=-0.3,cl=0.07,cr=0.07,head=-0.18,y=8)))
M.append(hit('Hit_Stagger',0.6,-36,0.08,dict(ul=-0.2,ur=-0.5,head=0.15,y=4)))
# Blocked completely: the crossed swords take the blow (SwordVisuals.Guard, hit: crossed at 0.09 s, struck at 0.17 s).
# He takes the jolt through his stance: a short slide back, the shoulders rock back and settle. No red, no scaling.
c=Clip('Hit_Guarded',0.55); rest(c,0)
c.pose(0.08,x=-5,rot=-0.015,ur=-0.35,fr=-0.5,ul=0.1,head=0.03,cl=0.02,cr=0.03,tint=(1.08,1.08,1.18))
c.pose(0.18,x=-15,rot=-0.045,ur=-0.42,fr=-0.58,ul=0.14,head=-0.02,cl=0.04,cr=0.06)
c.pose(0.28,x=-13,rot=-0.02,ur=-0.35,fr=-0.45,head=0.03)
c.pose(0.42,x=-6,rot=-0.008,ur=-0.15,fr=-0.2,head=0.01,tint=(1,1,1))
c.hand('R',0.0,0.5,0.0,(0.75,0.85,1.0)); c.hand('R',0.08,0.7,1.0,(0.75,0.85,1.0)); c.hand('R',0.45,0.7,0.0,(0.75,0.85,1.0))
rest(c,0.55); M.append(c)
# Death (user 2026-10-10 "본인이 소환한 마검들이 본인을 공격해서 마검들과 싸우다가 몸에 박힌 채로 털썩 주저앉는 모션"):
# his own swords turn on him (SwordVisuals.DeathBetrayal — keep the times in step). 0.15-0.5 they circle him and he
# turns to face them; 0.6 he parries a cut from the front; 1.0 one cuts him from behind and he staggers forward; 1.44 a
# blade goes into his chest, 1.64 one into his belly, 1.84 one into his back; then his knees give and he drops onto
# them, slumped forward over the blades, and stays there (the last key is the corpse pose). The rig is one flat picture
# without separate legs, so kneeling = the whole figure drops by KNEEL px while the Body sprite's region is cut from the
# bottom by the same height (rig px = 2x screen px): his feet stay on the ground line and the lower legs are "folded
# under" him. No scaling, no lying-flat spin.
DEAD_TINT=(0.62,0.56,0.68)
HURT=(1.25,0.88,0.92)
KNEEL=92
def dead_swords(name):
    c=Clip(name,2.4); rest(c,0)
    c.pose(0.15,x=-3,head=-0.1,ul=0.12,ur=-0.18,fr=-0.25)                              # startled: looks up at them
    c.pose(0.42,x=-6,rot=-0.015,head=0.06,ul=0.25,ur=-0.55,fr=-0.5,cl=0.02,cr=0.03)   # wary, palm raised
    c.pose(0.6,x=-13,rot=-0.045,head=-0.06,ur=-1.0,fr=-0.3,ul=0.3,cr=0.05)            # parries the first cut
    c.pose(0.78,x=-9,rot=-0.02,head=0.02,ur=-0.6,fr=-0.4,ul=0.2)
    c.pose(0.92,x=-7,rot=-0.01,head=-0.08,ul=0.35,ur=-0.45,tint=(1,1,1))               # turns, too late
    c.pose(1.02,x=9,rot=0.06,head=0.14,ul=0.5,fl=0.3,ur=-0.2,cl=0.05,cr=-0.02,tint=HURT)  # cut from behind
    c.pose(1.2,x=6,rot=0.035,head=0.08,ul=0.3,ur=-0.35,fr=-0.3,tint=(1.05,0.97,1.0))
    c.pose(1.44,x=-14,rot=-0.07,head=-0.2,ul=0.4,fl=0.2,ur=-0.45,fr=0.1,cl=0.06,cr=0.06,tint=HURT)  # chest
    c.pose(1.64,x=-10,y=6,rot=0.02,head=0.12,ul=0.15,ur=-0.15,fl=0.35,fr=-0.3,tint=(1.15,0.9,0.94))  # belly: folds
    c.pose(1.84,x=-4,y=12,rot=0.06,head=0.2,ul=0.05,ur=0.0,fl=0.2,fr=-0.2,tint=HURT)                # back
    c.pose(2.08,x=0,y=KNEEL+4,rot=0.11,head=0.36,ul=-0.04,ur=0.06,fl=0.12,fr=-0.08,cl=0.06,cr=-0.05,tint=(0.85,0.76,0.88))  # drops onto his knees
    c.pose(2.22,x=0,y=KNEEL-3,rot=0.1,head=0.4)                                                              # settles
    c.pose(2.4,x=0,y=KNEEL,rot=0.11,head=0.42,ul=-0.05,ur=0.07,fl=0.14,fr=-0.1,cl=0.06,cr=-0.05,tint=DEAD_TINT)
    for t,y in ((0,0),(1.84,12),(2.08,KNEEL+4),(2.22,KNEEL-3),(2.4,KNEEL)):
        c.key('Visuals/Rig/Body:region_rect',t,BODY_RECT(2*y))  # feet stay on the ground line
    c.hand('R',0.42,0.5,0.6,(0.8,0.85,1.0)); c.hand('R',0.6,0.75,1.0,(0.8,0.85,1.0)); c.hand('R',0.95,0.6,0.0,(0.8,0.85,1.0))
    c.fx('Visuals/Rig/Sigil',0,0.4,(1,1,1)).fx('Visuals/Rig/Sigil',1.44,0.9,(1,0.6,0.6)).fx('Visuals/Rig/Sigil',2.1,0,(1,1,1))
    return c
M.append(dead_swords('Dead_Swords'))
M.append(dead_swords('Dead'))  # the game's own trigger name: identical, so the body is right even without the redirect
c=Clip('Victory',1.4); rest(c,0); c.pose(0.4,y=-8,ur=-1.2,fr=-0.7,head=-0.12,cl=0.03,cr=-0.03); c.pose(1.0,y=-6,ur=-1.1,fr=-0.65,head=-0.08)
c.fx('Visuals/Rig/Sigil',0.4,1,(1,1,1),scale=(1.4,1.4)); c.hand('R',0.4,0.9,1.0); c.hand('R',1.2,0.9,0.0); rest(c,1.4); M.append(c)
c=Clip('TurnStart',0.6); rest(c,0); c.pose(0.25,y=-6,head=-0.05,ul=0.1,ur=-0.1); c.hand('LR',0.25,0.55,0.8); c.hand('LR',0.55,0.55,0.0); rest(c,0.6); M.append(c)

assert len({c.name for c in M})==len(M), 'duplicate clip names'
# ---------------------------------------------------------------- scene text
P='res://MagicSwordsman/images/'
ORDER=[('Body','body',None),('Head','head',None),('CoatL','coat_l',None),('CoatR','coat_r',None),
       ('UpperL','upper_l',None),('ForeL','fore_l','UpperL'),('UpperR','upper_r',None),('ForeR','fore_r','UpperR')]
PARENT_PART={'ForeL':'upper_l','ForeR':'upper_r'}
PALM={'L':('fore_l',(62*800/1216,662*800/1216)),'R':('fore_r',(684*800/1216,492*800/1216))}  # palms, output canvas px
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
    region=f'region_enabled = true\nregion_rect = {BODY_RECT()}\n' if n=='Body' else ''
    nodes+=f'[node name="{n}" type="Sprite2D" parent="{parent}"]\nposition = Vector2({pos[0]}, {pos[1]})\ncentered = false\noffset = Vector2({bx-px}, {by-py})\n{region}texture = ExtResource("p_{f}")\n\n'
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
