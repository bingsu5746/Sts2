using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 클라이브 솔라시 — STUB (content agent replaces this file). Spec §7:
/// At the END of the turn, if Claiomh Solais is current, charge 1 Light (OnPlayerTurnEnd with ctx.IsCurrent; counter ctx.AddCounter("light", 1)). Using other swords during the turn is free. Cards = draw-cut (발도): release Light for block-ignoring strikes. Cost: at the end of the turn, if it is NOT current, all Light is lost. Kusanagi inherits half the Light charge.
/// </summary>
public sealed class ClaiomhSolaisBehavior : SwordBehavior
{
    public override SwordId Id => SwordId.ClaiomhSolais;

    // TODO(content: ClaiomhSolais): 2 starter cards (ModelDb.Card<...>()) granted on first acquisition.
    public override IEnumerable<CardModel> StarterCards => [];

    // TODO(content: ClaiomhSolais): ClaiomhSolais-specific failure curse (Curses/), e.g. ModelDb.Card<ClaiomhSolaisCurse>().
    public override CardModel? FailureCurse => null;

    // TODO(content: ClaiomhSolais): current-sword effect — override the Modify*/After* hooks (see GramBehavior),
    // use ctx.Scale(...) for numbers and skip penalties when ctx.IsInherited.
}
