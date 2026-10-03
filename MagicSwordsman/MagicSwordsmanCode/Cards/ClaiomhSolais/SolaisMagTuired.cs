using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

/// <summary>
/// 마그 투이레드 (Rare): 【발도】 10 damage (+2/L) + 2K per Light, ignores Block. Next turn you have 1 less energy.
/// Source: 「『마그 투이레드 2차 전투』」.
/// </summary>
public sealed class SolaisMagTuired : SolaisDrawCutCard
{
    public SolaisMagTuired() : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(10);
        WithDamagePerLevel(2);
        WithLightDamageVar("PerLight", 8, 2);
        WithPower<SolaisMagTuiredPower>(1);
        WithTip(typeof(SolaisLightPower));
    }

    protected override int LightMultiplier => 2;

    protected override async Task OnDrawCut(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DrawCutAttack(cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }

    protected override async Task AfterLightConsumed(PlayerChoiceContext choiceContext, CardPlay cardPlay, int consumed)
    {
        await PowerCmd.Apply<SolaisMagTuiredPower>(choiceContext, Owner.Creature,
            DynamicVars["SolaisMagTuiredPower"].BaseValue, Owner.Creature, this);
    }
}
