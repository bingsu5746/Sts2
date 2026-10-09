using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 흐롤프의 용사들 (SkofnungHrolfsChampions): whenever the owner applies 망령 상흔 (a positive amount), it applies
/// Amount more (SneckoSkull pattern: ModifyPowerAmountGivenAdditive). Reductions (스코프눙 돌) are not affected.
/// </summary>
public sealed class SkofnungHrolfsChampionsPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<SkofnungWoundPower>()];

    public override decimal ModifyPowerAmountGivenAdditive(PowerModel power, Creature giver, decimal amount,
        Creature? target, CardModel? cardSource)
    {
        if (power is not SkofnungWoundPower || giver != Owner || amount <= 0m) return 0m;
        return Amount;
    }

    public override Task AfterModifyingPowerAmountGiven(PowerModel power)
    {
        Flash();
        return Task.CompletedTask;
    }
}
