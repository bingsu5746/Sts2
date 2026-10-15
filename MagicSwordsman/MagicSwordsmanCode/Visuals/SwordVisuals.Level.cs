using Godot;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Entities.Players;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// How a floating sword looks by its level (user request 2026-10-15 "검 강화 단계별로 ... 확연한 차이"), the broken Gram
/// ("그람 조각나 있는 건 좋은데 ... 엉성하고, aura는 그냥 검처럼") and Claíomh Solais answering its Light ("빛 스택 쌓을
/// 때마다 검이 더 빛나거나 이펙트 추가 ... 각 스택마다 다른 이펙트로").
///
/// Level, readable at a glance and still restrained:
///  0 — dull, greyed and a little smaller; no aura at all.
///  1–2 — natural colour, a faint violet aura breathing slowly (2: the engravings begin to glow).
///  3–4 — full size and colour, a strong aura shading from violet into the sword's own colour, glowing engravings,
///        drifting motes (4: a second, wider aura and a light running up the blade now and then).
///  5 — larger, slightly over-bright; a majestic two-layer aura (sword colour + pale gold), rising sparks, a slow ring
///      of light behind the blade and the running light more often.
/// The summon (SwordFx.Summon), the strike trail (<see cref="Trail"/>) and the impact (SwordFx.PlayImpact) scale with the
/// same level. Node layout under the sword's "Blade": [Decor (Node2D, behind)] [Aura (Sprite2D)] [the sword sprite(s)]
/// [DecorFront (Node2D)]. Only the sword sprites (and "Aura") are direct Sprite2D children, because the blade copies
/// (trail, ghosts, the death's stuck swords) copy exactly those.
/// Presentation only; every entry point swallows its errors.
/// </summary>
public static partial class SwordVisuals
{
    /// <summary>The look of one level: blade size/tint, engraving glow, aura (alpha range, scale, colour), motes.</summary>
    private readonly record struct LevelLook(
        float Size, Color Tint, float GlowLo, float GlowHi, float AuraLo, float AuraHi, float AuraScale, float AuraHue,
        float OuterLo, float OuterHi, double Breath, int Motes, double Shimmer);

    private static LevelLook LookOf(int level) => Math.Clamp(level, 0, 5) switch
    {
        0 => new(0.9f, new Color(0.6f, 0.59f, 0.64f), 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 2.0, 0, 0),
        1 => new(0.95f, new Color(0.8f, 0.79f, 0.84f), 0f, 0f, 0.06f, 0.13f, 1f, 0f, 0f, 0f, 2.0, 0, 0),
        2 => new(0.98f, new Color(0.9f, 0.89f, 0.93f), 0.03f, 0.08f, 0.1f, 0.19f, 1.02f, 0.15f, 0f, 0f, 1.9, 0, 0),
        3 => new(1.02f, Colors.White, 0.08f, 0.17f, 0.24f, 0.4f, 1.06f, 0.5f, 0f, 0f, 1.6, 9, 0),
        4 => new(1.06f, Colors.White, 0.12f, 0.24f, 0.26f, 0.42f, 1.1f, 0.7f, 0.08f, 0.16f, 1.4, 14, 4.2),
        _ => new(1.12f, new Color(1.08f, 1.06f, 1.02f), 0.2f, 0.36f, 0.36f, 0.56f, 1.14f, 0.9f, 0.16f, 0.3f, 1.0, 20, 2.6),
    };

    /// <summary>The level aura's colour: violet at low levels, the sword's own colour at high ones.</summary>
    private static Color AuraTint(SwordId sword, float hue) => AuraColor.Lerp(ColorOf(sword).Lightened(0.15f), hue);

