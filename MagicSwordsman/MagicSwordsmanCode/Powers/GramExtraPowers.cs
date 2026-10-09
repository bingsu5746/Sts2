using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 파프니르의 심장 (card GramFafnirsHeart): this combat, if Gram is the current sword when the owner's turn starts,
/// draw Amount more cards (ModifyHandDraw, same pattern as the game's MachineLearningPower).
/// </summary>
public sealed class GramFafnirsHeartPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner.Player || !SwordCombat.IsCurrent(player, SwordId.Gram)) return count;
        return count + Amount;
    }
}
