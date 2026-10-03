using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 다인슬레이프 — STUB (content agent replaces this file). Spec §7:
/// Current effect: played cards go to the draw pile instead of the discard pile (ModifyCardPlayResultPileTypeAndPosition). Cards = repeat-style. Cost: cannot gain block while Dainsleif is current (ModifyBlockMultiplicative -> 0 for ctx.Creature; not inherited). Kusanagi inherits: only the first card each turn goes to the draw pile.
/// </summary>
public sealed class DainsleifBehavior : SwordBehavior
{
    public override SwordId Id => SwordId.Dainsleif;

    // TODO(content: Dainsleif): 2 starter cards (ModelDb.Card<...>()) granted on first acquisition.
    public override IEnumerable<CardModel> StarterCards => [];

    // TODO(content: Dainsleif): Dainsleif-specific failure curse (Curses/), e.g. ModelDb.Card<DainsleifCurse>().
    public override CardModel? FailureCurse => null;

    // TODO(content: Dainsleif): current-sword effect — override the Modify*/After* hooks (see GramBehavior),
    // use ctx.Scale(...) for numbers and skip penalties when ctx.IsInherited.
}