    /// <summary>Builds the sword's art under <paramref name="blade"/> for <paramref name="level"/> (see the class summary).</summary>
    private static void BuildSwordArt(Node2D blade, SwordId sword, int level, Texture2D tex, float scale, Player player)
    {
        var look = LookOf(level);
        var id = sword.ToString().ToLowerInvariant();
        blade.Scale = Vector2.One * look.Size;
        var decor = new Node2D { Name = "Decor" };
        blade.AddChild(decor);

        // the level aura (none at 0); a broken Gram has its own shard-shaped glow instead (AddBrokenGram)
        var broken = sword == SwordId.Gram && level <= 0;
        if (look.AuraHi > 0f && !broken)
        {
            var auraPath = $"{MainFile.ResPath}/images/swords/aura/{id}.png";
            if (ResourceLoader.Exists(auraPath))
            {
                var auraTex = GD.Load<Texture2D>(auraPath);
                var aura = AuraSprite(auraTex, scale * look.AuraScale, AuraTint(sword, look.AuraHue), look.AuraLo);
                aura.Name = "Aura";
                blade.AddChild(aura);
                Breathe(aura, look.AuraLo, look.AuraHi, look.Breath);
                if (look.OuterHi > 0f)
                {
                    // a second, wider and softer aura: gold-white at 5, the sword's colour at 4
                    var outerCol = level >= 5 ? new Color(1f, 0.9f, 0.62f) : ColorOf(sword).Lightened(0.3f);
                    var outer = AuraSprite(auraTex, scale * (look.AuraScale + 0.12f + 0.04f * (level - 4)), outerCol, look.OuterLo);
                    decor.AddChild(outer);
                    Breathe(outer, look.OuterLo, look.OuterHi, look.Breath * 1.37);
                }
            }
        }

        if (level >= 5) AddCrownRing(decor, sword);

        Sprite2D main;
        if (broken) main = AddBrokenGram(blade, decor, tex, scale, look.Tint);
        else
        {
            main = new Sprite2D { Texture = tex, TextureFilter = CanvasItem.TextureFilterEnum.Linear, Scale = new Vector2(scale, scale), Modulate = look.Tint };
            blade.AddChild(main);
        }

        var front = new Node2D { Name = "DecorFront" };
        blade.AddChild(front);
        if (look.GlowHi > 0f)
        {
            // the engravings glow: an additive copy of the sprite in the sword's colour (dark steel adds little, the bright
            // inlays a lot), breathing a little out of step with the aura
            var glow = new Sprite2D
            {
                Texture = tex, TextureFilter = CanvasItem.TextureFilterEnum.Linear, Scale = new Vector2(scale, scale),
                Material = AddMat(), Modulate = new Color(ColorOf(sword).Lightened(0.2f), look.GlowLo),
            };
            front.AddChild(glow);
            Breathe(glow, look.GlowLo, look.GlowHi, look.Breath * 0.8);
        }
        if (look.Shimmer > 0 && !broken) AddShimmer(main, look.Shimmer, level);
        if (look.Motes > 0) AddMotes(front, sword, tex, scale, level, look.Motes);
        if (sword == SwordId.ClaiomhSolais) AttachSolaisLight(blade, player);
    }

    private static CanvasItemMaterial AddMat() => new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

    private static Sprite2D AuraSprite(Texture2D tex, float scale, Color col, float alpha) => new()
    {
        Texture = tex, Scale = new Vector2(scale, scale), Material = AddMat(), Modulate = new Color(col, alpha),
    };

