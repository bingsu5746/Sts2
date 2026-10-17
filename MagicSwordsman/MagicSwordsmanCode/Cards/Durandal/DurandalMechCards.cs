using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Durandal;

// 뒤랑달 메커니즘 카드 4장 (2026-10-17, 사용자 요청 "검의 메커니즘을 제대로 반영하는 카드"). Until now no card cared
// about the per-hit reduction itself; these raise it (단단한 날), turn it into damage (튕겨 나는 강철), count the hits it
// softened (롤랑의 반격) and turn the HP<=30% cost into a payoff (롤랑의 최후). Common 2 · Uncommon 2.

/// <summary>
/// 단단한 날 (Common skill): 5 Block (+1/L). Until your next turn Durandal's reduction is +2 (only while its effect is
/// active); each enemy hit it brings to 0 damage gives 1 more card next turn (max 2). Lore: "Durandal" is often
/// explained from Old French "dur" (hard) — a folk etymology, not settled (low–medium confidence, research doc line).
/// </summary>
public sealed class DurandalHardEdge : DurandalCard
{
    public const int ReductionBonus = 2;

    public DurandalHardEdge() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(5);
        WithBlockPerLevel(1);
        WithVar("Bonus", ReductionBonus);
        WithVar("MaxDraws", DurandalHardEdgePower.MaxDraws);
        WithTip(SwordMechanicTips.Effect(SwordId.Durandal));
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<DurandalHardEdgePower>()));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<DurandalHardEdgePower>(choiceContext, Owner.Creature, ReductionBonus, Owner.Creature, this);
    }
}

/// <summary>
/// 롤랑의 반격 (Common attack): 5 damage (+1/L), +3 for each enemy attack hit Durandal's effect reduced since the end of
/// your last turn (<see cref="DurandalBehavior.ReducedHitsSinceLastTurn"/>). Multi-hit enemies feed it most.
/// </summary>
public sealed class DurandalRolandsRiposte : DurandalCard
{
    public const int PerHit = 3;

    public DurandalRolandsRiposte() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(5);
        WithDamagePerLevel(1);
        WithVar("PerHit", PerHit);
        WithTip(SwordMechanicTips.Effect(SwordId.Durandal));
    }

    private int Hits => IsMutable && Owner != null && !LevelPreviewPatches.NeutralCombatInputs
        ? DurandalBehavior.ReducedHitsSinceLastTurn(Owner)
        : 0;

    protected override bool ShouldGlowGoldInternal => Hits > 0;

    protected override decimal BonusDamage(Creature? target) => PerHit * Hits;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}

/// <summary>
/// 튕겨 나는 강철 (Uncommon power): this combat, whenever an enemy attack hits you while Durandal's effect is active,
/// deal damage equal to Durandal's reduction to the attacker. Lore (La Chanson de Roland, laisses 171–173, high
/// confidence): the dying Roland strikes Durandal against the rock to break it, and the steel only grates and springs
/// back unbroken.
/// </summary>
public sealed class DurandalSpringingSteel : DurandalCard
{
    public DurandalSpringingSteel() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithTip(SwordMechanicTips.Effect(SwordId.Durandal));
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<DurandalSpringingSteelPower>()));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.HasPower<DurandalSpringingSteelPower>()) return;
        await PowerCmd.Apply<DurandalSpringingSteelPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }
}

/// <summary>
/// 롤랑의 최후 (Uncommon power): gain 6 Block (+1/L). This combat you can switch to Durandal even at 30% HP or less, and
/// while your HP is at or below 30% Durandal's reduction is +2. Turns the sword's cost into a last stand. Lore (Chanson
/// de Roland, high confidence): Roland dies at Roncevaux lying over Durandal and the Olifant.
/// </summary>
public sealed class DurandalRolandsEnd : DurandalCard
{
    public DurandalRolandsEnd() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(6);
        WithBlockPerLevel(1);
        WithVar("Bonus", DurandalRolandsEndPower.LowHpBonus);
        WithTip(SwordMechanicTips.Effect(SwordId.Durandal));
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<DurandalRolandsEndPower>()));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (!Owner.Creature.HasPower<DurandalRolandsEndPower>())
            await PowerCmd.Apply<DurandalRolandsEndPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
        // Played at low HP: the switch this card attempted was refused before the power existed — switch now.
        if (DurandalBehavior.IsLowHp(Owner.Creature)) await Combat.SwordCombat.SwitchTo(Owner, SwordId.Durandal, choiceContext, SwitchReason.CardPlayed);
    }
}
