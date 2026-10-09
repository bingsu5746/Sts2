extends SceneTree
## Headless preview of Visuals/SwordFx.cs and Visuals/BossKillCutIn.cs (GDScript mirror: the mod's C# cannot run in a
## plain Godot editor). Keep in step with the C#. Renders PNG frames into OUT.
## Run from a Godot project whose res://MagicSwordsman points at MagicSwordsman/MagicSwordsman:
##   godot --headless --import ; xvfb-run -a godot --rendering-driver opengl3 --resolution 1920x1080 --fixed-fps 60 -s fx_preview.gd

const OUT := "/tmp/claude-0/gtest-fx/out"
const R := "res://MagicSwordsman"
const SWORDS := ["gram", "ganjiang", "moye", "kusanagi", "tyrfing", "dainsleif", "durandal", "skofnung", "onimaru", "claiomhsolais", "caladbolg"]
const COLORS := {
	"gram": Color(0.95, 0.78, 0.30), "ganjiang": Color(0.85, 0.38, 0.26), "moye": Color(0.55, 0.78, 0.98),
	"kusanagi": Color(0.45, 0.80, 0.55), "tyrfing": Color(1.00, 0.55, 0.20), "dainsleif": Color(0.75, 0.15, 0.20),
	"durandal": Color(0.98, 0.95, 0.85), "skofnung": Color(0.65, 0.85, 1.00), "onimaru": Color(0.45, 0.30, 0.60),
	"claiomhsolais": Color(1.00, 1.00, 0.75), "caladbolg": Color(0.40, 0.90, 0.95)}
const NAMES := {"gram": "그람", "ganjiang": "간장", "moye": "막야", "kusanagi": "쿠사나기", "tyrfing": "티르빙",
	"dainsleif": "다인슬레이프", "durandal": "뒤랑달", "skofnung": "스코프눙", "onimaru": "오니마루",
	"claiomhsolais": "클라이브 솔라시", "caladbolg": "칼라드볼그"}

var add_mat := CanvasItemMaterial.new()
var texs := {}
var frame := 0
var jobs := []   # [frame, callable]
var scene: Node2D

func _init():
	add_mat.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
	DirAccess.make_dir_recursive_absolute(OUT)
	# scenario timeline (60 fps fixed): each scenario builds at its start frame, shots at start + t*60
	var f := 2
	f = scenario(f, "summon", [0.08, 0.18, 0.3, 0.5])
	f = scenario(f, "switch", [0.16, 0.26, 0.4])
	f = scenario(f, "impact", [0.05, 0.12, 0.25])
	f = scenario(f, "cutin_tyrfing", [0.12, 0.3, 0.5, 0.85, 1.1])
	f = scenario(f, "cutin_moye", [0.5])
	jobs.append([f + 2, func(): quit()])

func scenario(f: int, name: String, times: Array) -> int:
	jobs.append([f, func(): build(name)])
	for t in times:
		var tt: float = t
		jobs.append([f + int(round(tt * 60.0)), func(): shot("%s_%03d" % [name, int(tt * 100)])])
	return f + int(round(times[-1] * 60.0)) + 3

func _process(_d):
	frame += 1
	for j in jobs:
		if j[0] == frame: j[1].call()
	return false

func shot(n: String):
	root.get_texture().get_image().save_png("%s/%s.png" % [OUT, n])

func build(name: String):
	if scene: scene.queue_free()
	scene = Node2D.new(); root.add_child(scene)
	var bg := ColorRect.new(); bg.size = Vector2(1920, 1080); bg.color = Color(0.16, 0.15, 0.17); scene.add_child(bg)
	var floor := ColorRect.new(); floor.size = Vector2(1920, 300); floor.position = Vector2(0, 780); floor.color = Color(0.12, 0.11, 0.12); scene.add_child(floor)
	if name == "summon" or name == "switch":
		for i in SWORDS.size():
			var cell := Node2D.new(); cell.z_index = 10; scene.add_child(cell)
			cell.position = Vector2(180 + (i % 6) * 312, 400 + (i / 6) * 520); cell.scale = Vector2(0.85, 0.85)
			label(scene, SWORDS[i], cell.position + Vector2(-120, 120), 20)
			var node := make_sword(SWORDS[i]); node.z_index = 1; cell.add_child(node)
			if name == "summon":
				node.position = Vector2(0, -220 + 220) # SpawnPos relative to slot (slot = origin here)
				node.scale = Vector2(0.3, 0.3)
				summon(cell, node, SWORDS[i], Vector2.ZERO)
				layout(node, Vector2.ZERO)
			else:
				node.position = Vector2(-150, -10); node.scale = Vector2(0.8, 0.8); node.modulate = Color(0.75, 0.75, 0.8, 0.9)
				layout(node, Vector2.ZERO)
				switch_fx(cell, SWORDS[i], Vector2.ZERO)
	elif name == "impact":
		for i in SWORDS.size():
			var pos := Vector2(180 + (i % 6) * 312, 330 + (i / 6) * 480)
			var enemy := ColorRect.new(); enemy.size = Vector2(150, 220); enemy.position = pos - Vector2(75, 110); enemy.color = Color(0.3, 0.27, 0.25); scene.add_child(enemy)
			label(scene, SWORDS[i], pos + Vector2(-120, 140), 20)
			var fx := Node2D.new(); fx.z_index = 5; scene.add_child(fx); fx.position = pos
			play_impact(fx, SWORDS[i])
	elif name.begins_with("cutin_"):
		var s := name.substr(6)
		var art := Sprite2D.new(); art.texture = load(R + "/images/swords/tyrfing.png"); art.position = Vector2(500, 600); art.scale = Vector2(0.2, 0.2); scene.add_child(art)
		var layer := CanvasLayer.new(); layer.layer = 115; scene.add_child(layer)
		var rootc := Control.new(); rootc.mouse_filter = Control.MOUSE_FILTER_IGNORE; rootc.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT); layer.add_child(rootc)
		cutin(rootc, Vector2(1920, 1080), s, COLORS[s], ("%s · %s" % [NAMES["ganjiang"], NAMES["moye"]]) if s in ["ganjiang", "moye"] else NAMES[s])

