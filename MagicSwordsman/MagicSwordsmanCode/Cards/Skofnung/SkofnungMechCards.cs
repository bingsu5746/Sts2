using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Skofnung;

// 스코프눙 메커니즘 카드 3장 (2026-10-17, 사용자 요청 "검의 메커니즘을 제대로 반영하는 카드"):
// 망령 상흔을 "일찍 터뜨리기"(울부짖는 칼) · "방어로 쓰기"(망령의 장막) · "폭발/죽음으로 번지기"(떠나지 않는 혼).
// Common 1 · Uncommon 2.

/// <summary>
/// 울부짖는 칼 (Uncommon skill, Exhaust): if the target has 7+ Spectral Scars (6+ from level 3) they burst NOW
/// (the normal burst: 20 + 4×Skofnung level HP loss, Scars cleared). Otherwise apply 3 Scars.
/// Uses <see cref="SkofnungWoundPower.Burst"/>. Lore (Kormáks saga ch. 9, medium confidence on the exact wording):
/// Kormák wrenched Sköfnung out of its sheath against Skeggi's instructions and the sword howled/groaned.
/// </summary>
public sealed class SkofnungHowlingBlade : SkofnungCard
{
    public const int BaseThreshold = 7;
    public const int FallbackScars = 3;

    public SkofnungHowlingBlade() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithCalculatedVar("BurstAt", BaseThreshold, -1,
            static (card, _) => card is MagicSwordCard { SwordLevel: >= 3 } ? 1 : 0);
        WithVar("Wounds", FallbackScars);
        WithTip(SkofnungTips.Wound);
    }

    private int Threshold => SwordLevel >= 3 ? BaseThreshold - 1 : BaseThreshold;

    protected override bool ShouldGlowGoldInternal =>
        IsMutable && Owner?.Creature.CombatState is { } cs && cs.HittableEnemies.Any(e => WoundsOn(e) >= Threshold);

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is not { IsAlive: true } target) return;
        var wound = target.GetPower<SkofnungWoundPower>();
        if (wound is { } w && w.Amount >= Threshold)
        {
            await w.Burst(choiceContext, Owner.Creature);
            return;
        }

        await PowerCmd.Apply<SkofnungWoundPower>(choiceContext, target, DynamicVars["Wounds"].IntValue,
            Owner.Creature, this);
    }
}

/// <summary>
/// 망령의 장막 (Common skill): 4 Block (+1/L), plus half (rounded down) of the Spectral Scars on ALL enemies combined.
/// Scars never decrease, so this grows over the fight: the defensive use of the stack. Preview-safe (pure).
/// </summary>
public sealed class SkofnungWraithShroud : SkofnungCard
{
    public SkofnungWraithShroud() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(4);
        WithBlockPerLevel(1);
        WithTip(SkofnungTips.Wound);
    }

    private int TotalScars
    {
        get
        {
            if (!IsMutable || LevelPreviewPatches.NeutralCombatInputs) return 0;
            if (Owner?.Creature.CombatState is not { } cs) return 0;
            return cs.HittableEnemies.Sum(WoundsOn);
        }
    }

    protected override bool ShouldGlowGoldInternal => TotalScars >= 2;

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource,
        CardPlay? cardPlay)
    {
        var bonus = base.ModifyBlockAdditive(target, block, props, cardSource, cardPlay);
        if (ReferenceEquals(cardSource, this)) bonus += TotalScars / 2;
        return bonus;
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }
}

/// <summary>
/// 떠나지 않는 혼 (Uncommon power): whenever a Spectral Scar bursts, every OTHER enemy gains 2 Scars; whenever an enemy
/// with Scars dies (not from its own burst), its Scars move to a random other enemy.
/// <see cref="SkofnungLingeringSoulsPower"/>. Lore: the twelve berserkers' souls stay in the blade.
/// </summary>
public sealed class SkofnungLingeringSouls : SkofnungCard
{
    public const int ChainScars = 2;

    public SkofnungLingeringSouls() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("Chain", ChainScars);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<SkofnungLingeringSoulsPower>()));
        WithTip(SkofnungTips.Wound);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SkofnungLingeringSoulsPower>(choiceContext, Owner.Creature, ChainScars, Owner.Creature,
            this);
    }
}
