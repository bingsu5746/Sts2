using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

// 클라이브 솔라시 추가 카드 4장: Common 2 · Uncommon 1 · Rare 1. 2026-10-15: 납도 reworked (Light -> energy/draw),
// 왕위 복귀 added (spend 3 Light -> Strength).
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

/// <summary>
/// 납도 (Uncommon skill): cost 0. Spend 1 Light: gain 1 energy and draw 1 card (2 cards from level 3). Unplayable
/// without Light.
/// Reworked 2026-10-15 (user request #38): it used to be another "consume all Light, X + K per Light" 【발도】 card
/// (followed by 1 Light). Now Light is fuel for tempo instead of damage: putting the blade back into its sheath
/// banks the light as energy for the next stroke. Source: 「죽음의 칼집」 (the sword's sheath in Cath Maige Tuired).
/// </summary>
public sealed class SolaisResheathe : SolaisCard
{
    public const int LightCost = 1;

    public SolaisResheathe() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("LightCost", LightCost);
        WithEnergy(1);
        WithCalculatedVar("Draw", 1, static (card, _) => card is MagicSwordCard { SwordLevel: >= 3 } ? 1 : 0);
        WithTip(typeof(SolaisLightPower));
    }

    protected override bool IsPlayableExtra => !IsMutable || Owner?.PlayerCombatState == null || Light >= LightCost;

    protected override bool ShouldGlowGoldInternal => Light >= LightCost;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!await SolaisLightSpend.TrySpend(choiceContext, Owner, LightCost, this)) return;
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        await CardPileCmd.Draw(choiceContext, SwordLevel >= 3 ? 2 : 1, Owner);
    }
}

/// <summary>
/// 왕위 복귀 (Uncommon skill): cost 1. Spend 3 Light: gain 2 Strength (3 from level 5). Unplayable with less than
/// 3 Light. Added 2026-10-15 (user request #38: "spend some Light stacks for a buff").
/// Lore (Cath Maige Tuired, high confidence): Nuada lost his arm in the first battle and had to give up the kingship;
/// Dían Cécht made him a silver arm and he took the throne back — the stored light restores the king's strength.
/// </summary>
public sealed class SolaisKingshipRestored : SolaisCard
{
    public const int LightCost = 3;

    public SolaisKingshipRestored() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("LightCost", LightCost);
        WithCalculatedVar("Strength", 2, static (card, _) => card is MagicSwordCard { SwordLevel: >= 5 } ? 1 : 0);
        WithTip(typeof(SolaisLightPower));
        WithTip(typeof(StrengthPower));
    }

    protected override bool IsPlayableExtra => !IsMutable || Owner?.PlayerCombatState == null || Light >= LightCost;

    protected override bool ShouldGlowGoldInternal => Light >= LightCost;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!await SolaisLightSpend.TrySpend(choiceContext, Owner, LightCost, this)) return;
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, SwordLevel >= 5 ? 3 : 2, Owner.Creature,
            this);
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