func label(p: Node, t: String, pos: Vector2, size: int):
	var l := Label.new(); l.text = t; l.position = pos; l.add_theme_font_size_override("font_size", size); p.add_child(l)

func make_sword(id: String) -> Node2D:
	var holder := Node2D.new(); var blade := Node2D.new(); blade.name = "Blade"; holder.add_child(blade)
	var tex: Texture2D = load(R + "/images/swords/%s.png" % id)
	var k := 190.0 / tex.get_height()
	var sp := Sprite2D.new(); sp.texture = tex; sp.scale = Vector2(k, k); blade.add_child(sp)
	return holder

## SwordVisuals.Layout for one sword going to the current slot
func layout(node: Node2D, slot: Vector2):
	var t := node.create_tween().set_parallel()
	t.tween_property(node, "position", slot, 0.35).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	t.tween_property(node, "scale", Vector2(1.15, 1.15), 0.3)
	t.tween_property(node, "rotation", 0.0, 0.3)
	t.tween_property(node, "modulate", Color.WHITE, 0.3)

# ------------------------------------------------------------------ SwordFx mirror

func T(n: String) -> Texture2D:
	if texs.has(n): return texs[n]
	var t: Texture2D = null
	for p in [R + "/images/vfx/fx/%s.png" % n, R + "/images/vfx/%s.png" % n]:
		if ResourceLoader.exists(p): t = load(p); break
	texs[n] = t; return t

func fxroot(p: Node2D, pos: Vector2, life: float, z: int) -> Node2D:
	var n := Node2D.new(); n.position = pos; n.z_index = z; p.add_child(n)
	if z < 0: p.move_child(n, 0)
	var t := n.create_tween(); t.tween_interval(life); t.tween_callback(n.queue_free); return n

func pose(node: Node2D, pos: Vector2, scale, rot: float, mod: Color):
	node.position = pos; node.scale = scale if scale is Vector2 else Vector2(scale, scale); node.rotation = rot; node.modulate = mod

func sprite(p: Node2D, tex: String, col: Color, additive: bool, pos: Vector2) -> Sprite2D:
	var s := Sprite2D.new(); s.texture = T(tex); s.position = pos; s.modulate = col
	if additive: s.material = add_mat
	p.add_child(s); return s

func delayed(n: Node, d: float) -> Tween:
	var t := n.create_tween()
	if d > 0: t.tween_interval(d)
	return t

func cola(c: Color, a: float) -> Color: return Color(c.r, c.g, c.b, a)

func flare(p, pos, col, size, dur, delay := 0.0):
	var s := sprite(p, "fx_glow", cola(col, 0), true, pos); var k: float = size / 128.0
	s.scale = Vector2.ONE * k * 0.3
	var t := delayed(s, delay)
	t.tween_property(s, "modulate:a", 0.9, dur * 0.2)
	t.parallel().tween_property(s, "scale", Vector2.ONE * k, dur * 0.35).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	t.tween_property(s, "modulate:a", 0.0, dur * 0.65).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)

func ring(p, pos, col, r0, r1, dur, delay := 0.0, squash := 1.0):
	var s := sprite(p, "fx_ring", cola(col, 0), true, pos)
	var k0: float = r0 / 110.0; var k1: float = r1 / 110.0
	s.scale = Vector2(k0, k0 * squash)
	var t := delayed(s, delay)
	t.tween_property(s, "modulate:a", 0.85, 0.03)
	t.tween_property(s, "scale", Vector2(k1, k1 * squash), dur).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	t.parallel().tween_property(s, "modulate:a", 0.0, dur).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)

func ramp(a: Color, b = null) -> Gradient:
	if b == null: b = a
	var g := Gradient.new(); g.set_color(0, a); g.set_color(1, cola(b, 0)); g.add_point(0.55, b); return g

