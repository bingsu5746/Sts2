using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 막야 — STUB (content agent replaces this file). Spec §7:
/// Current effect: block up (female blade). Cards = defense / support. Pair rules: see GanjiangBehavior (Moye shares Ganjiang's level; acquired/lost together). Kusanagi inherits half of the effect.
/// </summary>
public sealed class MoyeBehavior : SwordBehavior
{
    public override SwordId Id => SwordId.Moye;

    // TODO(content: Moye): 2 starter cards (ModelDb.Card<...>()) granted on first acquisition.
    public override IEnumerable<CardModel> StarterCards => [];

    // TODO(content: Moye): Moye-specific failure curse (Curses/), e.g. ModelDb.Card<MoyeCurse>().
    public override CardModel? FailureCurse => null;

    // TODO(content: Moye): current-sword effect — override the Modify*/After* hooks (see GramBehavior),
    // use ctx.Scale(...) for numbers and skip penalties when ctx.IsInherited.
}
