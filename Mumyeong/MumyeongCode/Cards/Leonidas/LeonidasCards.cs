using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Mumyeong.MumyeongCode.Cards.Leonidas;

// 레오니다스 (spec §6.3): Block, 방진 (Block is kept), 반격 (thorns) and Block turned into damage.

public abstract class LeonidasCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : FigureCard(FigureId.Leonidas, cost, type, rarity, target);

/// <summary>방패벽 — Basic skill, 1: 8 (11) Block.</summary>
public sealed class LeonidasShieldWall : LeonidasCard
{
    public LeonidasShieldWall() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self) { WithBlock(8, 3); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => GainBlock(cp);
}

/// <summary>창 찌르기 — Basic attack, 1: 7 (10).</summary>
public sealed class LeonidasSpearThrust : LeonidasCard
{
    public LeonidasSpearThrust() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { WithDamage(7, 3); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>호플리테스 — Common skill, 1: 6 (9) Block, 2 반격.</summary>
public sealed class LeonidasHoplite : LeonidasCard
{
    public LeonidasHoplite() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(6, 3);
        WithPower<LeonidasCounterPower>(2);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainBlock(cp);
        await Self<LeonidasCounterPower>(ctx);
    }
}

/// <summary>방패 밀치기 — Common attack, 1 (0): deal damage equal to your Block.</summary>
public sealed class LeonidasShieldBash : LeonidasCard
{
    public LeonidasShieldBash() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithCalculatedDamage(0, 1, static (card, _) => OwnerBlock(card));
        WithCostUpgradeBy(-1);
        WithTip(StaticHoverTip.Block);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>협곡 — Common skill, 2: 15 (20) Block.</summary>
public sealed class LeonidasNarrowPass : LeonidasCard
{
    public LeonidasNarrowPass() : base(2, CardType.Skill, CardRarity.Common, TargetType.Self) { WithBlock(15, 5); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => GainBlock(cp);
}

/// <summary>아고게 — Common skill, 1: 5 (8) Block, draw 1.</summary>
public sealed class LeonidasAgoge : LeonidasCard
{
    public LeonidasAgoge() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(5, 3);
        WithCards(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainBlock(cp);
        await DrawCards(ctx);
    }
}

/// <summary>밀집 찌르기 — Common attack, 1: 5 (7) x2.</summary>
public sealed class LeonidasMassedThrust : LeonidasCard
{
    public const int Hits = 2;

    public LeonidasMassedThrust() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(5, 2);
        WithVar("Hits", Hits);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp, Hits);
}

/// <summary>버티기 — Common skill, 0: 4 (7) Block.</summary>
public sealed class LeonidasBrace : LeonidasCard
{
    public LeonidasBrace() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { WithBlock(4, 3); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => GainBlock(cp);
}

/// <summary>팔랑크스 — Uncommon power, 2 (1): 방진 — Block is not removed at the start of your turn.</summary>
public sealed class LeonidasPhalanx : LeonidasCard
{
    public LeonidasPhalanx() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithCostUpgradeBy(-1);
        WithTip(StaticHoverTip.Block);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<LeonidasPhalanxPower>(ctx, 1m);
}

/// <summary>청동 가시 — Uncommon power, 1: 3 (4) 반격.</summary>
public sealed class LeonidasBronzeSpikes : LeonidasCard
{
    public LeonidasBronzeSpikes() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { WithPower<LeonidasCounterPower>(3, 1); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<LeonidasCounterPower>(ctx);
}

/// <summary>역습 — Uncommon attack, 1: 8 (11). If you have 10+ Block, hit twice.</summary>
public sealed class LeonidasCounterattack : LeonidasCard
{
    public const int BlockNeeded = 10;

    public LeonidasCounterattack() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(8, 3);
        WithVar("BlockNeeded", BlockNeeded);
    }

    protected override bool ShouldGlowGoldInternal => OwnerBlock(this) >= BlockNeeded;

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) =>
        Hit(ctx, cp, OwnerBlock(this) >= BlockNeeded ? 2 : 1);
}

/// <summary>300 — Uncommon attack, 2: 9 (12) to ALL enemies, 6 Block.</summary>
public sealed class LeonidasThreeHundred : LeonidasCard
{
    public LeonidasThreeHundred() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithDamage(9, 3);
        WithBlock(6);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Hit(ctx, cp);
        await GainBlock(cp);
    }
}

/// <summary>방패를 들고 오라 — Uncommon skill, 1: 9 (12) Block, 5 Block next turn.</summary>
public sealed class LeonidasWithYourShield : LeonidasCard
{
    public LeonidasWithYourShield() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(9, 3);
        WithPower<BlockNextTurnPower>(5);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainBlock(cp);
        await Self<BlockNextTurnPower>(ctx);
    }
}

/// <summary>뜨거운 문 — Uncommon skill, 1: 1 Weak to ALL enemies, 6 (8) Block.</summary>
public sealed class LeonidasHotGates : LeonidasCard
{
    public LeonidasHotGates() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithPower<WeakPower>(1);
        WithBlock(6, 2);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await OnAllEnemies<WeakPower>(ctx);
        await GainBlock(cp);
    }
}

/// <summary>그늘에서 싸우리라 — Uncommon skill, 1: 8 (11) Block, 1 Blur.</summary>
public sealed class LeonidasInTheShade : LeonidasCard
{
    public LeonidasInTheShade() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(8, 3);
        WithPower<BlurPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainBlock(cp);
        await Self<BlurPower>(ctx);
    }
}

/// <summary>최후의 방진 — Uncommon skill, 2 (1): gain Block equal to your Block. Exhaust.</summary>
public sealed class LeonidasLastStand : LeonidasCard
{
    public LeonidasLastStand() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithCostUpgradeBy(-1);
        WithTip(StaticHoverTip.Block);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        var block = Owner.Creature.Block;
        if (block > 0) await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Unpowered, cp);
    }
}

/// <summary>와서 가져가라 — Rare power, 2 (1): at the end of your turn, deal half your Block to ALL enemies.</summary>
public sealed class LeonidasMolonLabe : LeonidasCard
{
    public LeonidasMolonLabe() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithCostUpgradeBy(-1);
        WithTip(StaticHoverTip.Block);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<LeonidasMolonLabePower>(ctx, 1m);
}

/// <summary>희생 — Rare skill, 1: lose 6 HP, gain 20 (26) Block and 3 반격. Exhaust.</summary>
public sealed class LeonidasSacrifice : LeonidasCard
{
    public const int HpLoss = 6;

    public LeonidasSacrifice() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithVar("HpLoss", HpLoss);
        WithBlock(20, 6);
        WithPower<LeonidasCounterPower>(3);
        WithKeywords(CardKeyword.Exhaust);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await LoseHp(ctx, HpLoss);
        await GainBlock(cp);
        await Self<LeonidasCounterPower>(ctx);
    }
}

/// <summary>왕의 창 — Rare attack, 2: 10 (15) + your Block.</summary>
public sealed class LeonidasKingsSpear : LeonidasCard
{
    public LeonidasKingsSpear() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithCalculatedDamage(10, 1, static (card, _) => OwnerBlock(card), upgrade: 5);
        WithTip(StaticHoverTip.Block);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>테르모필레 — Rare power, 1: whenever you play a Skill, deal 2 (3) damage to a random enemy.</summary>
public sealed class LeonidasThermopylae : LeonidasCard
{
    public LeonidasThermopylae() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { WithPower<LeonidasThermopylaePower>(2, 1); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<LeonidasThermopylaePower>(ctx);
}
