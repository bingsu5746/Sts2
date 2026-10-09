using Godot;
using MagicSwordsman.MagicSwordsmanCode.Cards.Union;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// Presentation only: "새 조합이 열렸다" — shown when an acquired sword completes one or more 【조합】 cards with a sword
/// already in Mangeomchong (discovery, user request 2026-10-09). Shows the unlocked cards themselves (game card
/// nodes) with "A + B" under each, and one line on how union cards work. Waits until the sword-acquired popup
/// (<see cref="SwordAcquiredPopup"/>) is closed, then appears the same way (dim + framed panel, click / key closes).
/// Texts: card_keywords MAGICSWORDSMAN-UNION.popup*. Every failure is swallowed.
/// UNVERIFIED in game: card node sizing inside the panel, layout at non-1080p resolutions.
/// </summary>
public static class UnionUnlockedPopup
{
    private static readonly Color Accent = new(0.72f, 0.5f, 1f);
    private static readonly Color Panel = new(0.07f, 0.075f, 0.095f, 0.97f);
    private static readonly Color Muted = new(0.72f, 0.72f, 0.76f);
    private const float CardScale = 0.72f;
    private const double MaxWaitSeconds = 90;

    /// <summary>Shows the union cards that <paramref name="acquired"/> completes (none -> nothing happens).</summary>
    public static void Show(Mangeomchong tomb, IEnumerable<SwordId> acquired)
    {
        try
        {
            var cards = UnionCatalog.UnlockedBy(tomb, acquired);
            if (cards.Count == 0) return;
            if (Engine.GetMainLoop() is not SceneTree tree) return;

            // wait for the sword popup (added in the same frame or soon after) to be closed first
            var waiter = new Node { Name = "MagicSwordUnionPopupWaiter" };
            var timer = new Godot.Timer { WaitTime = 0.25, Autostart = true };
            var waited = 0.0;
            timer.Timeout += () =>
            {
                try
                {
                    waited += timer.WaitTime;
                    var swordPopupOpen = tree.Root.GetChildren()
                        .Any(n => n.Name.ToString().StartsWith("MagicSwordAcquiredPopup"));
                    if (swordPopupOpen && waited < MaxWaitSeconds) return;
                    waiter.QueueFree();
                    ShowInner(tree, cards);
                }
                catch (Exception e)
                {
                    waiter.QueueFree();
                    MainFile.Logger.Warn($"[UnionUnlockedPopup] failed: {e.Message}");
                }
            };
            waiter.AddChild(timer);
            tree.Root.AddChild(waiter);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[UnionUnlockedPopup] failed: {e.Message}");
        }
    }

    private static void ShowInner(SceneTree tree, List<UnionCard> cards)
    {
        var layer = new CanvasLayer { Layer = 120, Name = "MagicSwordUnionUnlockedPopup" };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.72f), MouseFilter = Control.MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(dim);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Panel, BorderColor = Accent, BorderWidthLeft = 3, BorderWidthRight = 3, BorderWidthTop = 3,
            BorderWidthBottom = 3, CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14, ShadowColor = new Color(Accent, 0.35f), ShadowSize = 24,
            ContentMarginLeft = 32, ContentMarginRight = 32, ContentMarginTop = 24, ContentMarginBottom = 24,
        });
        panel.CustomMinimumSize = new Vector2(760, 0);
        center.AddChild(panel);

        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 10);
        panel.AddChild(col);

        col.AddChild(MakeLabel(Text("popupHeader"), 18, Accent, center: true));
        var title = new LocString("card_keywords", "MAGICSWORDSMAN-UNION.popupTitle");
        title.Add("Count", cards.Count);
        col.AddChild(MakeLabel(Format(title), 40, Colors.White, bold: true, center: true));

        // ---- the cards themselves, "A + B" under each
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 24);
        col.AddChild(row);
        foreach (var card in cards)
        {
            var cell = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            cell.AddThemeConstantOverride("separation", 6);
            var slot = new Control
            {
                CustomMinimumSize = NCard.defaultSize * CardScale, MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            cell.AddChild(slot);
            try
            {
                var node = NCard.Create(card);
                if (node != null)
                {
                    slot.AddChild(node);
                    node.MouseFilter = Control.MouseFilterEnum.Ignore;
                    node.Position = NCard.defaultSize * CardScale / 2; // a card node is drawn centred on its position
                    node.Scale = Vector2.One * CardScale;
                    node.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
                }
            }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"[UnionUnlockedPopup] card {card.Id.Entry}: {e.Message}");
                slot.AddChild(MakeLabel(card.Title, 24, Colors.White, bold: true, center: true));
            }

            var pair = new LocString("card_keywords", "MAGICSWORDSMAN-UNION.popupPair");
            pair.Add("SwordA", UnionCatalog.NameOf(card.Primary));
            pair.Add("SwordB", UnionCatalog.NameOf(card.Partner));
            cell.AddChild(MakeLabel(Format(pair), 18, Accent, center: true));
            row.AddChild(cell);
        }

        col.AddChild(new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore });
        col.AddChild(MakeLabel(Text("popupBody"), 20, Colors.White, center: true));
        var hint = MakeLabel(Text("popupContinue"), 16, new Color(Muted, 0.7f));
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        col.AddChild(hint);

        tree.Root.AddChild(layer);

        root.Modulate = new Color(1, 1, 1, 0);
        root.CreateTween().TweenProperty(root, "modulate:a", 1f, 0.25);

        var closing = false;
        void Close()
        {
            if (closing || !GodotObject.IsInstanceValid(layer)) return;
            closing = true;
            var o = root.CreateTween();
            o.TweenProperty(root, "modulate:a", 0f, 0.18);
            o.TweenCallback(Callable.From(layer.QueueFree));
        }

        var openedAt = Time.GetTicksMsec();
        root.GuiInput += ev =>
        {
            if (Time.GetTicksMsec() - openedAt < 350) return; // ignore the click that closed the sword popup
            if (ev is InputEventMouseButton { Pressed: true } or InputEventKey { Pressed: true }) Close();
        };
    }

    private static string Text(string key) => Format(new LocString("card_keywords", $"MAGICSWORDSMAN-UNION.{key}"));

    private static string Format(LocString loc)
    {
        try { return loc.GetFormattedText(); }
        catch (Exception) { return ""; }
    }

    private static Label MakeLabel(string text, int size, Color color, bool bold = false, bool center = false)
    {
        var label = new Label
        {
            Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left,
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
}
