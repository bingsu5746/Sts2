"""Generates MagicSwordsman/scenes/magic_swordsman_combat.tscn (character body + motion clips).
Clips: idle (loop), Attack, Cast, Summon, Block, Hit, Dead. Attack/Cast/Hit/Dead are triggered by the game
through BaseLib; Summon/Block by SwordVisuals.PlayMotion. Re-run after changing timings:  python3 tools/gen_combat_scene.py
"""
import os,json
OUT=os.path.join(os.path.dirname(__file__),'..','MagicSwordsman','scenes','magic_swordsman_combat.tscn')
PARTS=json.load(open(os.path.join(os.path.dirname(__file__),'..','MagicSwordsman','images','character','parts','parts.json')))
CW,CH=PARTS['_size']; CX,CY=CW/2,CH/2   # Visuals origin = centre of the 800 px art (shown at 0.5 scale)
CHEST=(175-CX,200-CY)                    # glowing pendant, in part space
def V(x,y): return f'Vector2({x}, {y})'
def C(r,g,b,a=1): return f'Color({r}, {g}, {b}, {a})'
def track(i,path,keys,interp=2):
    t=', '.join(str(k[0]) for k in keys); v=', '.join(k[1] for k in keys)
    return f'''tracks/{i}/type = "value"
tracks/{i}/imported = false
tracks/{i}/enabled = true
tracks/{i}/path = NodePath("{path}")
tracks/{i}/interp = {interp}
tracks/{i}/loop_wrap = true
tracks/{i}/keys = {{
"times": PackedFloat32Array({t}),
"transitions": PackedFloat32Array({', '.join('1' for _ in keys)}),
"update": 0,
"values": [{v}]
}}'''
R=['Visuals/Rig/ArmL:rotation','Visuals/Rig/ArmR:rotation','Visuals/Rig/CoatL:rotation','Visuals/Rig/CoatR:rotation']
BASE=[('Visuals:position',V(0,-200)),('Visuals:rotation','0.0'),('Visuals:scale',V(1,1)),('Visuals:modulate',C(1,1,1))]+[(r,'0.0') for r in R]+[
      ('Circle:modulate',C(1,1,1,0)),('Slash:modulate',C(1,1,1,0)),('Shield:modulate',C(1,1,1,0))]
def anim(aid,name,length,tracks,loop=False):
    # every clip resets the tracks it does not animate, so clips never leave leftovers
    used={p for p,_ in tracks}; allt=list(tracks)+[(p,[(0.0,v)]) for p,v in BASE if p not in used]
    body='\n'.join(track(i,p,k) for i,(p,k) in enumerate(allt))
    return f'[sub_resource type="Animation" id="{aid}"]\nresource_name = "{name}"\nlength = {length}\n'+('loop_mode = 1\n' if loop else '')+body+'\n'
