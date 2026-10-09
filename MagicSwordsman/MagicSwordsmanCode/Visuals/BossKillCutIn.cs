using Godot;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Events;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// Presentation only: a ~1.2 s cut-in when the last boss enemy falls (user request 2026-10-09 "보스 처치 연출"):
/// the screen dims, a dry-brush band in the current sword's colour sweeps across, the sword itself
/// (images/swords/&lt;sword&gt;.png) slashes through along it, a thin cut of light follows and the sword's name appears;
/// then everything fades. It is an overlay on its own CanvasLayer that ignores the mouse and frees itself — nothing
/// awaits it, so the game's own victory flow runs on underneath. Every failure is swallowed.
///
/// Trigger (<see cref="OnDeath"/>, from Mangeomchong.AfterDeath): a primary enemy (not a minion) of a boss encounter
/// (EncounterModel.RoomType == Boss, like CombatManager's own boss checks) died for good and no other primary enemy is
/// still alive; only on the local player's machine, only while the player lives, at most once every 4 s.
/// UNVERIFIED in game: bosses that "die" into a second phase (a new creature spawned after death) would show it early;
/// layout at non-16:9 resolutions; Korean font substitution.
/// </summary>
public static class BossKillCutIn
{
    private const double Duration = 1.2;
    private static ulong _lastShown;

    /// <summary>Called for every death in combat; shows the cut-in when it was the boss fight's final enemy.</summary>
    public static void OnDeath(Player owner, Creature dead, bool wasRemovalPrevented)
    {
        try
        {
            if (wasRemovalPrevented || !dead.IsPrimaryEnemy || owner.Creature.IsDead) return;
            if (!LocalContext.IsMe(owner)) return;
            if (dead.CombatState is not ICombatState combat || combat.Encounter?.RoomType != RoomType.Boss) return;
            if (combat.Enemies.Any(e => e != dead && e.IsAlive && e.IsPrimaryEnemy)) return;
            var now = Time.GetTicksMsec();
            if (_lastShown != 0 && now - _lastShown < 4000) return;
            _lastShown = now;
            Show(SwordFor(owner));
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[BossKillCutIn] {e.Message}");
        }
    }

    /// <summary>The sword shown: the current one, else the last current, else any present / owned one.</summary>
    private static SwordId? SwordFor(Player owner)
    {
        var state = SwordCombat.Get(owner);
        return state?.Current ?? state?.Previous ?? state?.Present.Cast<SwordId?>().FirstOrDefault()
            ?? owner.GetRelic<Mangeomchong>()?.OwnedSwords.Cast<SwordId?>().FirstOrDefault();
    }

    public static void Show(SwordId? sword)
    {
        try { ShowInner(sword); }
        catch (Exception e) { MainFile.Logger.Warn($"[BossKillCutIn] show failed: {e.Message}"); }
    }

    private static void ShowInner(SwordId? sword)
    {
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null) return;
        var col = sword is { } s ? SwordVisuals.ColorOf(s) : new Color(0.85f, 0.65f, 1f);
        Sfx.BossKill();

