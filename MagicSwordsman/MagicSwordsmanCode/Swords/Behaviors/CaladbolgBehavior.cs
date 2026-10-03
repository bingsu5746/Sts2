using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 칼라드볼그 — STUB (content agent replaces this file). Spec §7:
/// Current effect: attacks hit up to 3 enemies. Cards = area attacks. Cost: reduced damage when there is only one enemy (ModifyDamageMultiplicative; not inherited). Kusanagi inherits: hits at most 2 enemies.
/// </summary>
public sealed class CaladbolgBehavior : SwordBehavior
{
    public override SwordId Id => SwordId.Caladbolg;

    // TODO(content: Caladbolg): 2 starter cards (ModelDb.Card<...>()) granted on first acquisition.
    public override IEnumerable<CardModel> StarterCards => [];

    // TODO(content: Caladbolg): Caladbolg-specific failure curse (Curses/), e.g. ModelDb.Card<CaladbolgCurse>().
    public override CardModel? FailureCurse => null;

    // TODO(content: Caladbolg): current-sword effect — override the Modify*/After* hooks (see GramBehavior),
    // use ctx.Scale(...) for numbers and skip penalties when ctx.IsInherited.
}
