using MagicSwordsman.MagicSwordsmanCode.Extensions;
using BaseLib.Abstracts;
using MagicSwordsman.MagicSwordsmanCode.Cards.Tokens;
using MagicSwordsman.MagicSwordsmanCode.Events;
using MagicSwordsman.MagicSwordsmanCode.Events.Forge;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.RestSite;

/// <summary>
/// Rest-site option "마검 강화" added by Mangeomchong (pattern: Girya -> LiftRestSiteOption).
/// Flow (event-style pages, EventChoiceScreen; "돌아가기" = one step back):
///   1. pick an owned sword (a pair shows its leader, e.g. 간장·막야 -> Ganjiang)
///   2. that sword's story + 안전 강화 (+1) / 도박 강화 +2 / 도박 강화 +3 with the real odds
///      (gambles that would exceed level 5 are not offered)
///   3. roll with the player's reward Rng, apply, show the sword's result page.
/// Returning false from OnSelect (cancel) leaves the rest site open, like the game's Smith option.
/// Localization: rest_site_ui.json  OPTION_MAGICSWORDSMAN_FORGE.name / .description
/// </summary>
public sealed class SwordForgeRestSiteOption(Player owner) : CustomRestSiteOption(owner)
{
    public const string Id = "MAGICSWORDSMAN_FORGE";

    public override string OptionId => Id;

    // Own painted icon (a sword plunged into a glowing forge ring, AI Horde, see docs/card-art-credits.md).
    public override string? CustomIconPath => "ui/rest_option_forge.png".ImagePath();

    private Mangeomchong? Relic => Owner.GetRelic<Mangeomchong>();

    public override bool IsEnabled => Relic is { } r && SwordForge.UpgradeableSwords(r).Count > 0;

    public override async Task<bool> OnSelect()
    {
        var relic = Relic;
        if (relic == null) return false;
        var candidates = SwordForge.UpgradeableSwords(relic);
        if (candidates.Count == 0) return false;

        var choiceContext = new BlockingPlayerChoiceContext();

        // An event-style page per step (EventChoiceScreen, user request 2026-10-09): 1) which sword (the tomb),
        // 2) that sword's own story + how to forge it, 3) the result. "돌아가기" goes one step back; on the first page it
        // closes the option and the rest site stays open.
        while (true)
        {
            var swordIdx = await EventChoiceScreen.Choose(Owner, choiceContext, Ui("PICK_SWORD"), Ui("TOMB_TEXT"),
                $"{MainFile.ResPath}/images/relics/big/mangeomchong.png", new Godot.Color(0.62f, 0.45f, 0.95f),
                candidates.Select(c => new EventChoiceScreen.Option(
                    $"{SwordLore.NameText(c)} — {LevelText(relic.GetLevel(c))} {Text(SwordLore.StageName(c, relic.GetLevel(c)))}",
                    WithMastery(Text(SwordLore.Line(c, "EFFECT")), SwordMastery.ForgeLine(relic, c)))).ToList(),
                Ui("BACK"));
            if (swordIdx < 0 || swordIdx >= candidates.Count) return false;
            var sword = candidates[swordIdx];

            var lvl = relic.GetLevel(sword);
            var modes = new List<ForgeMode> { ForgeMode.Safe };
            if (SwordForge.IsAllowed(ForgeMode.GamblePlus2, lvl)) modes.Add(ForgeMode.GamblePlus2);
            if (SwordForge.IsAllowed(ForgeMode.GamblePlus3, lvl)) modes.Add(ForgeMode.GamblePlus3);
            var accent = Visuals.SwordVisuals.ColorOf(sword == SwordId.Ganjiang ? SwordId.Ganjiang : sword);
            var art = $"{MainFile.ResPath}/images/swords/{sword.ToString().ToLowerInvariant()}.png";
            var story = WithMastery(
                $"{Text(SwordLore.Line(sword, "FORGE_INTRO"))}\n\n[gold]{LevelText(lvl)}[/gold] — {Text(SwordLore.StageName(sword, lvl))}",
                SwordMastery.ForgeLine(relic, sword));
            var modeIdx = await EventChoiceScreen.Choose(Owner, choiceContext,
                Ui("PICK_METHOD").Replace("{Sword}", SwordLore.NameText(sword)), story, art, accent,
                modes.Select(m => ModeOption(sword, m)).ToList(), Ui("BACK"));
            if (modeIdx < 0 || modeIdx >= modes.Count) continue;

            var masteredBefore = SwordMastery.IsClaimed(relic, sword);
            // roll + apply (game RNG, never System.Random). Per-player stream: in multiplayer every client runs each
            // player's OnSelect, in different orders (RestSiteSynchronizer runs remote choices when their message
            // arrives), so a shared stream like RunState.Rng.Niche would give each client a different result.
            var result = await ForgeStory.Apply(Owner, sword, modes[modeIdx], Owner.PlayerRng.Rewards);
            relic.Flash();
            MainFile.Logger.Info($"[Forge] {sword} {modes[modeIdx]}: {result.Outcome}");

            var (title, text) = ResultText(result);
            if (!masteredBefore && SwordMastery.IsClaimed(relic, sword))
                text += MasteryResultText(sword); // reached level 5: the legend card (popup shown by SwordMastery)
            await EventChoiceScreen.Choose(Owner, choiceContext, title, text, art, accent,
                [new EventChoiceScreen.Option(Ui("CONTINUE"), "")], null);
            return true;
        }
    }

