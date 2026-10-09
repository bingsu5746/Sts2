using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 로카마두르 (DurandalRocamadour): while Durandal's effect is active (current, or inherited by Kusanagi) when your
/// Block would be cleared at the start of your turn, it is kept. Game pattern: BlurPower / BarricadePower
/// (ShouldClearBlock + AfterPreventingBlockClear flash).
/// </summary>
public sealed class DurandalRocamadourPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public override bool ShouldClearBlock(Creature creature)
    {
        if (creature != Owner || Owner.Player is not { } player) return true;
        return !DurandalBehavior.IsSwordEffectActive(player, SwordId.Durandal, out _);
    }

    public override Task AfterPreventingBlockClear(AbstractModel preventer, Creature creature)
    {
        if (preventer == this) Flash();
        return Task.CompletedTask;
    }
}
