using Godot;
using MagicSwordsman.MagicSwordsmanCode.Cards;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Extensions;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Swords;

/// <summary>
/// ONE hover tip on every sword card (user feedback 2026-10-17: first-time players could not tell what a sword card
/// does besides its own text). It says which sword the card belongs to, its role, that playing the card makes that sword
/// the current sword (summoning it the first time in a combat), and what the sword then does — effect and cost with live
/// numbers from <see cref="SwordEffectText"/>, so it never disagrees with the rules.
///
/// In combat the switch line follows the real state (<see cref="SwordCombat.CanSwitchTo"/>): already current /
/// summon + switch / switch / cannot switch right now (e.g. Durandal at 30% HP or less: the card still resolves, the
/// current sword stays — SwordCombat.SwitchTo). Outside combat (deck, rewards, shop, library) the generic line is shown.
/// Attached once in <see cref="MagicSwordCard"/>'s constructor; common cards (no sword) get nothing. Presentation only.
/// Texts: events table, MAGICSWORDSMAN-SWORD_CARD_TIP.* (fragment loc_fragments/clarity.&lt;lang&gt;.events.json).
/// </summary>
public static class SwordCardTip
{
    private const string Table = "events";
    private const string Prefix = "MAGICSWORDSMAN-SWORD_CARD_TIP";

    public static IEnumerable<IHoverTip> TipsFor(CardModel card)
    {
        if (card is not MagicSwordCard { Sword: { } sword }) return [];
        try
        {
            return [Build(card, sword)];
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordCardTip] {card.Id}: {e.Message}");
            return [];
        }
    }

    private static IHoverTip Build(CardModel card, SwordId sword)
    {
        var player = card.IsMutable ? card.Owner : null;
        var level = SwordEffectText.LevelOf(player, sword);
        var name = SwordRegistry.DisplayName(sword);

        var title = new LocString(Table, $"{Prefix}.title");
        title.Add("Sword", name);

        var lines = new List<string>();
        var role = SwordRole.Rich(sword);
        if (role.Length > 0) lines.Add(role);
        lines.Add(Line(SwitchPart(player, sword), name));
        var effect = new LocString(Table, $"{Prefix}.effect");
        effect.Add("Effect", SwordEffectText.EffectIfSwitched(sword, player, level));
        lines.Add(effect.GetFormattedText());
        var cost = SwordEffectText.Cost(sword, player, level);
        if (cost.Length > 0)
        {
            var costLine = new LocString(SwordEffectText.Table, "MAGICSWORDSMAN-SWORD_FX.COST_LINE");
            costLine.Add("Cost", cost);
            lines.Add(costLine.GetFormattedText());
        }

        return new HoverTip(title, string.Join("\n", lines), Icon(sword));
    }

    /// <summary>Which switch line applies: SWITCH (outside combat / generic), SUMMON, ALREADY or BLOCKED.</summary>
    private static string SwitchPart(Player? player, SwordId sword)
    {
        if (player?.PlayerCombatState == null || SwordCombat.Get(player) is not { } state) return "SWITCH";
        if (state.Current == sword) return "ALREADY";
        if (!SwordCombat.CanSwitchTo(player, sword, SwitchReason.CardPlayed)) return "BLOCKED";
        return state.Present.Contains(sword) ? "SWITCH_NOW" : "SUMMON";
    }

    private static string Line(string part, string swordName)
    {
        var loc = new LocString(Table, $"{Prefix}.{part}");
        loc.Add("Sword", swordName);
        return loc.GetFormattedText();
    }

    /// <summary>The sword's current-sword power icon (images/powers/current_sword_&lt;sword&gt;.png), null when missing.</summary>
    private static Texture2D? Icon(SwordId sword)
    {
        try
        {
            var path = Path.Join(MainFile.ResPath, "images", "powers", $"current_sword_{sword.ToString().ToLowerInvariant()}.png");
            return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
