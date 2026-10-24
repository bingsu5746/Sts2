using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Mumyeong.MumyeongCode.Cards.YiSunsin;

// 이순신 (spec §6.5): 포격 (YiCannonPower) hits ALL enemies at the end of every turn; defence and next-turn preparation.

public abstract class YiCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : FigureCard(FigureId.YiSunsin, cost, type, rarity, target);

/// <summary>천자총통 — Basic skill, 1: 3 (4) 포격.</summary>
public sealed class YiCheonjaCannon : YiCard
{
    public YiCheonjaCannon() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self) { WithPower<YiCannonPower>(3, 1); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<YiCannonPower>(ctx);
}

/// <summary>판옥선 — Basic skill, 1: 7 (10) Block.</summary>
public sealed class YiPanokseon : YiCard
{
    public YiPanokseon() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self) { WithBlock(7, 3); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => GainBlock(cp);
}

/// <summary>일제 포격 — Common attack, 1: 6 (9) to ALL enemies.</summary>
public sealed class YiBroadside : YiCard
{
    public YiBroadside() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { WithDamage(6, 3); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>화전 — Common attack, 1: 8 (11), 1 Vulnerable.</summary>
public sealed class YiFireArrow : YiCard
{
    public YiFireArrow() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(8, 3);
        WithPower<VulnerablePower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Hit(ctx, cp);
        await OnTarget<VulnerablePower>(ctx, cp);
    }
}

/// <summary>난중일기 — Common skill, 1: draw 2 (3).</summary>
public sealed class YiWarDiary : YiCard
{
    public YiWarDiary() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { WithCards(2, 1); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => DrawCards(ctx);
}

/// <summary>경계 — Common skill, 1: 6 (9) Block, draw 1 next turn.</summary>
public sealed class YiWatch : YiCard
{
    public YiWatch() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(6, 3);
        WithPower<DrawCardsNextTurnPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainBlock(cp);
        await Self<DrawCardsNextTurnPower>(ctx);
    }
}

/// <summary>출정 준비 — Common skill, 1: 5 (8) Block, 1 energy next turn.</summary>
public sealed class YiPrepareToSail : YiCard
{
    public YiPrepareToSail() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(5, 3);
        WithPower<EnergyNextTurnPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainBlock(cp);
        await Self<EnergyNextTurnPower>(ctx);
    }
}

/// <summary>장전 — Common skill, 0: 2 (3) 포격.</summary>
public sealed class YiReload : YiCard
{
    public YiReload() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { WithPower<YiCannonPower>(2, 1); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<YiCannonPower>(ctx);
}

/// <summary>거북선 — Uncommon power, 2: at the end of your turn, gain 5 (7) Block.</summary>
public sealed class YiTurtleShip : YiCard
{
    public YiTurtleShip() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { WithPower<YiTurtleShipPower>(5, 2); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<YiTurtleShipPower>(ctx);
}

/// <summary>학익진 — Uncommon power, 1: at the end of your turn, 4 (6) damage to ALL enemies per unspent energy.</summary>
public sealed class YiCraneWing : YiCard
{
    public YiCraneWing() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { WithPower<YiCraneWingPower>(4, 2); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<YiCraneWingPower>(ctx);
}

/// <summary>명량 — Uncommon attack, 2: 8 (12) + 4 per enemy.</summary>
public sealed class YiMyeongnyang : YiCard
{
    public YiMyeongnyang() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithCalculatedDamage(8, 4, static (card, _) => EnemyCount(card), upgrade: 4);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>옥포 — Uncommon attack, 1: 7 (10), 1 포격.</summary>
public sealed class YiOkpo : YiCard
{
    public YiOkpo() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(7, 3);
        WithPower<YiCannonPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Hit(ctx, cp);
        await Self<YiCannonPower>(ctx);
    }
}

/// <summary>한산도 — Uncommon skill, 2: 5 (7) 포격, 8 Block.</summary>
public sealed class YiHansando : YiCard
{
    public YiHansando() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<YiCannonPower>(5, 2);
        WithBlock(8);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Self<YiCannonPower>(ctx);
        await GainBlock(cp);
    }
}

/// <summary>백의종군 — Uncommon skill, 1: 10 (14) Block. Retain.</summary>
public sealed class YiServeAsCommoner : YiCard
{
    public YiServeAsCommoner() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(10, 4);
        WithKeywords(CardKeyword.Retain);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => GainBlock(cp);
}

/// <summary>쇠사슬 — Uncommon skill, 1: 1 Weak to ALL enemies, 1 (2) 포격.</summary>
public sealed class YiIronChain : YiCard
{
    public YiIronChain() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithPower<WeakPower>(1);
        WithPower<YiCannonPower>(1, 1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await OnAllEnemies<WeakPower>(ctx);
        await Self<YiCannonPower>(ctx);
    }
}

/// <summary>필사즉생 — Uncommon skill, 1: 2 (3) Strength. Exhaust.</summary>
public sealed class YiResolveToDie : YiCard
{
    public YiResolveToDie() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<StrengthPower>(2, 1);
        WithKeywords(CardKeyword.Exhaust);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<StrengthPower>(ctx);
}

/// <summary>노량 — Rare attack, 3: 16 (22) to ALL enemies, 3 포격.</summary>
public sealed class YiNoryang : YiCard
{
    public YiNoryang() : base(3, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
        WithDamage(16, 6);
        WithPower<YiCannonPower>(3);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Hit(ctx, cp);
        await Self<YiCannonPower>(ctx);
    }
}

/// <summary>열두 척 — Rare skill, 1: 6 (8) Block per enemy, 2 포격.</summary>
public sealed class YiTwelveShips : YiCard
{
    public YiTwelveShips() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithCalculatedBlock(0, 6, static (card, _) => EnemyCount(card), bonusUpgrade: 2);
        WithPower<YiCannonPower>(2);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainBlock(cp);
        await Self<YiCannonPower>(ctx);
    }
}

/// <summary>통제사 — Rare power, 2 (1): at the start of your turn, gain 2 포격.</summary>
public sealed class YiCommander : YiCard
{
    public YiCommander() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<YiCommanderPower>(2);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<YiCommanderPower>(ctx);
}

/// <summary>나의 죽음을 알리지 마라 — Rare skill, 1: double your 포격. Exhaust. (Upgrade: cost 0.)</summary>
public sealed class YiDoNotAnnounce : YiCard
{
    public YiDoNotAnnounce() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithCostUpgradeBy(-1);
    }

    protected override bool ShouldGlowGoldInternal => OwnerPower<YiCannonPower>(this) > 0;

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        var cannon = OwnerPower<YiCannonPower>(this);
        if (cannon > 0) await Self<YiCannonPower>(ctx, cannon);
    }
}
