using MagicSwordsman.MagicSwordsmanCode.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 쿠사나기. Spec §7 [확정]:
///  - Current effect "계승": the effect of the sword that was current right before, at 50% (rounded down, min 1);
///    its costs are not inherited. Implemented here via <see cref="GetInheritedSword"/> — CurrentSwordPower then
///    runs that sword's hooks with ctx.IsInherited = true (each behavior uses ctx.Scale / skips penalties).
///  - Cards: "정화" (exhaust Status/Curse cards, reduce own debuffs).  TODO(content: Kusanagi)
///  - Cost: lose a little HP at the end of combat + other sword -> Kusanagi at most 2 times per combat. After the
///    2nd switch-in is used up it goes back into Mangeomchong when it leaves: its cards get Exhaust + Ethereal(휘발성)
///    and playing them no longer switches (SwordCombat.CanSwitchTo refuses swords in ReturnedToVault).
/// FRAMEWORK PARTS (keep when adding content): GetInheritedSword, CanBecomeCurrent, OnLeaveCurrent, ModifyCardKeywords.
/// </summary>
public sealed class KusanagiBehavior : SwordBehavior
{
    public const int MaxSwitchInsPerCombat = 2;

    /// <summary>[임시] HP lost at the end of a combat in which Kusanagi came out.</summary>
    public const int CombatEndHpLoss = 2;

    public override SwordId Id => SwordId.Kusanagi;

    // TODO(content: Kusanagi): starter cards (2) and a Kusanagi-specific failure curse.
    public override IEnumerable<CardModel> StarterCards => [];
    public override CardModel? FailureCurse => null;

    /// <summary>Kusanagi's effect is to inherit, so inheriting Kusanagi itself makes no sense.</summary>
    public override bool CanBeInherited => false;

    public override SwordId? GetInheritedSword(SwordContext ctx)
    {
        var previous = ctx.Combat?.Previous;
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

    public override Task OnCombatEnd(SwordContext ctx, CombatRoom room)
    {
        // TODO(content: Kusanagi): lose CombatEndHpLoss HP if Kusanagi was summoned this combat
        //   (ctx.Combat?.Summoned.Contains(SwordId.Kusanagi)). Find the right HP-loss command in
        //   MegaCrit.Sts2.Core.Commands.CreatureCmd (Damage with ValueProp.Unblockable|Unpowered, or SetCurrentHp)
        //   and make sure it is safe to call after combat has ended.
        return Task.CompletedTask;
    }
}
