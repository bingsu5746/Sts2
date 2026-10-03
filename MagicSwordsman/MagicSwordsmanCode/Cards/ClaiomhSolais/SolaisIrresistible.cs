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
/// 저항할 수 없는 빛 (Uncommon): 【발도】 5 damage (+1/L), ignores Block. Instead of adding damage, each Light
/// consumed applies 1 Vulnerable to the target (max 3). Source: 「아무도 저항할 수 없었다」.
/// </summary>
public sealed class SolaisIrresistible : SolaisDrawCutCard
{
    public const int MaxVulnerable = 3;

    public SolaisIrresistible() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(5);
        WithDamagePerLevel(1);
        WithVar("MaxVuln", MaxVulnerable);
        WithTip(typeof(SolaisLightPower));
        WithTip(typeof(VulnerablePower));
    }

    protected override int LightMultiplier => 0;

    protected override async Task OnDrawCut(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DrawCutAttack(cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }

    protected override async Task AfterLightConsumed(PlayerChoiceContext choiceContext, CardPlay cardPlay, int consumed)
    {
        var amount = Math.Min(consumed, MaxVulnerable);
        if (amount <= 0 || cardPlay.Target is not { IsAlive: true } target) return;
        await PowerCmd.Apply<VulnerablePower>(choiceContext, target, amount, Owner.Creature, this);
    }
}
