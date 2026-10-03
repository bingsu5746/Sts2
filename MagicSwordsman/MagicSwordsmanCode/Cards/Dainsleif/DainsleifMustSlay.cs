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

/// <summary>반드시 죽여야 한다 (Uncommon): 12 damage (+1/L). If this does not kill the enemy, lose 2 HP.</summary>
public sealed class DainsleifMustSlay : DainsleifCard
{
    public const int HpLoss = 2;

    public DainsleifMustSlay() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(12);
        WithDamagePerLevel(1);
        WithVar("HpLoss", HpLoss);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var attack = await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        if (!attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
            await CreatureCmd.Damage(choiceContext, Owner.Creature, HpLoss, DamageProps.cardHpLoss, this);
    }
}
