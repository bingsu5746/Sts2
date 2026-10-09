using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Gram;

// 그람 추가 카드 4장 (Common 2 · Uncommon 1 · Rare 1). Scaling follows content doc §0.2 (Gram exception: attacks at
// half rate; block is not raised by Gram's current-sword effect, so it scales at the normal +1/L).

/// <summary>
/// 사이에 놓인 검 — Gram, Skill, Common, cost 1, self. Gain 7 (+1/L) Block. 【연속】 gain 3 more Block
/// (separate unpowered gain, so the per-level bonus and Dexterity are not counted twice).
/// Lore: Sigurd laid Gram between himself and Brynhild for the three nights he spent with her in Gunnar's shape.
/// </summary>
public sealed class GramBladeBetween : GramCard
{
    public GramBladeBetween() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(7);
        WithBlockPerLevel(1);
        WithVar("ComboBlock", 3);
    }

    protected override bool HasComboEffect => true;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combo = IsCombo;
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (combo)
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars["ComboBlock"].IntValue, ValueProp.Unpowered, null);
    }
}

/// <summary>
/// 부친의 복수 — Gram, Attack, Common, cost 1, all enemies. Deal 5 (+⌊L/2⌋) damage to ALL enemies.
/// Gram's current-sword +L applies to every enemy hit (Gram's only area attack).
/// Lore: with the reforged Gram, Sigurd avenged his father Sigmund on the sons of Hunding.
/// </summary>
public sealed class GramSigmundAvenged : GramCard
{
    public GramSigmundAvenged() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
        WithDamage(5);
    }

    protected override int LevelDamageBonus(int level) => level / 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}

/// <summary>
/// 파프니르의 심장 — Gram, Power, Uncommon, cost 1 (0 from Gram level 5; balance 2026-10-09, was level 3), self. This combat, if Gram is the current
/// sword when your turn starts, draw 1 more card (<see cref="GramFafnirsHeartPower"/>, MachineLearning pattern).
/// Lore: tasting the blood of Fafnir's heart, Sigurd understood the speech of birds.
/// </summary>
public sealed class GramFafnirsHeart : GramCard
{
    public GramFafnirsHeart() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<GramFafnirsHeartPower>(1);
    }

    public override int? CostAtLevel(int level) => level >= 5 ? 0 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<GramFafnirsHeartPower>(choiceContext, Owner.Creature,
            DynamicVars["GramFafnirsHeartPower"].BaseValue, Owner.Creature, this);
    }
}

/// <summary>
/// 최후의 투척 — Gram, Attack, Rare, cost 2, one enemy. Deal 12 (+1/L) damage; if your HP is at or below half,
/// deal it twice (Gram's current-sword +L applies to each hit). Glows while the condition holds.
/// Lore: mortally wounded in his bed, Sigurd hurled Gram after his murderer Guttorm and cut him in two.
/// </summary>
public sealed class GramLastThrow : GramCard
{
    public GramLastThrow() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(12);
        WithDamagePerLevel(1);
    }

    private bool IsDesperate => IsMutable && Owner?.Creature is { MaxHp: > 0 } c && c.CurrentHp * 2 <= c.MaxHp;

    protected override bool ShouldGlowGoldInternal => IsDesperate;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay, IsDesperate ? 2 : 1)
            .WithHitFx("vfx/vfx_giant_horizontal_slash")
            .Execute(choiceContext);
    }
}