## o: dir, radius, circle, rect, radial, tangential, spin, alignY, delay
func burst(p, pos, rmp, tex, amount, life, vmin, vmax, grav, spread, smin, smax, additive, o := {}):
	var c := CPUParticles2D.new()
	c.position = pos; c.amount = amount; c.lifetime = life; c.one_shot = true; c.explosiveness = 0.85; c.randomness = 0.3
	c.texture = T(tex); c.direction = Vector2.RIGHT.rotated(deg_to_rad(o.get("dir", -90.0))); c.spread = spread; c.gravity = grav
	c.initial_velocity_min = vmin; c.initial_velocity_max = vmax; c.scale_amount_min = smin; c.scale_amount_max = smax
	c.color_ramp = rmp; c.radial_accel_min = o.get("radial", 0.0); c.radial_accel_max = o.get("radial", 0.0)
	c.tangential_accel_min = o.get("tangential", 0.0) * 0.8; c.tangential_accel_max = o.get("tangential", 0.0)
	c.damping_min = 20; c.damping_max = 60; c.angle_min = -180; c.angle_max = 180
	c.angular_velocity_min = -o.get("spin", 0.0); c.angular_velocity_max = o.get("spin", 0.0); c.emitting = false
	c.set_particle_flag(CPUParticles2D.PARTICLE_FLAG_ALIGN_Y_TO_VELOCITY, o.get("alignY", false))
	if o.get("circle", 0.0) > 0: c.emission_shape = CPUParticles2D.EMISSION_SHAPE_SPHERE_SURFACE; c.emission_sphere_radius = o["circle"]
	elif o.has("rect"): c.emission_shape = CPUParticles2D.EMISSION_SHAPE_RECTANGLE; c.emission_rect_extents = o["rect"]
	elif o.get("radius", 0.0) > 0: c.emission_shape = CPUParticles2D.EMISSION_SHAPE_SPHERE; c.emission_sphere_radius = o["radius"]
	if additive: c.material = add_mat
	p.add_child(c)
	var d: float = o.get("delay", 0.0)
	if d <= 0: c.emitting = true
	else: delayed(c, d).tween_callback(func(): c.emitting = true)

func stroke(p, from, rot, col, length, thickness, dur, additive, delay := 0.0):
	var s := sprite(p, "fx_brush", cola(col, 0), additive, from)
	s.centered = false; s.offset = Vector2(0, -96); s.rotation = rot
	var target := Vector2(length / 1024.0, thickness / 192.0 * 1.6)
	s.scale = Vector2(0.01, target.y)
	var t := delayed(s, delay)
	t.tween_property(s, "modulate:a", col.a, 0.02)
	t.parallel().tween_property(s, "scale:x", target.x, dur * 0.22).set_trans(Tween.TRANS_EXPO).set_ease(Tween.EASE_OUT)
	t.tween_interval(dur * 0.25)
	t.tween_property(s, "modulate:a", 0.0, dur * 0.53).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	t.parallel().tween_property(s, "scale:y", target.y * 0.5, dur * 0.53)

func pillar(p, pos, col, height, width, dur, delay := 0.0):
	var s := sprite(p, "fx_pillar", cola(col, 0), true, pos)
	var k := Vector2(width / 128.0, height / 512.0)
	s.scale = Vector2(k.x * 0.2, k.y)
	var t := delayed(s, max(0.0, delay))
	t.tween_property(s, "modulate:a", 0.9, dur * 0.15)
	t.parallel().tween_property(s, "scale:x", k.x, dur * 0.2).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	t.tween_interval(dur * 0.3)
	t.tween_property(s, "modulate:a", 0.0, dur * 0.5)
	t.parallel().tween_property(s, "scale:x", k.x * 0.15, dur * 0.5).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)

func beam(p, pos, rot, col, length, width, dur, delay := 0.0):
	var s := sprite(p, "fx_spark", cola(col, 0), true, pos); s.rotation = rot
	var k := Vector2(width / 16.0, length / 64.0)
	s.scale = Vector2(k.x, k.y * 0.2)
	var t := delayed(s, delay)
	t.tween_property(s, "modulate:a", 1.0, 0.03)
	t.parallel().tween_property(s, "scale:y", k.y, dur * 0.3).set_trans(Tween.TRANS_EXPO).set_ease(Tween.EASE_OUT)
	t.tween_property(s, "modulate:a", 0.0, dur * 0.7)
	t.parallel().tween_property(s, "scale:x", k.x * 0.3, dur * 0.7)

func rays(p, pos, col, n, length, dur, delay := 0.0):
	var holder := Node2D.new(); holder.position = pos; holder.rotation = randf() * TAU; p.add_child(holder)
	for i in n:
		var a: float = TAU * i / n
		var len: float = length * (1.0 if i % 2 == 0 else 0.6)
		var s := sprite(holder, "fx_spark", cola(col, 0), true, Vector2.UP.rotated(a) * len * 0.5)
		s.rotation = a
		var k := Vector2(0.9, len / 64.0)
		s.scale = Vector2(k.x, 0.1)
		var t := delayed(s, delay)
		t.tween_property(s, "modulate:a", 0.9, 0.03)
		t.parallel().tween_property(s, "scale:y", k.y, dur * 0.35).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
		t.tween_property(s, "modulate:a", 0.0, dur * 0.65)
	delayed(holder, delay).tween_property(holder, "rotation", holder.rotation + 0.35, dur + 0.05)

func slash(p, pos, rot, col, scale, dur, delay := 0.0):
	var s := sprite(p, "fx_crescent", cola(col, 0), true, pos)
	s.rotation = rot - 0.4; s.scale = Vector2(scale * 0.6, scale * 0.6)
	var t := delayed(s, delay)
	t.tween_property(s, "modulate:a", col.a, 0.03)
	t.tween_property(s, "rotation", rot + 0.25, dur).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	t.parallel().tween_property(s, "scale", Vector2(scale, scale), dur * 0.6)
	t.parallel().tween_property(s, "modulate:a", 0.0, dur).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)

