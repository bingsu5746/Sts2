using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

/// <summary>
/// 머리카락과 손톱 — Moye, Skill, Common, cost 1, self. Lose 2 HP and gain 10 (+1/L) Block. 【짝】 no HP loss.
/// HP loss as the game's BloodWall (Unblockable | Unpowered | Move).
/// Lore: the smith couple put their hair and nails into the furnace.
/// </summary>
public sealed class MoyeHairAndNails : GanjiangMoyeCard
{
    public MoyeHairAndNails() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithVar("HpLoss", 2);
        WithBlock(10);
        WithBlockPerLevel(1);
    }

    protected override SwordId Side => SwordId.Moye;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        if (!pair)
            await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars["HpLoss"].IntValue,
                ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, this);
        await CommonActions.CardBlock(this, cardPlay);
    }
}
