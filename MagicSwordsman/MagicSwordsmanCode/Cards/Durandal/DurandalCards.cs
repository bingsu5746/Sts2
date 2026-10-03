using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Durandal;

// 뒤랑달 — 방어 · 10장 (content doc §2.6). Basic 2 · Common 3 · Uncommon 3 · Rare 2.
// The current-sword effect (flat reduction of attack damage taken) and the HP<=30% switch ban are in DurandalBehavior.
// Lore lines (research doc): 「천사 → 샤를마뉴 → 롤랑」 「'강한 불꽃'」 「성 베드로의 이빨, 성 바실리우스의 피,
// 성 드니의 머리카락」 「바위를 열 번 내리쳐도 흠집조차 나지 않았다」 「롤랑의 틈」 「자기 몸 밑에 숨기고 죽는다」.

/// <summary>성유물의 칼자루 (Basic): 7 Block (+1/L).</summary>
public sealed class DurandalRelicHilt : DurandalCard
{
    public DurandalRelicHilt() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
        WithBlock(7);
        WithBlockPerLevel(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }
}

/// <summary>천사의 검 (Basic): 5 damage, 5 Block (both +1/L). IronWave 5/5.</summary>
public sealed class DurandalAngelsBlade : DurandalCard
{
    public DurandalAngelsBlade() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithDamage(5);
        WithBlock(5);
        WithDamagePerLevel(1);
        WithBlockPerLevel(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}

/// <summary>
/// 흠집 하나 없이 (Common): 5 Block (+1/L). Until your next turn, whenever an attack hits you gain 2 Block
/// (3 from level 3). [확정] "맞을 때 방어도 누적" (1 turn).
/// </summary>
public sealed class DurandalUnscathed : DurandalCard
{
    public DurandalUnscathed() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(5);
        WithBlockPerLevel(1);
        WithCalculatedVar("OnHit", 2, static (card, _) => card is MagicSwordCard { SwordLevel: >= 3 } ? 1 : 0);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<DurandalUnscathedPower>()));
    }

    private int OnHitBlock => SwordLevel >= 3 ? 3 : 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<DurandalUnscathedPower>(choiceContext, Owner.Creature, OnHitBlock, Owner.Creature, this);
    }
}

/// <summary>성 드니의 머리카락 (Common): cost 0, 3 Block (+1/L).</summary>
public sealed class DurandalStDenissHair : DurandalCard
{
    public DurandalStDenissHair() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(3);
        WithBlockPerLevel(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }
}

/// <summary>강한 불꽃 (Common): 6 damage (+1/L). +3 damage if you have Block.</summary>
public sealed class DurandalStrongFlame : DurandalCard
{
    public const int BlockedBonus = 3;

    public DurandalStrongFlame() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(6);
        WithDamagePerLevel(1);
        WithVar("Bonus", BlockedBonus);
    }

    protected override decimal BonusDamage(Creature? target) =>
        IsMutable && Owner?.Creature is { Block: > 0 } ? BlockedBonus : 0m;

    protected override bool ShouldGlowGoldInternal => IsMutable && Owner?.Creature is { Block: > 0 };

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}

/// <summary>
/// 열 번의 내리침 (Uncommon power): this combat, whenever an attack hits you gain 2 (+⌊L/2⌋) Block, +1 while Durandal
/// is the current sword. At most 10 times per turn. [확정] "맞을 때 방어도 누적" (combat).
/// </summary>
public sealed class DurandalTenBlows : DurandalCard
{
    public DurandalTenBlows() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithCalculatedVar("OnHit", 2, static (card, _) => card is MagicSwordCard m ? m.SwordLevel / 2 : 0);
        WithVar("MaxTimes", DurandalTenBlowsPower.MaxTriggersPerTurn);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<DurandalTenBlowsPower>()));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<DurandalTenBlowsPower>(choiceContext, Owner.Creature, 2 + SwordLevel / 2, Owner.Creature,
            this);
    }
}

/// <summary>롤랑의 틈 (Uncommon): deal damage equal to your Block (+2/L). BodySlam pattern (CalculatedDamage).</summary>
public sealed class DurandalRolandsBreach : DurandalCard
{
    public DurandalRolandsBreach() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithCalculatedDamage(0, 1, static (card, _) => card.Owner?.Creature.Block ?? 0);
        WithDamagePerLevel(2);
        WithTip(StaticHoverTip.Block);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_blunt").Execute(choiceContext);
    }
}

/// <summary>
/// 성 바실리우스의 피 (Uncommon): 8 Block (+1/L). The first 성 바실리우스의 피 you play this combat also heals 3 (+⌊L/2⌋) HP.
/// </summary>
public sealed class DurandalStBasilsBlood : DurandalCard
{
    private const string HealUsedKey = "basil_heal_used";

    public DurandalStBasilsBlood() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(8);
        WithBlockPerLevel(1);
        WithCalculatedVar("Heal", 3, static (card, _) => card is MagicSwordCard m ? m.SwordLevel / 2 : 0);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        var state = SwordCombat.Get(Owner);
        if (state == null || state.AddCounter(SwordId.Durandal, HealUsedKey, 1) != 1) return;
        await CreatureCmd.Heal(Owner.Creature, 3 + SwordLevel / 2);
    }
}

/// <summary>
/// 성 베드로의 이빨 (Rare): until your next turn, you take at most 8/8/7/7/6/5 damage at a time. Exhaust.
/// [확정] "피해 상한" — game pattern HardToKillPower.ModifyDamageCap.
/// </summary>
public sealed class DurandalStPetersTooth : DurandalCard
{
    private static readonly int[] CapByLevel = [8, 8, 7, 7, 6, 5];

    public DurandalStPetersTooth() : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithCalculatedVar("Cap", CapByLevel[0],
            static (card, _) => card is MagicSwordCard m ? CapFor(m.SwordLevel) - CapByLevel[0] : 0);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<DurandalStPetersToothPower>()));
    }

    public static int CapFor(int level) => CapByLevel[Math.Clamp(level, 0, CapByLevel.Length - 1)];

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var cap = CapFor(SwordLevel);
        var existing = Owner.Creature.GetPower<DurandalStPetersToothPower>();
        if (existing == null)
        {
            await PowerCmd.Apply<DurandalStPetersToothPower>(choiceContext, Owner.Creature, cap, Owner.Creature, this);
            return;
        }

        // A second copy never raises the cap: keep the lower one.
        if (cap < existing.Amount)
            await PowerCmd.ModifyAmount(choiceContext, existing, cap - existing.Amount, Owner.Creature, this);
    }
}

/// <summary>
/// 몸 밑에 숨긴 검 (Rare power): this combat, while Durandal's reduction is not active (another sword is current),
/// attack hits against you are reduced by half of Durandal's reduction (rounded down, min 1). Cost 1 from level 3.
/// Works regardless of the HP&lt;=30% switch ban.
/// </summary>
public sealed class DurandalHiddenBeneath : DurandalCard
{
    public DurandalHiddenBeneath() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithCalculatedVar("Half", 1,
            static (card, _) => card is MagicSwordCard m
                ? Swords.Behaviors.DurandalBehavior.HalfReductionFor(m.SwordLevel) - 1
                : 0);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<DurandalHiddenBeneathPower>()));
    }

    protected override int? CostAtLevel(int level) => level >= 3 ? 1 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.HasPower<DurandalHiddenBeneathPower>()) return;
        await PowerCmd.Apply<DurandalHiddenBeneathPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }
}
