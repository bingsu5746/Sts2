using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 발뭉·노퉁 (card GramBalmungNothung): this combat the owner's powered attacks deal +(Gram level + 1) per stack,
/// whatever the current sword is. GramBehavior skips its own +L while the owner has this power (no stacking).
/// Same damage filter as the game's StrengthPower (powered attacks of the owner).
/// </summary>
public sealed class BalmungNothungPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>Bonus per hit for one stack: Gram level (incl. combat bonus) + 1.</summary>
    public int BonusPerStack
    {
        get
        {
            var player = IsMutable ? Owner?.Player : null;
            return player == null ? 1 : SwordCombat.LevelOf(player, SwordId.Gram) + 1;
        }
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        if (dealer == null || dealer != Owner) return 0m;
        if (!props.IsPoweredAttack()) return 0m;
        return BonusPerStack * Amount;
    }
}
