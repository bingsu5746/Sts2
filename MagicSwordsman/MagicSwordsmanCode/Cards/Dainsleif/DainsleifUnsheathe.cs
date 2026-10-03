using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Dainsleif;

/// <summary>칼집에서 뽑다 (Basic): cost 0. 3 damage (+⌊L/2⌋). Draw 1 card.</summary>
public sealed class DainsleifUnsheathe : DainsleifCard
{
    public DainsleifUnsheathe() : base(0, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithDamage(3);
        WithCards(1);
    }

    protected override decimal BonusDamage(Creature? target) => SwordLevel / 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}