func blot(p, pos, col, r0, r1, dur, delay := 0.0, squash := 0.32):
	var holder := Node2D.new(); holder.position = pos; holder.scale = Vector2(1, squash); p.add_child(holder)
	var s := sprite(holder, "fx_ink", cola(col, 0), false, Vector2.ZERO)
	s.rotation = randf() * TAU; s.scale = Vector2.ONE * r0 / 48.0
	var t := delayed(s, delay)
	t.tween_property(s, "modulate:a", col.a, 0.05)
	t.parallel().tween_property(s, "scale", Vector2.ONE * r1 / 48.0, dur * 0.4).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	t.tween_property(s, "modulate:a", 0.0, dur * 0.6)

func arc(p, center, radius, from_deg, to_deg, col, width, dur, rainbow := false, delay := 0.0, grow := 1.0):
	var line := Line2D.new(); line.position = center; line.width = width; line.default_color = col; line.material = add_mat
	line.modulate = Color(1, 1, 1, 0); line.joint_mode = Line2D.LINE_JOINT_ROUND; line.begin_cap_mode = Line2D.LINE_CAP_ROUND
	line.end_cap_mode = Line2D.LINE_CAP_ROUND; line.antialiased = true
	var wc := Curve.new(); wc.add_point(Vector2(0, 0.2)); wc.add_point(Vector2(0.5, 1)); wc.add_point(Vector2(1, 0.4)); line.width_curve = wc
	if rainbow:
		var cs := [Color(1, 0.35, 0.35), Color(1, 0.65, 0.3), Color(1, 0.95, 0.4), Color(0.45, 1, 0.5), Color(0.4, 0.85, 1), Color(0.5, 0.55, 1), Color(0.8, 0.5, 1)]
		var g := Gradient.new(); g.set_color(0, cs[0]); g.set_color(1, cs[-1])
		for i in range(1, cs.size() - 1): g.add_point(i / float(cs.size() - 1), cs[i])
		line.gradient = g
	p.add_child(line)
	line.points = PackedVector2Array([Vector2.ZERO, Vector2.ZERO])
	var draw := func(k: float):
		var n: int = max(2, int(28 * k) + 1); var pts := PackedVector2Array()
		for i in n:
			var a: float = deg_to_rad(lerp(float(from_deg), float(to_deg), k * i / (n - 1)))
			pts.append(Vector2(cos(a), sin(a)) * radius)
		line.points = pts
	var t := delayed(line, delay)
	t.tween_property(line, "modulate:a", 0.9, 0.04)
	t.parallel().tween_method(draw, 0.05, 1.0, dur * 0.5).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	if grow != 1.0: t.parallel().tween_property(line, "scale", Vector2.ONE * grow, dur).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	t.tween_property(line, "modulate:a", 0.0, dur * 0.5)

func converge(p, pos, col, n, radius, dur):
	var a0 := randf() * TAU
	for i in n:
		var a: float = a0 + TAU * i / n + randf_range(-0.25, 0.25)
		var s := sprite(p, "fx_shard", cola(col, 0), true, pos + Vector2.RIGHT.rotated(a) * radius * randf_range(0.8, 1.15))
		s.rotation = a + PI; s.scale = Vector2.ONE * randf_range(0.5, 0.8)
		var t := s.create_tween()
		t.tween_property(s, "modulate:a", 1.0, 0.05)
		t.parallel().tween_property(s, "position", pos, dur).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
		t.tween_property(s, "modulate:a", 0.0, 0.06)

func twin(p, pos, radius, dur, moye_first):
	for i in 2:
		var sw := "moye" if (i == 0) == moye_first else "ganjiang"
		var col: Color = COLORS[sw]
		var phase := i * PI
		var s := sprite(p, "fx_glow", cola(col, 0.95), true, pos + Vector2.RIGHT.rotated(phase) * radius); s.scale = Vector2.ONE * 0.7
		var tr := CPUParticles2D.new(); tr.amount = 24; tr.lifetime = 0.25; tr.local_coords = false; tr.texture = T("fx_glow")
		tr.scale_amount_min = 0.25; tr.scale_amount_max = 0.4; tr.color_ramp = ramp(cola(col, 0.7)); tr.gravity = Vector2.ZERO
		tr.initial_velocity_max = 10; tr.material = add_mat; s.add_child(tr)
		var t := s.create_tween()
		t.tween_method(func(k: float): s.position = pos + Vector2.RIGHT.rotated(phase + k * PI * 1.5) * radius * (1 - k), 0.0, 1.0, dur).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN)
		t.tween_callback(func(): tr.emitting = false)
		t.tween_property(s, "modulate:a", 0.0, 0.15)