    private const string Cards = "cards";

    private EventChoiceScreen.Option ModeOption(SwordId sword, ForgeMode mode)
    {
        var key = mode switch
        {
            ForgeMode.Safe => "MAGICSWORDSMAN-FORGE_STORY_SAFE_TOKEN",
            ForgeMode.GamblePlus2 => "MAGICSWORDSMAN-FORGE_STORY_GAMBLE2_TOKEN",
            _ => "MAGICSWORDSMAN-FORGE_STORY_GAMBLE3_TOKEN",
        };
        var odds = SwordForge.OddsFor(Owner, sword, mode);
        var desc = new LocString(Cards, key + ".description");
        desc.Add("Story", mode switch
        {
            ForgeMode.Safe => SwordLore.Line(sword, "FORGE_SAFE"),
            ForgeMode.GamblePlus2 => SwordLore.Line(sword, "FORGE_GAMBLE"),
            _ => SwordLore.Generic("FORGE_GAMBLE3"),
        });
        desc.Add("Gain", SwordForge.GainFor(mode));
        desc.Add("Success", odds.Success);
        desc.Add("Failure", odds.Failure);
        desc.Add("Shatter", odds.Shatter);
        desc.Add("ShatterRule", SwordLore.Generic(
            SwordRegistry.GetDefinition(SwordRegistry.GroupLeader(sword)).CanBeLost ? "SHATTER_RULE" : "SHATTER_RULE_RESET"));
        return new EventChoiceScreen.Option(Text(new LocString(Cards, key + ".title")), Text(desc));
    }

    private static (string Title, string Text) ResultText(ForgeStoryResult r)
    {
        var o = r.Outcome;
        var kind = o.Kind.ToString().ToUpperInvariant();
        var key = "MAGICSWORDSMAN-FORGE_STORY_RESULT_TOKEN";
        var line = new LocString(Cards, $"{key}.{kind}.text");
        line.Add("SwordName", SwordLore.Name(o.Sword));
        line.Add("OldLevel", o.OldLevel);
        line.Add("NewLevel", o.NewLevel);
        line.Add("Stage", SwordLore.StageName(o.Sword, o.NewLevel));
        line.Add("CurseName", SwordRegistry.Get(o.Sword).FailureCurse?.Title ?? "");
        var storyPart = o.Kind switch
        {
            ForgeOutcomeKind.Success => "FORGE_SUCCESS",
            ForgeOutcomeKind.Shatter or ForgeOutcomeKind.ShatterReset => "FORGE_SHATTER",
            _ => "FORGE_FAILURE",
        };
        var text = Text(SwordLore.Line(o.Sword, storyPart));
        if (r.GotNibelung) text += Text(SwordLore.Line(SwordId.Gram, "NIBELUNG"));
        return (Text(new LocString(Cards, $"{key}.{kind}.title")), $"{text}\n\n{Text(line)}");
    }

    /// <summary>Appends the "5단계 완성 보상" line (Swords/SwordMastery.cs) to a forge text.</summary>
    private static string WithMastery(string text, string masteryLine) =>
        string.IsNullOrEmpty(masteryLine) ? text : $"{text}\n{masteryLine}";

    private static string MasteryResultText(SwordId sword)
    {
        if (SwordMastery.LegendCard(sword) is not { } legend) return "";
        var loc = SwordMastery.Text("FORGE_RESULT");
        loc.Add("Line", SwordLore.Line(sword, "MASTERY"));
        loc.Add("Card", legend.Title);
        return Text(loc);
    }

    /// <summary>"3단계" / "Level 3" (LEVEL_FORMAT, {Level} replaced here).</summary>
    private static string LevelText(int level) => Ui("LEVEL_FORMAT").Replace("{Level}", level.ToString());

    /// <summary>rest_site_ui.json MAGICSWORDSMAN_FORGE_UI.&lt;part&gt;</summary>
    private static string Ui(string part)
    {
        // raw text: PICK_METHOD's {Sword} is replaced by the caller, not by SmartFormat
        try { return new LocString("rest_site_ui", $"MAGICSWORDSMAN_FORGE_UI.{part}").GetRawText(); }
        catch (Exception) { return ""; }
    }

    private static string Text(LocString loc)
    {
        try { return loc.GetFormattedText(); }
        catch (Exception) { return ""; }
    }
}