def rot(path,keys): return (path,[(t,str(v)) for t,v in keys])
A=[]
A.append(anim('a_idle','idle',3.0,[
 ('Visuals:position',[(0.0,V(0,-200)),(1.5,V(0,-204)),(3.0,V(0,-200))]),
 ('Visuals:scale',[(0.0,V(1,1)),(1.5,V(1.006,1.014)),(3.0,V(1,1))]),
 rot('Visuals/Rig/ArmL:rotation',[(0.0,0.0),(1.5,0.035),(3.0,0.0)]),
 rot('Visuals/Rig/ArmR:rotation',[(0.0,0.0),(1.5,-0.035),(3.0,0.0)]),
 rot('Visuals/Rig/CoatL:rotation',[(0.0,0.0),(0.9,0.03),(2.1,-0.012),(3.0,0.0)]),
 rot('Visuals/Rig/CoatR:rotation',[(0.0,0.0),(1.2,-0.035),(2.4,0.01),(3.0,0.0)]),
 ('Visuals/Rig/Sigil:modulate',[(0.0,C(1,1,1,0.35)),(1.5,C(1,1,1,0.9)),(3.0,C(1,1,1,0.35))]),
],loop=True))
A.append(anim('a_attack','Attack',0.6,[
 ('Visuals:position',[(0.0,V(0,-200)),(0.1,V(-14,-198)),(0.22,V(55,-202)),(0.6,V(0,-200))]),
 rot('Visuals/Rig/ArmR:rotation',[(0.0,0.0),(0.1,0.18),(0.22,-1.0),(0.38,-0.85),(0.6,0.0)]),
 rot('Visuals/Rig/ArmL:rotation',[(0.0,0.0),(0.12,-0.1),(0.25,0.2),(0.6,0.0)]),
 rot('Visuals/Rig/CoatL:rotation',[(0.0,0.0),(0.22,0.05),(0.6,0.0)]),
 rot('Visuals/Rig/CoatR:rotation',[(0.0,0.0),(0.22,0.07),(0.6,0.0)]),
 ('Slash:modulate',[(0.0,C(1,1,1,0)),(0.18,C(1,1,1,0)),(0.24,C(1,1,1,1)),(0.46,C(1,1,1,0))]),
 ('Slash:scale',[(0.18,V(0.5,0.5)),(0.32,V(1.1,1.1)),(0.46,V(1.25,1.25))]),
]))
A.append(anim('a_cast','Cast',0.7,[
 ('Visuals:position',[(0.0,V(0,-200)),(0.25,V(0,-212)),(0.7,V(0,-200))]),
 ('Visuals:modulate',[(0.0,C(1,1,1)),(0.25,C(1.3,1.18,1.55)),(0.7,C(1,1,1))]),
 rot('Visuals/Rig/ArmL:rotation',[(0.0,0.0),(0.25,0.4),(0.7,0.0)]),
 rot('Visuals/Rig/ArmR:rotation',[(0.0,0.0),(0.25,-0.4),(0.7,0.0)]),
 rot('Visuals/Rig/CoatL:rotation',[(0.0,0.0),(0.25,0.04),(0.7,0.0)]),
 rot('Visuals/Rig/CoatR:rotation',[(0.0,0.0),(0.25,-0.04),(0.7,0.0)]),
 ('Visuals/Rig/Sigil:modulate',[(0.0,C(1,1,1,0.4)),(0.25,C(1,1,1,1)),(0.7,C(1,1,1,0.4))]),
 ('Circle:modulate',[(0.0,C(1,1,1,0)),(0.12,C(1,1,1,0.9)),(0.7,C(1,1,1,0))]),
 ('Circle:scale',[(0.0,V(0.25,0.09)),(0.7,V(0.75,0.26))]),
]))
A.append(anim('a_summon','Summon',0.95,[
 ('Visuals:position',[(0.0,V(0,-200)),(0.3,V(0,-220)),(0.95,V(0,-200))]),
 ('Visuals:modulate',[(0.0,C(1,1,1)),(0.3,C(1.45,1.22,1.85)),(0.95,C(1,1,1))]),
 rot('Visuals/Rig/ArmL:rotation',[(0.0,0.0),(0.3,0.6),(0.6,0.55),(0.95,0.0)]),
 rot('Visuals/Rig/ArmR:rotation',[(0.0,0.0),(0.3,-0.6),(0.6,-0.55),(0.95,0.0)]),
 rot('Visuals/Rig/CoatL:rotation',[(0.0,0.0),(0.3,0.05),(0.95,0.0)]),
 rot('Visuals/Rig/CoatR:rotation',[(0.0,0.0),(0.3,-0.05),(0.95,0.0)]),
 ('Visuals/Rig/Sigil:modulate',[(0.0,C(1,1,1,0.4)),(0.3,C(1.5,1.5,1.5,1)),(0.95,C(1,1,1,0.4))]),
 ('Visuals/Rig/Sigil:scale',[(0.0,V(1,1)),(0.3,V(1.5,1.5)),(0.95,V(1,1))]),
 ('Circle:modulate',[(0.0,C(1,1,1,0)),(0.15,C(1,1,1,1)),(0.95,C(1,1,1,0))]),
 ('Circle:scale',[(0.0,V(0.3,0.1)),(0.95,V(1.0,0.34))]),
 ('Circle:rotation',[(0.0,'0.0'),(0.95,'1.2')]),
]))
A.append(anim('a_block','Block',0.55,[
 ('Visuals:position',[(0.0,V(0,-200)),(0.12,V(-10,-200)),(0.55,V(0,-200))]),
 rot('Visuals/Rig/ArmR:rotation',[(0.0,0.0),(0.12,-0.55),(0.55,0.0)]),
 rot('Visuals/Rig/ArmL:rotation',[(0.0,0.0),(0.12,0.2),(0.55,0.0)]),
 rot('Visuals/Rig/CoatL:rotation',[(0.0,0.0),(0.12,0.05),(0.55,0.0)]),
 rot('Visuals/Rig/CoatR:rotation',[(0.0,0.0),(0.12,0.05),(0.55,0.0)]),
 ('Shield:modulate',[(0.0,C(1,1,1,0)),(0.1,C(1,1,1,0.55)),(0.55,C(1,1,1,0))]),
 ('Shield:scale',[(0.0,V(0.55,0.55)),(0.15,V(0.7,0.7)),(0.55,V(0.74,0.74))]),
]))
A.append(anim('a_hit','Hit',0.45,[
 ('Visuals:position',[(0.0,V(0,-200)),(0.06,V(-28,-200)),(0.12,V(-18,-200)),(0.18,V(-26,-200)),(0.45,V(0,-200))]),
 ('Visuals:modulate',[(0.0,C(1,1,1)),(0.05,C(1.8,0.75,0.75)),(0.25,C(1,1,1))]),
 ('Visuals:rotation',[(0.0,'0.0'),(0.06,'-0.05'),(0.45,'0.0')]),
 rot('Visuals/Rig/ArmL:rotation',[(0.0,0.0),(0.06,0.18),(0.45,0.0)]),
 rot('Visuals/Rig/ArmR:rotation',[(0.0,0.0),(0.06,-0.12),(0.45,0.0)]),
 rot('Visuals/Rig/CoatL:rotation',[(0.0,0.0),(0.08,0.045),(0.45,0.0)]),
 rot('Visuals/Rig/CoatR:rotation',[(0.0,0.0),(0.08,0.06),(0.45,0.0)]),
]))
A.append(anim('a_dead','Dead',1.2,[
 ('Visuals:position',[(0.0,V(0,-200)),(0.6,V(-10,-170)),(1.2,V(-14,-160))]),
 ('Visuals:rotation',[(0.0,'0.0'),(0.6,'-0.25'),(1.2,'-0.3')]),
 ('Visuals:modulate',[(0.0,C(1,1,1)),(0.3,C(1.4,0.8,1.4)),(1.2,C(0.6,0.5,0.7,0.35))]),
 rot('Visuals/Rig/ArmL:rotation',[(0.0,0.0),(0.6,-0.12),(1.2,-0.15)]),
 rot('Visuals/Rig/ArmR:rotation',[(0.0,0.0),(0.6,0.12),(1.2,0.15)]),
]))
names=['idle','Attack','Cast','Summon','Block','Hit','Dead']; ids=['a_idle','a_attack','a_cast','a_summon','a_block','a_hit','a_dead']
lib='[sub_resource type="AnimationLibrary" id="lib"]\n_data = {\n'+',\n'.join(f'&"{n}": SubResource("{i}")' for n,i in zip(names,ids))+'\n}\n'
P='res://MagicSwordsman/images/'
head=f'''[gd_scene load_steps={len(A)+7} format=3]

{{PART_RES}}
[ext_resource type="Texture2D" path="{P}vfx/magic_circle.png" id="2_circle"]
[ext_resource type="Texture2D" path="{P}vfx/slash.png" id="3_slash"]
[ext_resource type="Texture2D" path="{P}vfx/shield.png" id="4_shield"]
[ext_resource type="Texture2D" path="{P}charselect/glow.png" id="5_glow"]

[sub_resource type="CanvasItemMaterial" id="add"]
blend_mode = 1
'''
nodes=f'''[node name="MagicSwordsman" type="Node2D"]

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

{{PART_NODES}}
[node name="Sigil" type="Sprite2D" parent="Visuals/Rig"]
material = SubResource("add")
modulate = Color(1, 1, 1, 0.4)
position = Vector2({CHEST[0]}, {CHEST[1]})
texture = ExtResource("5_glow")

[node name="Slash" type="Sprite2D" parent="."]
material = SubResource("add")
modulate = Color(1, 1, 1, 0)
position = Vector2(170, -230)
rotation = 0.35
texture = ExtResource("3_slash")

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
'''
order=[('Body','body'),('CoatL','coat_l'),('CoatR','coat_r'),('ArmL','arm_l'),('ArmR','arm_r')]
res='\n'.join(f'[ext_resource type="Texture2D" path="{P}character/parts/{f}.png" id="p_{f}"]' for _,f in order)
pn=''
for n,f in order:
    m=PARTS[f]; px,py=m['pivot']; bx,by=m['bbox'][:2]
    pn+=f'[node name="{n}" type="Sprite2D" parent="Visuals/Rig"]\nposition = Vector2({px-CX}, {py-CY})\ncentered = false\noffset = Vector2({bx-px}, {by-py})\ntexture = ExtResource("p_{f}")\n\n'
head=head.replace('{PART_RES}',res).replace(f'load_steps={len(A)+7}',f'load_steps={len(A)+12}')
nodes=nodes.replace('{PART_NODES}',pn)
open(OUT,'w').write(head+'\n'+'\n'.join(A)+'\n'+lib+'\n'+nodes)
print('written',OUT)
