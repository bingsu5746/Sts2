using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 막야의 투신 (card MoyesSacrifice): this combat, whenever a 【짝】 (Ganjiang/Moye pair) effect triggers, gain
/// Amount Block. Triggered by Cards/GanjiangMoye/PairRules. Block is Unpowered (like the game's PlatingPower).
/// </summary>
public sealed class MoyesSacrificePower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task OnPairTriggered()
    {
        if (Amount <= 0 || Owner == null || Owner.IsDead) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }
}
