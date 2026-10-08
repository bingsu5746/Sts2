using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace MagicSwordsman.MagicSwordsmanCode.RestSite;

/// <summary>
/// The game's choose-a-card screen always says "카드를 선택하세요" with a "Skip" button. Our sword screens (forge,
/// release...) are not card rewards, so while <see cref="Use"/> is active the next screen that opens gets our banner,
/// an optional subtitle under it and our label on the skip button (user request 2026-10-08).
/// Presentation only: selection, sync and skip behaviour are the game's.
/// </summary>
public static class ChooseScreenText
{
    private sealed record Texts(string Header, string? Subtitle, string? Skip);

    private static Texts? _current;

    public static bool IsActive => _current != null;

    /// <summary>Applies to screens opened until the returned handle is disposed.</summary>
    public static IDisposable Use(string header, string? subtitle = null, string? skip = null)
    {
        _current = new Texts(header, subtitle, skip);
        return new Handle();
    }

    private sealed class Handle : IDisposable
    {
        public void Dispose() => _current = null;
    }

    [HarmonyPatch(typeof(NChooseACardSelectionScreen), nameof(NChooseACardSelectionScreen._Ready))]
    private static class ReadyPatch
    {
        [HarmonyPostfix]
        private static void Apply(NChooseACardSelectionScreen __instance)
        {
            var t = _current;
            if (t == null) return;
            try
            {
                if (__instance.GetNodeOrNull("Banner") is NCommonBanner banner) banner.label.SetTextAutoSize(t.Header);
                if (t.Skip != null && __instance.GetNodeOrNull("SkipButton")?.GetNodeOrNull<MegaLabel>("Label") is { } skip)
                    skip.SetTextAutoSize(t.Skip);
                if (!string.IsNullOrWhiteSpace(t.Subtitle))
                    AddSubtitle(__instance, t.Subtitle!, (__instance.GetNodeOrNull("Banner") as NCommonBanner)?.label);
            }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"[ChooseScreenText] {e.Message}");
            }
        }
    }

    private static void AddSubtitle(Control screen, string text, Label? fontFrom)
    {
        var label = new Label
        {
            Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore, Name = "MagicSwordsmanSubtitle",
        };
        // the banner's font has the Korean glyphs; Godot's default theme font does not
        if (fontFrom?.GetThemeFont("font") is { } font) label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", 24);
        label.AddThemeColorOverride("font_color", new Color(0.93f, 0.9f, 0.82f));
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 6);
        // under the banner, across the middle 60% of the screen
        label.AnchorLeft = 0.2f;
        label.AnchorRight = 0.8f;
        label.AnchorTop = 0.2f;
        label.AnchorBottom = 0.2f;
        label.OffsetBottom = 120;
        screen.AddChild(label);
    }
}
