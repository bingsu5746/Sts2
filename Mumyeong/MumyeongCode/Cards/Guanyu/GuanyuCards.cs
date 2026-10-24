using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Mumyeong.MumyeongCode.Cards.Guanyu;

// 관우 (spec §6.1): Vigor (활력, the game's VigorPower) builds up for one heavy blow. Card names follow the
// well-known episodes of the 삼국지 tradition (some come from the novel 삼국지연의); they are names, not claims.

public abstract class GuanyuCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : FigureCard(FigureId.Guanyu, cost, type, rarity, target);

/// <summary>청룡언월 — Basic attack, 2: 14 (18).</summary>
public sealed class GuanyuCrescentBlade : GuanyuCard
{
    public GuanyuCrescentBlade() : base(2, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { WithDamage(14, 4); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>단기 — Basic skill, 1: 5 (7) Vigor.</summary>
public sealed class GuanyuLoneRider : GuanyuCard
{
    public GuanyuLoneRider() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self) { WithPower<VigorPower>(5, 2); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<VigorPower>(ctx);
}

/// <summary>일도양단 — Common attack, 2: 13 (17).</summary>
public sealed class GuanyuCleave : GuanyuCard
{
    public GuanyuCleave() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { WithDamage(13, 4); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>미염공 — Common skill, 1: 6 (9) Block, 3 Vigor.</summary>
public sealed class GuanyuBeard : GuanyuCard
{
    public GuanyuBeard() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(6, 3);
        WithPower<VigorPower>(3);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainBlock(cp);
        await Self<VigorPower>(ctx);
    }
}

/// <summary>온주 — Common attack, 1: 8 (11). If you had Vigor, draw 1.</summary>
public sealed class GuanyuWarmWine : GuanyuCard
{
    public GuanyuWarmWine() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(8, 3);
        WithCards(1);
    }

    protected override bool ShouldGlowGoldInternal => OwnerPower<VigorPower>(this) > 0;

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        var hadVigor = OwnerPower<VigorPower>(this) > 0;
        await Hit(ctx, cp);
        if (hadVigor) await DrawCards(ctx);
    }
}

/// <summary>청룡 휘두르기 — Common attack, 1: 7 (10) to ALL enemies.</summary>
public sealed class GuanyuSweep : GuanyuCard
{
    public GuanyuSweep() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { WithDamage(7, 3); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>위세 — Common skill, 0: 2 (4) Vigor.</summary>
public sealed class GuanyuPresence : GuanyuCard
{
    public GuanyuPresence() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { WithPower<VigorPower>(2, 2); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<VigorPower>(ctx);
}

/// <summary>의리 — Common skill, 1: 8 (11) Block.</summary>
public sealed class GuanyuLoyalty : GuanyuCard
{
    public GuanyuLoyalty() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { WithBlock(8, 3); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => GainBlock(cp);
}

/// <summary>오관돌파 — Uncommon attack, 2: 6 (8) x3.</summary>
public sealed class GuanyuFivePasses : GuanyuCard
{
    public const int Hits = 3;
    public GuanyuFivePasses() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(6, 2);
        WithVar("Hits", Hits);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp, Hits);
}

/// <summary>수엄칠군 — Uncommon attack, 2: 10 (14) to ALL enemies, 1 Weak to ALL enemies.</summary>
public sealed class GuanyuFlood : GuanyuCard
{
    public GuanyuFlood() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithDamage(10, 4);
        WithPower<WeakPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Hit(ctx, cp);
        await OnAllEnemies<WeakPower>(ctx);
    }
}

/// <summary>괄골요독 — Uncommon skill, 1: 1 (2) Strength, 6 Block.</summary>
public sealed class GuanyuBoneScrape : GuanyuCard
{
    public GuanyuBoneScrape() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<StrengthPower>(1, 1);
        WithBlock(6);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Self<StrengthPower>(ctx);
        await GainBlock(cp);
    }
}

/// <summary>적토마 — Uncommon skill, 1: draw 2 (3), 2 Vigor.</summary>
public sealed class GuanyuRedHare : GuanyuCard
{
    public GuanyuRedHare() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithCards(2, 1);
        WithPower<VigorPower>(2);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await DrawCards(ctx);
        await Self<VigorPower>(ctx);
    }
}

/// <summary>위진화하 — Uncommon power, 1: at the start of your turn, gain 2 (3) Vigor.</summary>
public sealed class GuanyuAwe : GuanyuCard
{
    public GuanyuAwe() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { WithPower<GuanyuAwePower>(2, 1); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<GuanyuAwePower>(ctx);
}

/// <summary>화용도 — Uncommon skill, 1: 2 (3) Weak, 7 Block.</summary>
public sealed class GuanyuHuarong : GuanyuCard
{
    public GuanyuHuarong() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithPower<WeakPower>(2, 1);
        WithBlock(7);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await OnTarget<WeakPower>(ctx, cp);
        await GainBlock(cp);
    }
}

/// <summary>단도부회 — Uncommon attack, 1: 10 (13), 1 (2) Vulnerable.</summary>
public sealed class GuanyuSingleBlade : GuanyuCard
{
    public GuanyuSingleBlade() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(10, 3);
        WithPower<VulnerablePower>(1, 1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Hit(ctx, cp);
        await OnTarget<VulnerablePower>(ctx, cp);
    }
}

/// <summary>일기당천 — Uncommon attack, 3: 24 (30).</summary>
public sealed class GuanyuThousand : GuanyuCard
{
    public GuanyuThousand() : base(3, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { WithDamage(24, 6); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>무신 — Rare power, 2 (1): whenever you play an Attack, gain 2 Vigor.</summary>
public sealed class GuanyuWarGod : GuanyuCard
{
    public GuanyuWarGod() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<GuanyuWarGodPower>(2);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<GuanyuWarGodPower>(ctx);
}

/// <summary>언월참 — Rare attack, 3: 28 (36).</summary>
public sealed class GuanyuGreenDragonSlash : GuanyuCard
{
    public GuanyuGreenDragonSlash() : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { WithDamage(28, 8); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>도원의 맹세 — Rare skill, 1: 2 (3) Strength, 10 Block. Exhaust.</summary>
public sealed class GuanyuPeachGardenOath : GuanyuCard
{
    public GuanyuPeachGardenOath() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithPower<StrengthPower>(2, 1);
        WithBlock(10);
        WithKeywords(CardKeyword.Exhaust);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Self<StrengthPower>(ctx);
        await GainBlock(cp);
    }
}

/// <summary>의절천추 — Rare power, 1: whenever you play a Skill, gain 1 (2) Vigor.</summary>
public sealed class GuanyuRighteous : GuanyuCard
{
    public GuanyuRighteous() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { WithPower<GuanyuRighteousPower>(1, 1); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<GuanyuRighteousPower>(ctx);
}
