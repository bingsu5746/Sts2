using MagicSwordsman.MagicSwordsmanCode.Cards;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 스코프눙. Spec §7 [확정]: current effect = attacks apply "상처" (Wound): every turn the enemy takes damage equal
/// to its wounds (never decreases); at 12 wounds it bursts and resets to 0. Cards stack wounds.
/// Cost: cannot switch to it or play its cards on the first turn of combat (sunlight taboo, 햇빛 금기).
/// Kusanagi inherits "1 wound every 2 attacks".
/// FRAMEWORK PARTS: CanBecomeCurrent + CanPlayCard (turn-1 taboo).
/// </summary>
public sealed class SkofnungBehavior : SwordBehavior
{
    public override SwordId Id => SwordId.Skofnung;

    // TODO(content: Skofnung): starter cards (2), failure curse, a WoundPower (Powers/) applied in AfterDamageGiven
    //   when ctx.IsCurrent (or every 2nd attack when ctx.IsInherited, counter via ctx.AddCounter("attacks", 1)).
    public override IEnumerable<CardModel> StarterCards => [];
    public override CardModel? FailureCurse => null;

    public override bool CanBecomeCurrent(SwordContext ctx, SwitchReason reason) => ctx.TurnNumber != 1;

    public override bool CanPlayCard(SwordContext ctx, MagicSwordCard card) => ctx.TurnNumber != 1;
}
