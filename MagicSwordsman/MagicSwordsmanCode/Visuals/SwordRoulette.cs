using System.Text.RegularExpressions;
using Godot;
using MagicSwordsman.MagicSwordsmanCode.Events;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// Presentation only (user report #4): the run-start "첫 검을 깨운다" roulette. A framed slot cycles through every
/// sword's art with a tick sound, slowing down over ~2.6 s, and stops on <paramref name="target"/> — the first sword
/// that Mangeomchong already rolled (seeded, Mangeomchong.GrantStartingSword), so the animation only REVEALS it and can
/// never change the result. Local player only; failures complete the task immediately; a click skips to the end.
/// UNVERIFIED in game: look at non-1080p resolutions.
/// </summary>
public static class SwordRoulette
{
    private static readonly Color Panel = new(0.07f, 0.075f, 0.095f, 0.97f);

    /// <summary>Plays the roulette; the task completes once it has stopped and faded (or on any failure, max 8 s).</summary>
    public static async Task Play(SwordId target)
    {
        var done = new TaskCompletionSource();
        try { Start(SwordRegistry.GroupLeader(target), done); }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordRoulette] failed: {e.Message}");
            done.TrySetResult();
        }
        await Task.WhenAny(done.Task, Task.Delay(8000));
    }

    private static void Start(SwordId target, TaskCompletionSource done)
    {
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
        {
            done.TrySetResult();
            return;
        }

        var swords = SwordRegistry.StartingSwordCandidates();
        if (swords.Count == 0 || !swords.Contains(target)) swords = [target];

        // Tick schedule: intervals grow geometrically (0.045 s -> ~0.42 s), about 2.6 s in total. The strip is laid out
        // backwards from the target so the LAST frame is always the pre-rolled sword.
        var intervals = new List<double>();
        double interval = 0.045, total = 0;
        while (total < 2.6)
        {
            intervals.Add(interval);
            total += interval;
            interval *= 1.085;
        }
        var startIndex = ((swords.IndexOf(target) - intervals.Count) % swords.Count + swords.Count) % swords.Count;

        var layer = new CanvasLayer { Layer = 119, Name = "MagicSwordRoulette" };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f), MouseFilter = Control.MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(dim);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var border = new StyleBoxFlat
        {
            BgColor = Panel, BorderColor = new Color(0.62f, 0.45f, 0.95f), BorderWidthLeft = 3, BorderWidthRight = 3,
            BorderWidthTop = 3, BorderWidthBottom = 3, CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14, ContentMarginLeft = 26, ContentMarginRight = 26,
            ContentMarginTop = 20, ContentMarginBottom = 20, ShadowSize = 24,
            ShadowColor = new Color(0.62f, 0.45f, 0.95f, 0.35f),
        };
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", border);
        center.AddChild(panel);

        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 12);
        panel.AddChild(col);

        var header = MakeLabel(Text(SwordLore.Generic("ROULETTE_HEADER")), 22, new Color(0.85f, 0.8f, 1f));
        col.AddChild(header);

        var art = new TextureRect
        {
            CustomMinimumSize = new Vector2(300, 420), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        col.AddChild(art);

        var name = MakeLabel("", 30, Colors.White, bold: true);
        col.AddChild(name);

        tree.Root.AddChild(layer);

        var textures = new Dictionary<SwordId, Texture2D?>();
        void Show(SwordId sword)
        {
            if (!GodotObject.IsInstanceValid(art)) return;
            if (!textures.TryGetValue(sword, out var tex))
            {
                var path = ArtPath(sword);
                tex = path != null ? ResourceLoader.Load<Texture2D>(path) : null;
                textures[sword] = tex;
            }
            art.Texture = tex;
            name.Text = Text(SwordLore.Name(sword));
            var c = SwordVisuals.ColorOf(sword == SwordId.Ganjiang ? SwordId.Moye : sword);
            border.BorderColor = c;
            border.ShadowColor = new Color(c, 0.35f);
        }

        var finished = false;
        void Finish()
        {
            if (finished) return;
            finished = true;
            try
            {
                Show(target);
                Sfx.Summon(target);
                if (GodotObject.IsInstanceValid(art))
                {
                    art.PivotOffset = art.Size / 2;
                    var pop = art.CreateTween();
                    pop.TweenProperty(art, "scale", new Vector2(1.08f, 1.08f), 0.12);
                    pop.TweenProperty(art, "scale", Vector2.One, 0.18);
                }
                tree.CreateTimer(0.9).Timeout += () =>
                {
                    if (!GodotObject.IsInstanceValid(root))
                    {
                        done.TrySetResult();
                        return;
                    }
                    var o = root.CreateTween();
                    o.TweenProperty(root, "modulate:a", 0f, 0.25);
                    o.TweenCallback(Callable.From(() =>
                    {
                        layer.QueueFree();
                        done.TrySetResult();
                    }));
                };
            }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"[SwordRoulette] finish failed: {e.Message}");
                if (GodotObject.IsInstanceValid(layer)) layer.QueueFree();
                done.TrySetResult();
            }
        }

        // click / key: skip straight to the result
        var openedAt = Time.GetTicksMsec();
        root.GuiInput += ev =>
        {
            if (Time.GetTicksMsec() - openedAt < 350) return;
            if (ev is InputEventMouseButton { Pressed: true } or InputEventKey { Pressed: true }) Finish();
        };

        var step = 0;
        void Tick()
        {
            if (finished) return;
            if (step >= intervals.Count)
            {
                Finish();
                return;
            }
            var sword = swords[(startIndex + step + 1) % swords.Count];
            Show(sword);
            Sfx.Play("switch", -15f, 1.25f - 0.35f * step / intervals.Count);
            var wait = intervals[step];
            step++;
            tree.CreateTimer(wait).Timeout += Tick;
        }

        Show(swords[startIndex]);
        Tick();
    }

    private static string? ArtPath(SwordId sword)
    {
        // Same choice as SwordAcquiredPopup: the floating-sword sprite, the pair's crossed-swords art for 간장·막야.
        var spritePath = $"{MainFile.ResPath}/images/swords/{sword.ToString().ToLowerInvariant()}.png";
        if (sword != SwordId.Ganjiang && ResourceLoader.Exists(spritePath)) return spritePath;
        var id = sword == SwordId.Ganjiang
            ? "ganjiang_moye_pair"
            : Regex.Replace(sword.ToString(), "(?<!^)([A-Z])", "_$1").ToLowerInvariant() + "_token";
        foreach (var ext in new[] { ".png", ".jpg" })
        {
            var path = $"{MainFile.ResPath}/images/card_portraits/big/{id}{ext}";
            if (ResourceLoader.Exists(path)) return path;
        }
        return ResourceLoader.Exists(spritePath) ? spritePath : null;
    }

    private static Label MakeLabel(string text, int size, Color color, bool bold = false)
    {
        var label = new Label
        {
            Text = text, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", bold ? 6 : 3);
        try
        {
            var lang = LocManager.Instance?.Language ?? "eng";
            var font = FontManager.GetSubstituteFont(lang, bold ? FontType.Bold : FontType.Regular)
                       ?? NGame.Instance?.GetThemeDefaultFont();
            if (font != null) label.AddThemeFontOverride("font", font);
        }
        catch (Exception)
        {
            // default font
        }
        return label;
    }

    private static string Text(LocString loc)
    {
        try { return loc.GetFormattedText(); }
        catch (Exception) { return ""; }
    }
}
