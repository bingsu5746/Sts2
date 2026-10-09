using MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 막야 (female blade of the pair). Spec §7 + content doc §1.1 / §2.2:
///  - Current effect: Block the owner gains from cards +2/+2/+3/+3/+4/+5 (level 0..5; Moye uses Ganjiang's level).
///    Same filter as the game's DexterityPower (card or move block, powered) but only when a card is the source.
///    Kusanagi inherits half (ctx.Scale).
///  - Acquired/lost with Ganjiang; the Max HP cost is paid once, by GanjiangBehavior.
///  - Starter card: 막야 막기. Forge-failure curse: 용광로의 제물 (shared with Ganjiang).
/// </summary>
public sealed class MoyeBehavior : SwordBehavior
{
    private static readonly int[] BlockByLevel = [2, 2, 3, 3, 4, 5];

    public override SwordId Id => SwordId.Moye;

    public override IEnumerable<CardModel> StarterCards => [ModelDb.Card<MoyeGuard>()];

    public override CardModel? FailureCurse => ModelDb.Card<FurnaceOffering>();

    /// <summary>Card block bonus of the current-sword effect at a level (0..5).</summary>
    public static int BlockBonus(int level) => BlockByLevel[Math.Clamp(level, 0, BlockByLevel.Length - 1)];

    public override decimal ModifyBlockAdditive(SwordContext ctx, Creature target, decimal block, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (cardSource == null || cardSource.Owner?.Creature != ctx.Creature) return 0m;
        if (target != ctx.Creature) return 0m;
        if (!props.IsPoweredCardOrMonsterMoveBlock()) return 0m;
        return ctx.Scale(BlockBonus(ctx.LevelFor(cardSource)));
    }
}
