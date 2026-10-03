using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

/// <summary>
/// 막야의 투신 — Moye, Power, Uncommon, cost 1, self. Lose 3 HP. This combat, whenever 【짝】 triggers, gain
/// 3 (+⌊L/2⌋) Block (<see cref="MoyesSacrificePower"/>).
/// Lore: in another version Moye threw herself into the furnace to finish the sword.
/// </summary>
public sealed class MoyesSacrifice : GanjiangMoyeCard
{
    public MoyesSacrifice() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("HpLoss", 3);
        WithCalculatedVar("PairBlock", 3, 1, static (card, _) => card is MagicSwordCard m ? m.SwordLevel / 2 : 0);
        WithTip(typeof(MoyesSacrificePower));
    }

    protected override SwordId Side => SwordId.Moye;
    protected override bool HasPairEffect => false;

    private int PairBlock => DynamicVars["PairBlockBase"].IntValue + SwordLevel / 2;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars["HpLoss"].IntValue,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, this);
        await PowerCmd.Apply<MoyesSacrificePower>(choiceContext, Owner.Creature, PairBlock, Owner.Creature, this);
    }
}