        var layer = new CanvasLayer { Layer = 115, Name = "MagicSwordBossCutIn" };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);
        tree.Root.AddChild(layer);
        var size = tree.Root.GetVisibleRect().Size;
        if (size.X < 10) size = new Vector2(1920, 1080);

        Build(root, size, sword, col, NameOf(sword), GameFont());

        var t = layer.CreateTween();
        t.TweenInterval(Duration + 0.1);
        t.TweenCallback(Callable.From(layer.QueueFree));
    }

    /// <summary>
    /// The cut-in's nodes and tweens under <paramref name="root"/> (a full-screen Control of <paramref name="size"/>).
    /// tools/fx_preview.gd mirrors this for headless renders — keep the two in step.
    /// </summary>
    internal static void Build(Control root, Vector2 size, SwordId? sword, Color col, string name, Font? font)
    {
        var add = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        var center = size / 2;
        const float tilt = -0.13f;

        // 1) dim
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(dim);
        var td = dim.CreateTween();
        td.TweenProperty(dim, "color:a", 0.55f, 0.15);
        td.TweenInterval(0.8);
        td.TweenProperty(dim, "color:a", 0f, 0.25);

        // everything else lives on a tilted stage through the centre of the screen
        var stage = new Node2D { Position = center, Rotation = tilt };
        root.AddChild(stage);
        var brushTex = Load("images/vfx/fx/fx_brush.png");
        var bandLen = size.X * 1.35f;

        // 2) the brush band: a dark stroke and a coloured sheen on it
        Sprite2D Band(Color c, float thick, bool additive, double delay)
        {
            var b = new Sprite2D
            {
                Texture = brushTex, Centered = false, Offset = new Vector2(0, -96), Position = new Vector2(-bandLen * 0.5f, 0),
                Scale = new Vector2(0.01f, thick / 192f * 1.6f), Modulate = new Color(c, 0f),
            };
            if (additive) b.Material = add;
            stage.AddChild(b);
            var tb = b.CreateTween();
            if (delay > 0) tb.TweenInterval(delay);
            tb.TweenProperty(b, "modulate:a", c.A, 0.03);
            tb.Parallel().TweenProperty(b, "scale:x", bandLen / 1024f, 0.26).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            return b;
        }
        Band(new Color(col.Darkened(0.72f), 0.93f), 250, false, 0.04);
        Band(new Color(col, 0.38f), 120, true, 0.08);

        // 3) the sword(s) slashing through along the band, with afterimages
        var swords = new List<SwordId>();
        if (sword is { } sw)
        {
            swords.Add(sw);
            if (sw is SwordId.Ganjiang or SwordId.Moye) swords.Add(sw == SwordId.Ganjiang ? SwordId.Moye : SwordId.Ganjiang);
        }
        for (var i = 0; i < swords.Count; i++)
        {
            var tex = Load($"images/swords/{swords[i].ToString().ToLowerInvariant()}.png");
            if (tex == null) continue;
            var k = size.Y * 0.62f / tex.GetHeight();
            var y = i == 0 ? -8f : 70f;
            var delay = 0.1 + i * 0.06;
            var from = new Vector2(-size.X * 0.62f, y);
            var to = new Vector2(size.X * 0.06f - i * 120, y);
            var sp = new Sprite2D
            {
                Texture = tex, Rotation = Mathf.Pi / 2, Scale = new Vector2(k, k), Position = from,
                TextureFilter = CanvasItem.TextureFilterEnum.Linear, Modulate = new Color(1, 1, 1, 0),
            };
            stage.AddChild(sp);
            var ts = sp.CreateTween();
            ts.TweenInterval(delay);
            ts.TweenProperty(sp, "modulate:a", 1f, 0.02);
            ts.Parallel().TweenProperty(sp, "position", to, 0.28).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            ts.TweenProperty(sp, "position", to + new Vector2(60, 0), 0.75);
            for (var g = 1; g <= 5; g++) // afterimages strung out behind the fast part of the slash
            {
                var ghost = new Sprite2D
                {
                    Texture = tex, Rotation = Mathf.Pi / 2, Scale = new Vector2(k, k), Material = add,
                    Position = from.Lerp(to, 1f - g * 0.16f), Modulate = new Color(col.Lightened(0.3f), 0f),
                };
                stage.AddChild(ghost);
                var tg = ghost.CreateTween();
                tg.TweenInterval(delay + 0.05 + g * 0.012);
                tg.TweenProperty(ghost, "modulate:a", 0.45f - g * 0.07f, 0.02);
                tg.TweenProperty(ghost, "modulate:a", 0f, 0.22);
            }
        }

        // 4) the cut of light the blade leaves, and a spray of sparks along it
        var spark = Load("images/vfx/fx/fx_spark.png");
        var cut = new Sprite2D
        {
            Texture = spark, Rotation = Mathf.Pi / 2, Material = add, Position = new Vector2(0, -8),
            Scale = new Vector2(0.5f, 0.05f), Modulate = new Color(col.Lightened(0.55f), 0f),
        };
        stage.AddChild(cut);
        var tc = cut.CreateTween();
        tc.TweenInterval(0.22);
        tc.TweenProperty(cut, "modulate:a", 0.95f, 0.03);
        tc.Parallel().TweenProperty(cut, "scale:y", size.X * 1.1f / 64f, 0.14).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tc.TweenProperty(cut, "modulate:a", 0f, 0.35);
        tc.Parallel().TweenProperty(cut, "scale:x", 0.12f, 0.35);

        var sparks = new CpuParticles2D
        {
            Position = new Vector2(0, -8), Amount = 50, Lifetime = 0.6, OneShot = true, Explosiveness = 0.8f, Texture = spark,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle, EmissionRectExtents = new Vector2(size.X * 0.4f, 6),
            Direction = new Vector2(1, -0.3f), Spread = 35, InitialVelocityMin = 300, InitialVelocityMax = 800,
            Gravity = new Vector2(0, 500), ScaleAmountMin = 0.3f, ScaleAmountMax = 0.7f, Material = add, Emitting = false,
            ColorRamp = SparkRamp(col),
        };
        sparks.SetParticleFlag(CpuParticles2D.ParticleFlags.AlignYToVelocity, true);
        stage.AddChild(sparks);
        var tsp = sparks.CreateTween();
        tsp.TweenInterval(0.24);
        tsp.TweenCallback(Callable.From(() => sparks.Emitting = true));

        // 5) the sword's name
        if (!string.IsNullOrEmpty(name))
        {
            var label = new Label
            {
                Text = name, MouseFilter = Control.MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Right,
                Size = new Vector2(900, 120), Position = new Vector2(size.X * 0.5f - 900 - size.X * 0.06f + 40, 80),
                Modulate = new Color(1, 1, 1, 0),
            };
            label.AddThemeFontSizeOverride("font_size", 84);
            label.AddThemeColorOverride("font_color", col.Lerp(Colors.White, 0.8f));
            label.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.03f, 0.06f));
            label.AddThemeConstantOverride("outline_size", 14);
            label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
            label.AddThemeConstantOverride("shadow_offset_x", 4);
            label.AddThemeConstantOverride("shadow_offset_y", 5);
            if (font != null) label.AddThemeFontOverride("font", font);
            stage.AddChild(label);
            var tl = label.CreateTween();
            tl.TweenInterval(0.3);
            tl.TweenProperty(label, "modulate:a", 1f, 0.15);
            tl.Parallel().TweenProperty(label, "position:x", label.Position.X - 40, 0.6).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }

        // 6) fade everything but the dim (which fades on its own)
        var tf = stage.CreateTween();
        tf.TweenInterval(0.92);
        tf.TweenProperty(stage, "modulate:a", 0f, 0.26).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
    }

    /// <summary>The sword's localized name; the pair shows both ("간장 · 막야").</summary>
    private static string NameOf(SwordId? sword) => sword switch
    {
        null => "",
        SwordId.Ganjiang or SwordId.Moye =>
            $"{SwordLore.NameText(SwordId.Ganjiang)} · {SwordLore.NameText(SwordId.Moye)}",
        { } s => SwordLore.NameText(s),
    };

    private static Gradient SparkRamp(Color col)
    {
        var g = new Gradient();
        g.SetColor(0, new Color(1f, 0.97f, 0.9f));
        g.SetColor(1, new Color(col, 0f));
        g.AddPoint(0.4f, col.Lightened(0.3f));
        return g;
    }

    private static Texture2D? Load(string rel)
    {
        var path = $"{MainFile.ResPath}/{rel}";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    private static Font? GameFont()
    {
        try
        {
            var lang = LocManager.Instance?.Language ?? "eng";
            return FontManager.GetSubstituteFont(lang, FontType.Bold) ?? NGame.Instance?.GetThemeDefaultFont();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