    /// <summary>Alpha breathing between lo and hi, half a breath every <paramref name="half"/> s.</summary>
    private static void Breathe(CanvasItem item, float lo, float hi, double half)
    {
        var t = item.CreateTween().SetLoops();
        t.TweenProperty(item, "modulate:a", hi, half).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        t.TweenProperty(item, "modulate:a", lo, half).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    /// <summary>Level 5: a thin ring of light standing behind the blade, turning slowly and breathing.</summary>
    private static void AddCrownRing(Node2D decor, SwordId sword)
    {
        if (SwordFxTex("fx_ring") is not { } ringTex) return;
        var ring = new Sprite2D
        {
            Texture = ringTex, Material = AddMat(), Position = new Vector2(0, -30),
            Scale = new Vector2(0.5f, 0.5f), Modulate = new Color(ColorOf(sword).Lerp(new Color(1f, 0.9f, 0.6f), 0.5f), 0.12f),
        };
        decor.AddChild(ring);
        var spin = ring.CreateTween().SetLoops();
        spin.TweenProperty(ring, "rotation", Mathf.Tau, 14.0).From(0f);
        Breathe(ring, 0.07f, 0.17f, 1.3);
    }

    /// <summary>
    /// Level 4+: a band of light runs up the blade every few seconds. The sword sprite clips it to its own silhouette
    /// (ClipChildren), so the light stays on the steel.
    /// </summary>
    private static void AddShimmer(Sprite2D main, double every, int level)
    {
        if (SwordFxTex("fx_glow") is not { } glowTex || main.Texture is not { } tex) return;
        main.ClipChildren = CanvasItem.ClipChildrenMode.AndDraw;
        float h = tex.GetHeight(), w = tex.GetWidth();
        var band = new Sprite2D
        {
            Texture = glowTex, Material = AddMat(), Modulate = new Color(1f, 0.97f, 0.88f, level >= 5 ? 0.85f : 0.6f),
            Scale = new Vector2(w * 1.6f / 128f, h * 0.12f / 128f), Rotation = -0.35f, Position = new Vector2(0, h * 0.6f),
        };
        main.AddChild(band);
        var t = band.CreateTween().SetLoops();
        t.TweenInterval(every * (0.6 + GD.Randf() * 0.5));
        t.TweenProperty(band, "position:y", -h * 0.6f, 0.9).From(h * 0.6f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    /// <summary>Level 3+: motes of the sword's colour drifting up from the blade (level 5: sparks among them).</summary>
    private static void AddMotes(Node2D front, SwordId sword, Texture2D tex, float scale, int level, int amount)
    {
        var motes = new CpuParticles2D
        {
            Amount = amount, Lifetime = 1.8, Preprocess = 1.8,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(tex.GetWidth() * scale * 0.3f, tex.GetHeight() * scale * 0.45f),
            Gravity = new Vector2(0, level >= 5 ? -34 : -18), InitialVelocityMin = 2, InitialVelocityMax = 10,
            ScaleAmountMin = 0.035f, ScaleAmountMax = level >= 5 ? 0.09f : 0.07f, Color = ColorOf(sword).Lightened(0.3f),
            Material = AddMat(),
        };
        var core = $"{MainFile.ResPath}/images/vfx/hand_core.png";
        if (ResourceLoader.Exists(core)) motes.Texture = GD.Load<Texture2D>(core);
        front.AddChild(motes);
        if (level < 5 || SwordFxTex("fx_spark") is not { } spark) return;
        var sparks = new CpuParticles2D
        {
            Amount = 6, Lifetime = 1.1, Preprocess = 1.1, Texture = spark, Material = AddMat(),
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(tex.GetWidth() * scale * 0.25f, tex.GetHeight() * scale * 0.4f),
            Gravity = new Vector2(0, -60), InitialVelocityMin = 10, InitialVelocityMax = 30, Direction = Vector2.Up, Spread = 10,
            ScaleAmountMin = 0.18f, ScaleAmountMax = 0.3f, Color = new Color(1f, 0.93f, 0.7f, 0.8f),
        };
        sparks.SetParticleFlag(CpuParticles2D.ParticleFlags.AlignYToVelocity, true);
        front.AddChild(sparks);
    }

    private static Texture2D? SwordFxTex(string name)
    {
        var path = $"{MainFile.ResPath}/images/vfx/fx/{name}.png";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    // ------------------------------------------------------------------ the broken Gram (level 0)

    /// <summary>Middle of Gram's break line in texture px (tools/gen_gram_shards.py BREAK_PIVOT).</summary>
    private static readonly Vector2 BrokenPivot = new(127, 352);

    /// <summary>
    /// "부서진 그람": the two pieces cut along one jagged break (images/swords/broken, tools/gen_gram_shards.py), the point
    /// held a little above the rest with a narrow gap and a faint gold glow between them, a few embers drifting out of
    /// the gap, and the point drifting slowly on its own rhythm (it never quite lines up). Each piece carries its own
    /// shard-shaped aura (a dim forge-gold, behind it). Returns the lower piece (the "main" sprite).
    /// </summary>
    private static Sprite2D AddBrokenGram(Node2D blade, Node2D decor, Texture2D whole, float scale, Color tint)
    {
        var dir = $"{MainFile.ResPath}/images/swords/broken";
        Texture2D? Load(string n) => ResourceLoader.Exists($"{dir}/{n}.png") ? GD.Load<Texture2D>($"{dir}/{n}.png") : null;
        var topTex = Load("gram_top");
        var bottomTex = Load("gram_bottom");
        if (topTex == null || bottomTex == null)
        {
            var plain = new Sprite2D { Texture = whole, Scale = new Vector2(scale, scale), Modulate = tint, TextureFilter = CanvasItem.TextureFilterEnum.Linear };
            blade.AddChild(plain);
            return plain;
        }
        var ember = new Color(1f, 0.68f, 0.3f);
        Sprite2D Piece(Texture2D t, string auraName)
        {
            var s = new Sprite2D { Texture = t, Scale = new Vector2(scale, scale), Modulate = tint, TextureFilter = CanvasItem.TextureFilterEnum.Linear };
            if (Load(auraName) is { } at)
            {
                var a = new Sprite2D { Texture = at, Material = AddMat(), Modulate = new Color(ember, 0.18f), ShowBehindParent = true, Name = "ShardAura" };
                s.AddChild(a);
                Breathe(a, 0.14f, 0.28f, 1.7);
            }
            return s;
        }
        var bottom = Piece(bottomTex, "aura_gram_bottom");
        var top = Piece(topTex, "aura_gram_top");
        blade.AddChild(bottom);
        blade.AddChild(top);

        // where the break is, in blade space (the sprites are centred on the texture)
        var center = new Vector2(whole.GetWidth(), whole.GetHeight()) / 2f;
        var pivot = (BrokenPivot - center) * scale;
        Sprite2D? seam = null;
        if (Load("gram_seam") is { } st)
        {
            seam = new Sprite2D { Texture = st, Material = AddMat(), Scale = new Vector2(scale, scale), Position = pivot + new Vector2(0, -4), Modulate = new Color(1f, 0.75f, 0.35f, 0.5f) };
            decor.AddChild(seam);
            Breathe(seam, 0.4f, 0.8f, 1.3);
        }
        var embers = new CpuParticles2D
        {
            Amount = 5, Lifetime = 1.4, Preprocess = 1.4, Position = pivot + new Vector2(0, -4), Material = AddMat(),
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle, EmissionRectExtents = new Vector2(11, 2),
            Gravity = new Vector2(0, -16), InitialVelocityMin = 3, InitialVelocityMax = 12, Spread = 180,
            ScaleAmountMin = 0.025f, ScaleAmountMax = 0.05f, Color = new Color(1f, 0.72f, 0.35f, 0.8f),
        };
        var core = $"{MainFile.ResPath}/images/vfx/hand_core.png";
        if (ResourceLoader.Exists(core)) embers.Texture = GD.Load<Texture2D>(core);
        decor.AddChild(embers);

        // the point floats free above the break: a gap of ~8 px, tilted a hair, drifting on its own slow rhythm.
        // A sprite turns about its centre, so its position is solved to keep the break point where it should be.
        void Place(float u)
        {
            if (!GodotObject.IsInstanceValid(top)) return;
            var gap = new Vector2(1.5f + 1.5f * Mathf.Sin(u * 0.7f), -12f - 3f * Mathf.Sin(u));
            var rot = 0.045f + 0.03f * Mathf.Sin(u * 0.6f + 1.1f);
            top.Rotation = rot;
            top.Position = pivot + gap - pivot.Rotated(rot);
            if (seam != null && GodotObject.IsInstanceValid(seam)) seam.Position = pivot + gap * 0.5f;
        }
        Place(0f);
        var drift = top.CreateTween().SetLoops();
        drift.TweenMethod(Callable.From<float>(Place), 0f, Mathf.Tau * 10f, 52.0); // 10 slow cycles of the drift per loop
        return bottom;
    }

    // ------------------------------------------------------------------ Claíomh Solais: the Light it holds

    private const string LightMeta = "ms_solais_light";

    /// <summary>
    /// The Light power changed (SolaisLightPower.AfterPowerAmountChanged): refresh Claíomh Solais' layers right away.
    /// A slow poll on the sword (<see cref="AttachSolaisLight"/>) also catches removals and redraws.
    /// </summary>
    public static void OnSolaisLightChanged(Player player)
    {
        try
        {
            if (!Rigs.TryGetValue(player, out var rig) || !GodotObject.IsInstanceValid(rig.Root)) return;
            if (!rig.Swords.TryGetValue(SwordId.ClaiomhSolais, out var node) || !GodotObject.IsInstanceValid(node)) return;
            if (node.GetNodeOrNull<Node2D>("Blade/SolaisLight") is { } root) RefreshSolaisLight(root, LightOf(player));
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordVisuals] Solais light: {e.Message}");
        }
    }

    private static int LightOf(Player player)
    {
        try { return SolaisLight.Get(player); }
        catch (Exception) { return 0; }
    }

    /// <summary>Claíomh Solais' blade sits left of its texture centre (art bbox x 9..298 of 467): its centre line, px.</summary>
    private const float SolaisAxisX = -13.5f;

    private static void AttachSolaisLight(Node2D blade, Player player)
    {
        var root = new Node2D { Name = "SolaisLight", Position = new Vector2(SolaisAxisX, 0) };
        blade.AddChild(root);
        blade.MoveChild(root, 0); // behind everything; the orbiting motes add their own front layer
        root.SetMeta(LightMeta, 0);
        RefreshSolaisLight(root, LightOf(player), pulse: false);
        var poll = new Godot.Timer { WaitTime = 0.4, Autostart = true, Name = "LightPoll" };
        poll.Timeout += () =>
        {
            try
            {
                if (GodotObject.IsInstanceValid(root)) RefreshSolaisLight(root, LightOf(player));
            }
            catch (Exception) { /* presentation only */ }
        };
        root.AddChild(poll);
    }

    /// <summary>
    /// One layer per Light, stacking (user request: "각 스택마다 다른 이펙트로"): 1 a soft glow, 2 a halo ring about the
    /// upper blade, 3 rays turning slowly behind it, 4 motes of light orbiting it, 5+ a standing pillar of light and a
    /// crown of short rays over the point. Rebuilt when the count changes; a gain flashes once.
    /// </summary>
    private static void RefreshSolaisLight(Node2D root, int light, bool pulse = true)
    {
        var old = root.HasMeta(LightMeta) ? (int)root.GetMeta(LightMeta) : 0;
        if (old == light && root.GetChildCount() > 1) return;
        root.SetMeta(LightMeta, light);
        foreach (var child in root.GetChildren())
            if (child is not Godot.Timer) child.QueueFree();
        if (root.GetParent()?.GetNodeOrNull<Node2D>("SolaisLightFront") is { } oldFront) oldFront.QueueFree();
        if (light <= 0) return;

        var warm = new Color(1f, 0.96f, 0.78f);
        var glowTex = SwordFxTex("fx_glow");
        var sparkTex = SwordFxTex("fx_spark");
        var ringTex = SwordFxTex("fx_ring");
        var pillarTex = SwordFxTex("fx_pillar");
        var bonus = Math.Max(0, light - 5); // 6+: the whole thing a little brighter still

        // 5+: a pillar of light standing through the sword (drawn first: the farthest back)
        if (light >= 5 && pillarTex != null)
        {
            var pillar = new Sprite2D { Texture = pillarTex, Material = AddMat(), Position = new Vector2(0, -60), Scale = new Vector2(0.75f, 1.05f), Modulate = new Color(warm, 0.22f) };
            root.AddChild(pillar);
            Breathe(pillar, 0.16f + 0.02f * bonus, 0.32f + 0.03f * bonus, 1.1);
        }
        // 1: a soft glow behind the blade
        if (glowTex != null)
        {
            var glow = new Sprite2D { Texture = glowTex, Material = AddMat(), Position = new Vector2(0, -20), Scale = new Vector2(1.0f, 1.9f), Modulate = new Color(warm, 0.3f) };
            root.AddChild(glow);
            Breathe(glow, 0.22f + 0.02f * Math.Min(light, 6), 0.4f + 0.03f * Math.Min(light, 6), 1.2);
        }
        // 3: rays turning slowly behind it
        if (light >= 3 && sparkTex != null)
        {
            var rays = new Node2D { Position = new Vector2(0, -25) };
            root.AddChild(rays);
            for (var i = 0; i < 10; i++)
            {
                var a = Mathf.Tau * i / 10;
                var len = i % 2 == 0 ? 120f : 75f;
                rays.AddChild(new Sprite2D
                {
                    Texture = sparkTex, Material = AddMat(), Rotation = a, Position = Vector2.Up.Rotated(a) * len * 0.5f,
                    Scale = new Vector2(0.7f, len / 64f), Modulate = new Color(warm, i % 2 == 0 ? 0.26f : 0.18f),
                });
            }
            var spin = rays.CreateTween().SetLoops();
            spin.TweenProperty(rays, "rotation", Mathf.Tau, 24.0).From(0f);
            Breathe(rays, 0.7f, 1f, 1.5);
        }
        // 2: a halo ring around the upper blade, tilted like a ring seen at an angle
        if (light >= 2 && ringTex != null)
        {
            var halo = new Node2D { Position = new Vector2(0, -52), Scale = new Vector2(1f, 0.32f) };
            root.AddChild(halo);
            var ring = new Sprite2D { Texture = ringTex, Material = AddMat(), Scale = Vector2.One * 0.42f, Modulate = new Color(1f, 0.92f, 0.6f, 0.75f) };
            halo.AddChild(ring);
            var turn = ring.CreateTween().SetLoops();
            turn.TweenProperty(ring, "rotation", Mathf.Tau, 6.0).From(0f);
            var bob = halo.CreateTween().SetLoops();
            bob.TweenProperty(halo, "position:y", -58f, 1.4).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            bob.TweenProperty(halo, "position:y", -48f, 1.4).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }
        // 4: motes of light orbiting the blade (in front when on the near side of the ellipse, dim behind it)
        if (light >= 4 && glowTex != null && root.GetParent() is Node2D blade)
        {
            var front = new Node2D { Name = "SolaisLightFront", Position = root.Position };
            blade.AddChild(front);
            const int n = 4;
            var motes = new Sprite2D[n];
            for (var i = 0; i < n; i++)
            {
                motes[i] = new Sprite2D { Texture = glowTex, Material = AddMat(), Scale = Vector2.One * 0.16f, Modulate = new Color(1f, 0.97f, 0.82f, 0.9f) };
                front.AddChild(motes[i]);
            }
            var orbit = front.CreateTween().SetLoops();
            orbit.TweenMethod(Callable.From<float>(th =>
            {
                for (var i = 0; i < n; i++)
                {
                    if (!GodotObject.IsInstanceValid(motes[i])) return;
                    var a = th + Mathf.Tau * i / n;
                    motes[i].Position = new Vector2(Mathf.Cos(a) * 46f, -20f + Mathf.Sin(a) * 15f - 25f * Mathf.Sin(a * 0.5f + i));
                    var near = Mathf.Sin(a) > 0;
                    motes[i].Modulate = new Color(1f, 0.97f, 0.82f, near ? 0.95f : 0.3f);
                    motes[i].Scale = Vector2.One * (near ? 0.18f : 0.12f);
                }
            }), 0f, Mathf.Tau, 2.6);
        }
        // 5+: a crown of short rays over the point
        if (light >= 5 && sparkTex != null)
        {
            var crown = new Node2D { Position = new Vector2(0, -112) };
            root.AddChild(crown);
            for (var i = -3; i <= 3; i++)
            {
                var a = i * 0.32f;
                var len = i == 0 ? 60f : 44f - Math.Abs(i) * 4f;
                crown.AddChild(new Sprite2D
                {
                    Texture = sparkTex, Material = AddMat(), Rotation = a, Position = Vector2.Up.Rotated(a) * (12f + len * 0.5f),
                    Scale = new Vector2(0.6f, len / 64f), Modulate = new Color(1f, 0.93f, 0.62f, 0.75f),
                });
            }
            Breathe(crown, 0.6f, 1f, 0.9);
        }
        // a gain flashes once
        if (pulse && light > old && glowTex != null)
        {
            var flash = new Sprite2D { Texture = glowTex, Material = AddMat(), Position = new Vector2(0, -20), Scale = Vector2.One * 0.6f, Modulate = new Color(1f, 0.98f, 0.88f, 0.9f) };
            root.AddChild(flash);
            var t = flash.CreateTween().SetParallel();
            t.TweenProperty(flash, "scale", Vector2.One * 2.4f, 0.5).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            t.TweenProperty(flash, "modulate:a", 0f, 0.5);
            t.Chain().TweenCallback(Callable.From(flash.QueueFree));
        }
    }
}
