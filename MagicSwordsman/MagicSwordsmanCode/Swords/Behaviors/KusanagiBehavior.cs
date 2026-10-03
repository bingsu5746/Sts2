using MagicSwordsman.MagicSwordsmanCode.Cards;
using MagicSwordsman.MagicSwordsmanCode.Cards.Kusanagi;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 쿠사나기. Spec §7 [확정]:
///  - Current effect "계승": the effect of the sword that was current right before, at 50% (rounded down, min 1);
///    its costs are not inherited. Implemented here via <see cref="GetInheritedSword"/> — CurrentSwordPower then
///    runs that sword's hooks with ctx.IsInherited = true (each behavior uses ctx.Scale / skips penalties).
///  - Cards: "정화" (exhaust Status/Curse cards, reduce own debuffs) — Cards/Kusanagi/ (KusanagiPurify helpers).
///  - Cost: lose a little HP at the end of every combat while Kusanagi is OWNED (content doc 0.4 #7: 2 at levels
///    0-2, 1 at levels 3-5) + other sword -> Kusanagi at most 2 times per combat. After the
///    2nd switch-in is used up it goes back into Mangeomchong when it leaves: its cards get Exhaust + Ethereal(휘발성)
///    and playing them no longer switches (SwordCombat.CanSwitchTo refuses swords in ReturnedToVault).
/// FRAMEWORK PARTS (keep when adding content): GetInheritedSword, CanBecomeCurrent, OnLeaveCurrent, ModifyCardKeywords.
/// </summary>
public sealed class KusanagiBehavior : SwordBehavior
{
    public const int MaxSwitchInsPerCombat = 2;

    /// <summary>HP lost at the end of a combat in which Kusanagi was summoned (spec §9 [확정]; levels 0-2).</summary>
    public const int CombatEndHpLoss = 2;

    /// <summary>[Claude] HP lost at the end of combat from level 3 on (content doc §1.1).</summary>
    public const int CombatEndHpLossHighLevel = 1;

    public static int CombatEndHpLossFor(int level) => level >= 3 ? CombatEndHpLossHighLevel : CombatEndHpLoss;

    public override SwordId Id => SwordId.Kusanagi;

    public override IEnumerable<CardModel> StarterCards =>
        [ModelDb.Card<KusanagiGrassCutter>(), ModelDb.Card<KusanagiGatheringClouds>()];

    /// <summary>덴무의 병 (content doc §3.1).</summary>
    public override CardModel? FailureCurse => ModelDb.Card<TenmusIllness>();

    /// <summary>Kusanagi's effect is to inherit, so inheriting Kusanagi itself makes no sense.</summary>
    public override bool CanBeInherited => false;

    public override SwordId? GetInheritedSword(SwordContext ctx)
    {
        // ctx.PreviousSword (not Combat.Previous) so the hand preview of a Kusanagi card is correct too.
        var previous = ctx.PreviousSword;
        return previous is { } p && p != SwordId.Kusanagi ? p : null;
    }

    public override bool CanBecomeCurrent(SwordContext ctx, SwitchReason reason)
    {
        var state = ctx.Combat;
        if (state == null) return true;
        if (state.Current == null) return true; // first sword of the combat: not a "switch-in" from another sword
        return state.KusanagiSwitchIns < MaxSwitchInsPerCombat;
    }

    public override Task OnLeaveCurrent(SwordContext ctx, SwordId next, PlayerChoiceContext choiceContext)
    {
        var state = ctx.Combat;
        if (state != null && state.KusanagiSwitchIns >= MaxSwitchInsPerCombat)
        {
            // back into Mangeomchong for the rest of the combat (TODO(art): "만검총 문이 들썩이는 연출")
            state.Present.Remove(SwordId.Kusanagi);
            state.ReturnedToVault.Add(SwordId.Kusanagi);
        }

        return Task.CompletedTask;
    }

    public override bool ModifyCardKeywords(SwordContext ctx, MagicSwordCard card, ISet<CardKeyword> keywords)
    {
        if (ctx.Combat?.ReturnedToVault.Contains(SwordId.Kusanagi) != true) return false;
        var changed = keywords.Add(CardKeyword.Exhaust);
        changed |= keywords.Add(CardKeyword.Ethereal);
        return changed;
    }

    public override async Task OnCombatEnd(SwordContext ctx, CombatRoom room)
    {
        // Spec §9 [확정] (2026-10-03, overrides content doc 0.4 #7): only applies when Kusanagi was summoned in this
        // combat. The combat state is still alive here (Mangeomchong clears it after OnCombatEnd). Ignores block (HP loss). Same command the game's events use for
        // out-of-combat HP loss (e.g. SunkenStatue: CreatureCmd.Damage(..., Unblockable | Unpowered, null, null)).
        // Safety deviation: never lethal (leaves at least 1 HP) because a death after the victory screen started
        // is untested. TODO(test): verify in game that damage right after AfterCombatEnd shows/applies correctly.
        if (ctx.Combat is not { } state || !state.Summoned.Contains(SwordId.Kusanagi)) return;
        var creature = ctx.Creature;
        if (creature.IsDead) return;
        var loss = Math.Min(CombatEndHpLossFor(ctx.Level), creature.CurrentHp - 1);
        if (loss <= 0) return;
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), creature, loss, DamageProps.nonCardHpLoss, null, null);
    }
}
