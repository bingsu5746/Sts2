using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 티르빙 — STUB (content agent replaces this file). Spec §7:
/// Current effect: attacks ignore block. Cards = high-damage finishers. Cost: the first 3 times its cards are used in the whole RUN, each adds a permanent curse (count with Mangeomchong.AddRunCounter("tyrfing.curses", 1)); none after 3. Kusanagi inherits: only the first attack each turn ignores block.
/// </summary>
public sealed class TyrfingBehavior : SwordBehavior
{
    public override SwordId Id => SwordId.Tyrfing;

    // TODO(content: Tyrfing): 2 starter cards (ModelDb.Card<...>()) granted on first acquisition.
    public override IEnumerable<CardModel> StarterCards => [];

    // TODO(content: Tyrfing): Tyrfing-specific failure curse (Curses/), e.g. ModelDb.Card<TyrfingCurse>().
    public override CardModel? FailureCurse => null;

    // TODO(content: Tyrfing): current-sword effect — override the Modify*/After* hooks (see GramBehavior),
    // use ctx.Scale(...) for numbers and skip penalties when ctx.IsInherited.
}