func summon(rig: Node2D, node: Node2D, sword: String, slot: Vector2):
	var col: Color = COLORS[sword]
	var fx := fxroot(rig, slot, 0.9, 2); var back := fxroot(rig, slot, 0.9, -1)
	match sword:
		"tyrfing":
			pose(node, slot + Vector2(0, 30), 0.2, 0, Color(1.8, 0.9, 0.5, 0.4))
			flare(back, Vector2.ZERO, col, 260, 0.45)
			burst(fx, Vector2.ZERO, ramp(Color(1, 0.85, 0.4), Color(1, 0.3, 0.08)), "fx_spark", 40, 0.55, 150, 420, Vector2(0, -320), 180, 0.25, 0.55, true, {"radius": 20, "alignY": true})
			burst(back, Vector2(0, -10), ramp(Color(0.22, 0.12, 0.1, 0.5)), "fx_smoke", 8, 0.6, 30, 90, Vector2(0, -120), 70, 0.5, 0.9, false, {"dir": -90, "radius": 30})
			ring(back, Vector2(0, 95), Color(1, 0.45, 0.15), 30, 170, 0.4, 0, 0.35)
		"skofnung":
			pose(node, slot, 1.6, 0, Color(0.7, 0.9, 1, 0))
			burst(back, Vector2.ZERO, ramp(Color(0.75, 0.9, 1, 0.55)), "fx_smoke", 18, 0.45, 0, 10, Vector2.ZERO, 180, 0.5, 0.9, true, {"circle": 170, "radial": -1400})
			burst(fx, Vector2.ZERO, ramp(Color(0.85, 0.95, 1, 0.8)), "fx_glow", 14, 0.4, 0, 0, Vector2.ZERO, 180, 0.08, 0.16, true, {"circle": 130, "radial": -1100})
			flare(back, Vector2.ZERO, col, 190, 0.35, 0.25)
		"durandal":
			pose(node, slot + Vector2(0, -340), 1.15, 0, Color(1.3, 1.25, 1.1, 0.3))
			pillar(back, Vector2(0, -120), Color(1, 0.92, 0.65), 560, 110, 0.55)
			burst(fx, Vector2(0, -300), ramp(Color(1, 0.95, 0.75)), "fx_glow", 16, 0.5, 20, 60, Vector2(0, 420), 25, 0.06, 0.12, true, {"dir": 90, "rect": Vector2(45, 30)})
			ring(back, Vector2(0, 100), Color(1, 0.9, 0.6), 40, 160, 0.4, 0.25, 0.3)
			flare(fx, Vector2.ZERO, col, 200, 0.3, 0.27)
		"kusanagi":
			pose(node, slot + Vector2(-60, 40), 0.5, -TAU, Color.WHITE)
			burst(fx, Vector2.ZERO, ramp(Color(0.5, 0.85, 0.45), Color(0.75, 0.95, 0.5)), "fx_leaf", 22, 0.55, 60, 90, Vector2.ZERO, 180, 0.5, 0.9, false, {"circle": 95, "radial": -60, "tangential": 700, "spin": 360})
			arc(back, Vector2.ZERO, 115, 200, 520, Color(0.6, 0.95, 0.65, 0.75), 9, 0.45)
			arc(back, Vector2(10, -20), 80, 30, 330, Color(0.75, 1, 0.75, 0.6), 6, 0.4, false, 0.06)
		"onimaru":
			pose(node, slot + Vector2(-25, 25), 1.15, 0, Color(0.45, 0.35, 0.55, 0))
			stroke(back, Vector2(-170, 110), -0.62, Color(0.1, 0.05, 0.14, 0.95), 400, 90, 0.55, false)
			stroke(fx, Vector2(-160, 100), -0.62, Color(0.65, 0.45, 0.95, 0.55), 380, 26, 0.4, true, 0.03)
			burst(back, Vector2(60, -40), ramp(Color(0.12, 0.06, 0.16, 0.9)), "fx_ink", 10, 0.5, 120, 300, Vector2(0, 500), 50, 0.1, 0.25, false, {"dir": -40})
		"dainsleif":
			pose(node, slot + Vector2(0, -140), Vector2(0.5, 1.4), 0, Color(0.9, 0.3, 0.35, 0.3))
			flare(back, Vector2.ZERO, Color(0.6, 0.08, 0.14), 170, 0.5)
			burst(fx, Vector2(0, 10), ramp(Color(0.55, 0.03, 0.08, 0.95)), "fx_drop", 12, 0.5, 20, 80, Vector2(0, 900), 10, 0.3, 0.5, false, {"dir": 90, "rect": Vector2(8, 70), "alignY": true, "delay": 0.12})
			blot(back, Vector2(0, 105), Color(0.4, 0.02, 0.06, 0.85), 30, 120, 0.55, 0.2)
		"claiomhsolais":
			pose(node, slot, 1.4, 0, Color(2, 2, 1.6, 0))
			flare(back, Vector2.ZERO, Color(1, 0.97, 0.8), 300, 0.35)
			rays(fx, Vector2.ZERO, Color(1, 0.95, 0.7), 10, 230, 0.45)
			burst(fx, Vector2.ZERO, ramp(Color(1, 1, 0.85)), "fx_glow", 16, 0.5, 80, 220, Vector2.ZERO, 180, 0.05, 0.1, true)
			ring(back, Vector2.ZERO, Color(1, 0.95, 0.7), 30, 190, 0.4)
		"caladbolg":
			pose(node, slot + Vector2(-270, 60), 0.6, -1.2, Color.WHITE)
			arc(back, Vector2(-135, 40), 135, 180, 360, Color.WHITE, 16, 0.6, true)
			burst(fx, Vector2.ZERO, ramp(Color(0.6, 1, 1), Color(0.9, 0.7, 1)), "fx_glow", 18, 0.5, 60, 200, Vector2.ZERO, 180, 0.05, 0.1, true, {"delay": 0.25})
			ring(back, Vector2.ZERO, col, 30, 160, 0.35, 0.25)
		"gram":
			pose(node, slot, 1.0, 0, Color(1.5, 1.3, 0.8, 0))
			converge(fx, Vector2.ZERO, Color(1, 0.85, 0.5), 7, 160, 0.22)
			flare(back, Vector2.ZERO, col, 230, 0.35, 0.22)
			ring(back, Vector2.ZERO, col, 30, 170, 0.35, 0.22)
			burst(fx, Vector2.ZERO, ramp(Color(1, 0.9, 0.5), Color(1, 0.5, 0.15)), "fx_spark", 30, 0.45, 200, 450, Vector2(0, 600), 180, 0.2, 0.45, true, {"alignY": true, "delay": 0.22})
		"ganjiang", "moye":
			pose(node, slot, 0.5, 0, Color(1, 1, 1, 0))
			twin(fx, Vector2.ZERO, 130, 0.3, sword == "moye")
			flare(back, Vector2.ZERO, Color(0.85, 0.6, 0.95), 220, 0.3, 0.26)
			ring(back, Vector2.ZERO, COLORS["ganjiang"], 30, 150, 0.3, 0.26)
			ring(back, Vector2.ZERO, COLORS["moye"], 20, 120, 0.3, 0.29)

