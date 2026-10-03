using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 간장 — STUB (content agent replaces this file). Spec §7:
/// Current effect: attack damage up (male blade). Cards = attack. Alternating Ganjiang/Moye cards gives a pair bonus; twin-sword cards count as both and always trigger the pair bonus. Cost: lose Max HP when acquired (OnAcquired, firstTime). Acquired together with Moye (2 slots), shares Moye's level (SwordDefinition.LevelOwner = Ganjiang). Kusanagi inherits half of the effect of whichever of the two was current right before.
/// </summary>
public sealed class GanjiangBehavior : SwordBehavior
{
    public override SwordId Id => SwordId.Ganjiang;

    // TODO(content: Ganjiang): 2 starter cards (ModelDb.Card<...>()) granted on first acquisition.
    public override IEnumerable<CardModel> StarterCards => [];

    // TODO(content: Ganjiang): Ganjiang-specific failure curse (Curses/), e.g. ModelDb.Card<GanjiangCurse>().
    public override CardModel? FailureCurse => null;

    // TODO(content: Ganjiang): current-sword effect — override the Modify*/After* hooks (see GramBehavior),
    // use ctx.Scale(...) for numbers and skip penalties when ctx.IsInherited.
}
