using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

// 클라이브 솔라시 추가 카드 4장: Common 2 · Uncommon 1 · Rare 1.
// Lore: 1차 마그 투이레드(피르 볼그와의 싸움), 핀디아스의 스승 우스키아스, '빛의 검'(클라이브 솔라시라는 이름의 뜻).

/// <summary>피르 볼그 (Common): 6 damage (+1/L). Gain 1 Light. An attack that charges instead of spending.</summary>
public sealed class SolaisFirBolg : SolaisCard
{
    public SolaisFirBolg() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(6);
        WithDamagePerLevel(1);
        WithPower<SolaisLightPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        await SolaisLight.Gain(choiceContext, Owner, DynamicVars["SolaisLightPower"].IntValue, this);
    }
}

/// <summary>우스키아스 (Common): gain 2 Light. Uscias, the master of Findias the sword was brought from.</summary>
public sealed class SolaisUscias : SolaisCard
{
    public SolaisUscias() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithPower<SolaisLightPower>(2);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await SolaisLight.Gain(choiceContext, Owner, DynamicVars["SolaisLightPower"].IntValue, this);
    }
}

/// <summary>다시 칼집으로 (Uncommon): 【발도】 4 damage (+1/L) + K per Light, ignores Block; then gain 1 Light.</summary>
public sealed class SolaisResheathe : SolaisDrawCutCard
{
    public SolaisResheathe() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(4);
        WithDamagePerLevel(1);
        WithLightDamageVar("PerLight", 4, 1);
        WithPower<SolaisLightPower>(1);
    }

    protected override async Task OnDrawCut(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DrawCutAttack(cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }

    protected override async Task AfterLightConsumed(PlayerChoiceContext choiceContext, CardPlay cardPlay, int consumed)
    {
        await SolaisLight.Gain(choiceContext, Owner, DynamicVars["SolaisLightPower"].IntValue, this);
    }
}

/// <summary>
/// 빛의 검 (Rare power): when you end your turn with Claíomh Solais current, deal 2 (3 from level 3) damage per Light
/// to ALL enemies (Light is not consumed). The name Claíomh Solais means "sword of light".
/// </summary>
public sealed class SolaisSwordOfLight : SolaisCard
{
    public SolaisSwordOfLight() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<SolaisSwordOfLightPower>(1);
        WithCalculatedVar("PerLight", SolaisSwordOfLightPower.DamagePerLight,
            static (card, _) => card is MagicSwordCard { SwordLevel: >= SolaisSwordOfLightPower.HighFromLevel } ? 1 : 0);
        WithTip(typeof(SolaisLightPower));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SolaisSwordOfLightPower>(choiceContext, Owner.Creature,
            DynamicVars["SolaisSwordOfLightPower"].BaseValue, Owner.Creature, this);
    }
}