func switch_fx(rig: Node2D, sword: String, pos: Vector2):
	var col: Color = COLORS[sword]
	var fx := fxroot(rig, pos, 0.8, 2); var back := fxroot(rig, pos, 0.8, -1)
	var d := 0.12
	ring(back, Vector2.ZERO, col, 25, 125, 0.35, d)
	flare(back, Vector2.ZERO, col, 150, 0.3, d)
	match sword:
		"tyrfing": burst(fx, Vector2.ZERO, ramp(Color(1, 0.85, 0.4), Color(1, 0.3, 0.08)), "fx_spark", 16, 0.45, 120, 300, Vector2(0, -300), 180, 0.2, 0.4, true, {"alignY": true, "delay": d})
		"skofnung": burst(back, Vector2.ZERO, ramp(Color(0.75, 0.9, 1, 0.45)), "fx_smoke", 8, 0.45, 60, 120, Vector2.ZERO, 180, 0.4, 0.7, true, {"delay": d})
		"durandal": pillar(back, Vector2(0, -80), Color(1, 0.92, 0.65), 340, 70, 0.4, d - 0.05)
		"kusanagi":
			burst(fx, Vector2.ZERO, ramp(Color(0.5, 0.85, 0.45)), "fx_leaf", 10, 0.45, 60, 90, Vector2.ZERO, 180, 0.4, 0.7, false, {"circle": 70, "tangential": 600, "spin": 360, "delay": d})
			arc(back, Vector2.ZERO, 90, 160, 460, Color(0.6, 0.95, 0.65, 0.7), 7, 0.35, false, d)
		"onimaru": stroke(back, Vector2(-110, 70), -0.62, Color(0.1, 0.05, 0.14, 0.9), 240, 55, 0.4, false, d - 0.06)
		"dainsleif": burst(fx, Vector2(0, 10), ramp(Color(0.55, 0.03, 0.08, 0.95)), "fx_drop", 6, 0.45, 20, 60, Vector2(0, 900), 10, 0.25, 0.4, false, {"dir": 90, "rect": Vector2(8, 60), "alignY": true, "delay": d})
		"claiomhsolais": rays(fx, Vector2.ZERO, Color(1, 0.95, 0.7), 6, 160, 0.35, d)
		"caladbolg": arc(back, Vector2.ZERO, 100, -90, 270, Color.WHITE, 8, 0.4, true, d)
		"gram": burst(fx, Vector2.ZERO, ramp(Color(1, 0.9, 0.5), Color(1, 0.5, 0.15)), "fx_spark", 14, 0.4, 150, 350, Vector2(0, 600), 180, 0.2, 0.35, true, {"alignY": true, "delay": d})
		"ganjiang", "moye":
			var other := "ganjiang" if sword == "moye" else "moye"
			flare(fx, Vector2(-45, 10), COLORS["ganjiang"], 100, 0.35, d)
			flare(fx, Vector2(45, -10), COLORS["moye"], 100, 0.35, d)
			ring(back, Vector2.ZERO, COLORS[other], 20, 95, 0.35, d + 0.05)

