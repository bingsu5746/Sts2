using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Skofnung;

// 스코프눙 추가 카드 4장 (Common 2 · Uncommon 1 · Rare 1). Lore: Hrólfr kraki fell with his champions at Lejre;
// Kormáks saga: when Sköfnung is drawn a small serpent creeps out from under the hilt.

/// <summary>레이레 함락 (Common): 4 damage (+1/L) to ALL enemies; +3 per hit to enemies that already have scars.</summary>
public sealed class SkofnungFallOfLejre : SkofnungCard
{
    public const int ScarBonus = 3;

    public SkofnungFallOfLejre() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
        WithDamage(4);
        WithDamagePerLevel(1);
        WithVar("ScarBonus", ScarBonus);
        WithTip(SkofnungTips.Wound);
    }

    protected override decimal BonusDamage(Creature? target) => WoundsOn(target) > 0 ? ScarBonus : 0m;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}

/// <summary>자루의 뱀 (Common skill): the target loses HP equal to its scars now (ignores Block). Draw 1 (2 from level 3).</summary>
public sealed class SkofnungHiltSerpent : SkofnungCard
{
    public SkofnungHiltSerpent() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithCards(1);
        WithTip(SkofnungTips.Wound);
    }

    protected override bool ShouldGlowGoldInternal =>
        IsMutable && Owner?.Creature.CombatState is { } cs && cs.HittableEnemies.Any(e => WoundsOn(e) > 0);

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is { IsAlive: true } target && WoundsOn(target) is var wounds and > 0)
        {
            VfxCmd.PlayOnCreature(target, "vfx/vfx_attack_slash");
            await CreatureCmd.Damage(choiceContext, target, wounds, ValueProp.Unblockable | ValueProp.Unpowered,
                Owner.Creature, this);
        }

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue + (SwordLevel >= 3 ? 1 : 0), Owner);
    }
}

/// <summary>
/// 떠도는 망령 (Uncommon): 7 damage (+1/L). Then every OTHER enemy gains half the target's scars (rounded down;
/// the full amount from level 3). The scars are read after the attack, so the current-sword scar counts.
/// </summary>
public sealed class SkofnungWanderingWraiths : SkofnungCard
{
    public SkofnungWanderingWraiths() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(7);
        WithDamagePerLevel(1);
        WithTip(SkofnungTips.Wound);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is not { } target) return;
        var before = WoundsOn(target);
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);

        // A killed target may lose its powers: fall back to the count before the attack.
        var wounds = target.IsAlive ? WoundsOn(target) : before;
        var spread = SwordLevel >= 3 ? wounds : wounds / 2;
        if (spread <= 0 || Owner.Creature.CombatState is not { } cs) return;
        var others = cs.HittableEnemies.Where(e => e != target && e.IsAlive).ToList();
        if (others.Count > 0)
            await PowerCmd.Apply<SkofnungWoundPower>(choiceContext, others, spread, Owner.Creature, this);
    }
}

/// <summary>
/// 흐롤프의 용사들 (Rare power): whenever you apply 망령 상흔, apply 1 more. Cost 1 from level 4.
/// </summary>
public sealed class SkofnungHrolfsChampions : SkofnungCard
{
    public SkofnungHrolfsChampions() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithVar("ExtraWounds", 1);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<SkofnungHrolfsChampionsPower>()));
        WithTip(SkofnungTips.Wound);
    }

    protected override int? CostAtLevel(int level) => level >= 4 ? 1 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SkofnungHrolfsChampionsPower>(choiceContext, Owner.Creature,
            DynamicVars["ExtraWounds"].IntValue, Owner.Creature, this);
    }
}
