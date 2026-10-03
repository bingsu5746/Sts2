using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Tyrfing;

/// <summary>
/// 뽑으면 한 사람 (Uncommon): cost 2. 13 damage (+2/L). If this kills the enemy: gain 2 energy and draw 2 cards.
/// Otherwise lose 3 HP.
/// </summary>
public sealed class TyrfingOneLifePerDraw : TyrfingCard
{
    public const int HpLoss = 3;

    public TyrfingOneLifePerDraw() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(13);
        WithDamagePerLevel(2);
        WithEnergy(2);
        WithCards(2);
        WithVar("HpLoss", HpLoss);
    }

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var attack = await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        if (attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
        {
            await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        }
        else
        {
            await CreatureCmd.Damage(choiceContext, Owner.Creature, HpLoss, DamageProps.cardHpLoss, this);
        }
    }
}