func play_impact(fx: Node2D, sword: String):
	var rot := randf_range(-0.5, 0.5)
	var col: Color = COLORS[sword]
	match sword:
		"tyrfing":
			flare(fx, Vector2.ZERO, col, 230, 0.4)
			burst(fx, Vector2.ZERO, ramp(Color(1, 0.85, 0.4), Color(1, 0.3, 0.08)), "fx_spark", 30, 0.5, 200, 480, Vector2(0, -250), 180, 0.25, 0.5, true, {"alignY": true})
			ring(fx, Vector2.ZERO, Color(1, 0.45, 0.15), 30, 170, 0.35)
		"skofnung":
			stroke(fx, Vector2(-190, 0).rotated(rot - 0.5), rot - 0.5, Color(0.7, 0.9, 1, 0.8), 380, 50, 0.35, true)
			burst(fx, Vector2.ZERO, ramp(Color(0.75, 0.9, 1, 0.22)), "fx_smoke", 6, 0.5, 60, 140, Vector2.ZERO, 180, 0.5, 0.8, true)
			burst(fx, Vector2.ZERO, ramp(Color(0.85, 0.95, 1)), "fx_shard", 10, 0.4, 150, 320, Vector2(0, 400), 180, 0.25, 0.45, true, {"alignY": true})
		"durandal":
			beam(fx, Vector2.ZERO, 0, Color(1, 0.95, 0.75), 330, 26, 0.4)
			beam(fx, Vector2.ZERO, PI / 2, Color(1, 0.95, 0.75), 230, 22, 0.4, 0.03)
			flare(fx, Vector2.ZERO, Color(1, 0.92, 0.65), 200, 0.35)
		"kusanagi":
			for k in 3: slash(fx, Vector2.ZERO, rot + k * TAU / 3, Color(0.55, 0.95, 0.6, 0.6), 0.75, 0.32, k * 0.04)
			burst(fx, Vector2.ZERO, ramp(Color(0.5, 0.85, 0.45)), "fx_leaf", 14, 0.5, 150, 320, Vector2(0, 300), 180, 0.4, 0.7, false, {"spin": 540})
		"onimaru":
			stroke(fx, Vector2(-170, 0).rotated(0.7), 0.7, Color(0.1, 0.05, 0.14, 0.92), 340, 60, 0.45, false)
			stroke(fx, Vector2(-170, 0).rotated(-0.7), -0.7, Color(0.1, 0.05, 0.14, 0.92), 340, 60, 0.45, false, 0.06)
			stroke(fx, Vector2(-160, 0).rotated(0.7), 0.7, Color(0.65, 0.45, 0.95, 0.5), 320, 16, 0.3, true)
			burst(fx, Vector2.ZERO, ramp(Color(0.12, 0.06, 0.16, 0.9)), "fx_ink", 8, 0.45, 150, 320, Vector2(0, 500), 180, 0.1, 0.22, false)
		"dainsleif":
			stroke(fx, Vector2(-180, 0).rotated(rot + 0.4), rot + 0.4, Color(0.75, 0.12, 0.18, 0.85), 360, 40, 0.35, true)
			blot(fx, Vector2(10, 10), Color(0.45, 0.02, 0.07, 0.85), 30, 130, 0.5, 0, 1.0)
			burst(fx, Vector2.ZERO, ramp(Color(0.55, 0.03, 0.08, 0.95)), "fx_drop", 14, 0.5, 150, 380, Vector2(0, 900), 180, 0.25, 0.45, false, {"alignY": true})
		"claiomhsolais":
			rays(fx, Vector2.ZERO, Color(1, 0.95, 0.7), 8, 220, 0.35)
			flare(fx, Vector2.ZERO, Color(1, 0.97, 0.8), 200, 0.3)
		"caladbolg":
			arc(fx, Vector2.ZERO, 70, -90, 270, Color.WHITE, 12, 0.4, true, 0, 2.2)
			burst(fx, Vector2.ZERO, ramp(Color(0.6, 1, 1), Color(0.9, 0.7, 1)), "fx_glow", 16, 0.45, 120, 300, Vector2.ZERO, 180, 0.05, 0.1, true)
		"gram":
			ring(fx, Vector2.ZERO, col, 40, 230, 0.4)
			ring(fx, Vector2.ZERO, Color(1, 0.7, 0.3), 20, 150, 0.35, 0.05)
			flare(fx, Vector2.ZERO, col, 220, 0.35)
			burst(fx, Vector2.ZERO, ramp(Color(1, 0.9, 0.5), Color(1, 0.5, 0.15)), "fx_spark", 24, 0.45, 250, 520, Vector2(0, 700), 180, 0.25, 0.5, true, {"alignY": true})
		"ganjiang", "moye":
			stroke(fx, Vector2(-170, 0).rotated(0.55), 0.55, COLORS["ganjiang"], 340, 36, 0.35, true)
			stroke(fx, Vector2(-170, 0).rotated(-0.55), -0.55, COLORS["moye"], 340, 36, 0.35, true, 0.05)
			flare(fx, Vector2.ZERO, Color(0.85, 0.6, 0.95), 170, 0.3, 0.05)

# ------------------------------------------------------------------ BossKillCutIn.Build mirror

func cutin(root_c: Control, size: Vector2, sword: String, col: Color, name: String):
	var center := size / 2; var tilt := -0.13
	var dim := ColorRect.new(); dim.color = Color(0, 0, 0, 0); dim.mouse_filter = Control.MOUSE_FILTER_IGNORE
	dim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT); root_c.add_child(dim)
	var td := dim.create_tween(); td.tween_property(dim, "color:a", 0.55, 0.15); td.tween_interval(0.8); td.tween_property(dim, "color:a", 0.0, 0.25)
	var stage := Node2D.new(); stage.position = center; stage.rotation = tilt; root_c.add_child(stage)
	var brush: Texture2D = load(R + "/images/vfx/fx/fx_brush.png")
	var band_len := size.x * 1.35
	var band := func(c: Color, thick: float, additive: bool, delay: float):
		var b := Sprite2D.new(); b.texture = brush; b.centered = false; b.offset = Vector2(0, -96); b.position = Vector2(-band_len * 0.5, 0)
		b.scale = Vector2(0.01, thick / 192.0 * 1.6); b.modulate = cola(c, 0)
		if additive: b.material = add_mat
		stage.add_child(b)
		var tb := b.create_tween()
		if delay > 0: tb.tween_interval(delay)
		tb.tween_property(b, "modulate:a", c.a, 0.03)
		tb.parallel().tween_property(b, "scale:x", band_len / 1024.0, 0.26).set_trans(Tween.TRANS_EXPO).set_ease(Tween.EASE_OUT)
	band.call(cola(col.darkened(0.72), 0.93), 250, false, 0.04)
	band.call(cola(col, 0.38), 120, true, 0.08)
	var swords := [sword]
	if sword == "ganjiang" or sword == "moye": swords.append("moye" if sword == "ganjiang" else "ganjiang")
	for i in swords.size():
		var tex: Texture2D = load(R + "/images/swords/%s.png" % swords[i])
		var k := size.y * 0.62 / tex.get_height()
		var y := -8.0 if i == 0 else 70.0
		var delay := 0.1 + i * 0.06
		var from := Vector2(-size.x * 0.62, y); var to := Vector2(size.x * 0.06 - i * 120, y)
		var sp := Sprite2D.new(); sp.texture = tex; sp.rotation = PI / 2; sp.scale = Vector2(k, k); sp.position = from
		sp.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR; sp.modulate = Color(1, 1, 1, 0); stage.add_child(sp)
		var ts := sp.create_tween(); ts.tween_interval(delay); ts.tween_property(sp, "modulate:a", 1.0, 0.02)
		ts.parallel().tween_property(sp, "position", to, 0.28).set_trans(Tween.TRANS_EXPO).set_ease(Tween.EASE_OUT)
		ts.tween_property(sp, "position", to + Vector2(60, 0), 0.75)
		for g in range(1, 6):
			var gh := Sprite2D.new(); gh.texture = tex; gh.rotation = PI / 2; gh.scale = Vector2(k, k); gh.material = add_mat
			gh.position = from.lerp(to, 1.0 - g * 0.16); gh.modulate = cola(col.lightened(0.3), 0); stage.add_child(gh)
			var tg := gh.create_tween(); tg.tween_interval(delay + 0.05 + g * 0.012)
			tg.tween_property(gh, "modulate:a", 0.45 - g * 0.07, 0.02); tg.tween_property(gh, "modulate:a", 0.0, 0.22)
	var spark: Texture2D = load(R + "/images/vfx/fx/fx_spark.png")
	var cut := Sprite2D.new(); cut.texture = spark; cut.rotation = PI / 2; cut.material = add_mat; cut.position = Vector2(0, -8)
	cut.scale = Vector2(0.5, 0.05); cut.modulate = cola(col.lightened(0.55), 0); stage.add_child(cut)
	var tc := cut.create_tween(); tc.tween_interval(0.22); tc.tween_property(cut, "modulate:a", 0.95, 0.03)
	tc.parallel().tween_property(cut, "scale:y", size.x * 1.1 / 64.0, 0.14).set_trans(Tween.TRANS_EXPO).set_ease(Tween.EASE_OUT)
	tc.tween_property(cut, "modulate:a", 0.0, 0.35); tc.parallel().tween_property(cut, "scale:x", 0.12, 0.35)
	var sparks := CPUParticles2D.new(); sparks.position = Vector2(0, -8); sparks.amount = 50; sparks.lifetime = 0.6; sparks.one_shot = true
	sparks.explosiveness = 0.8; sparks.texture = spark; sparks.emission_shape = CPUParticles2D.EMISSION_SHAPE_RECTANGLE
	sparks.emission_rect_extents = Vector2(size.x * 0.4, 6); sparks.direction = Vector2(1, -0.3); sparks.spread = 35
	sparks.initial_velocity_min = 300; sparks.initial_velocity_max = 800; sparks.gravity = Vector2(0, 500)
	sparks.scale_amount_min = 0.3; sparks.scale_amount_max = 0.7; sparks.material = add_mat; sparks.emitting = false
	var g2 := Gradient.new(); g2.set_color(0, Color(1, 0.97, 0.9)); g2.set_color(1, cola(col, 0)); g2.add_point(0.4, col.lightened(0.3)); sparks.color_ramp = g2
	sparks.set_particle_flag(CPUParticles2D.PARTICLE_FLAG_ALIGN_Y_TO_VELOCITY, true); stage.add_child(sparks)
	var tsp := sparks.create_tween(); tsp.tween_interval(0.24); tsp.tween_callback(func(): sparks.emitting = true)
	var label := Label.new(); label.text = name; label.mouse_filter = Control.MOUSE_FILTER_IGNORE; label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	label.size = Vector2(900, 120); label.position = Vector2(size.x * 0.5 - 900 - size.x * 0.06 + 40, 80); label.modulate = Color(1, 1, 1, 0)
	label.add_theme_font_size_override("font_size", 84); label.add_theme_color_override("font_color", col.lerp(Color.WHITE, 0.8))
	label.add_theme_color_override("font_outline_color", Color(0.05, 0.03, 0.06)); label.add_theme_constant_override("outline_size", 14)
	label.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.6)); label.add_theme_constant_override("shadow_offset_x", 4)
	label.add_theme_constant_override("shadow_offset_y", 5)
	stage.add_child(label)
	var tl := label.create_tween(); tl.tween_interval(0.3); tl.tween_property(label, "modulate:a", 1.0, 0.15)
	tl.parallel().tween_property(label, "position:x", label.position.x - 40, 0.6).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	var tf := stage.create_tween(); tf.tween_interval(0.92); tf.tween_property(stage, "modulate:a", 0.0, 0.26).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
