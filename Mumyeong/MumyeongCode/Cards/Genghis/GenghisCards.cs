using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Mumyeong.MumyeongCode.Cards.Genghis;

// 칭기즈 칸 (spec §6.4): area and multi-hit attacks, speeding up whenever an enemy falls.

public abstract class GenghisCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : FigureCard(FigureId.Genghis, cost, type, rarity, target);

/// <summary>기사 — Basic attack, 1: 4 (5) x2.</summary>
public sealed class GenghisMountedArcher : GenghisCard
{
    public const int Hits = 2;

    public GenghisMountedArcher() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithDamage(4, 1);
        WithVar("Hits", Hits);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp, Hits);
}

/// <summary>기마 돌격 — Basic attack, 2: 8 (11) to ALL enemies.</summary>
public sealed class GenghisCavalryCharge : GenghisCard
{
    public GenghisCavalryCharge() : base(2, CardType.Attack, CardRarity.Basic, TargetType.AllEnemies) { WithDamage(8, 3); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>일제 사격 — Common attack, 1: 3 (4) x2 to ALL enemies.</summary>
public sealed class GenghisVolley : GenghisCard
{
    public const int Hits = 2;

    public GenghisVolley() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
        WithDamage(3, 1);
        WithVar("Hits", Hits);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp, Hits);
}

/// <summary>초원의 바람 — Common skill, 1: draw 2 (3), 4 Block.</summary>
public sealed class GenghisSteppeWind : GenghisCard
{
    public GenghisSteppeWind() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithCards(2, 1);
        WithBlock(4);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await DrawCards(ctx);
        await GainBlock(cp);
    }
}

/// <summary>거짓 퇴각 — Common skill, 1: 8 (11) Block, 1 Vulnerable.</summary>
public sealed class GenghisFeignedRetreat : GenghisCard
{
    public GenghisFeignedRetreat() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithBlock(8, 3);
        WithPower<VulnerablePower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainBlock(cp);
        await OnTarget<VulnerablePower>(ctx, cp);
    }
}

/// <summary>창기병 — Common attack, 1: 9 (12).</summary>
public sealed class GenghisLancer : GenghisCard
{
    public GenghisLancer() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { WithDamage(9, 3); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>갈아타기 — Common skill, 0: gain 1 (2) energy. Exhaust.</summary>
public sealed class GenghisRemount : GenghisCard
{
    public GenghisRemount() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithEnergy(1, 1);
        WithKeywords(CardKeyword.Exhaust);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => GainEnergy();
}

/// <summary>척후 — Common attack, 0: 4 (6), draw 1.</summary>
public sealed class GenghisScout : GenghisCard
{
    public GenghisScout() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(4, 2);
        WithCards(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Hit(ctx, cp);
        await DrawCards(ctx);
    }
}

/// <summary>기마 군단 — Uncommon power, 2: start of turn, 2 (3) damage to ALL enemies, +1 every turn.</summary>
public sealed class GenghisHorde : GenghisCard
{
    public GenghisHorde() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { WithPower<GenghisHordePower>(2, 1); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<GenghisHordePower>(ctx);
}

/// <summary>정복 — Uncommon power, 1 (0): whenever an enemy dies, gain 1 energy and draw 1.</summary>
public sealed class GenghisConquest : GenghisCard
{
    public GenghisConquest() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<GenghisConquestPower>(1);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<GenghisConquestPower>(ctx);
}

/// <summary>공성 — Uncommon attack, 2: 18 (24).</summary>
public sealed class GenghisSiege : GenghisCard
{
    public GenghisSiege() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { WithDamage(18, 6); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>화살비 — Uncommon attack, 2: 5 (7) x3 to ALL enemies.</summary>
public sealed class GenghisArrowRain : GenghisCard
{
    public const int Hits = 3;

    public GenghisArrowRain() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithDamage(5, 2);
        WithVar("Hits", Hits);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp, Hits);
}

/// <summary>약탈 — Uncommon attack, 1: 9 (12). If this kills the enemy, gain 15 gold.</summary>
public sealed class GenghisPlunder : GenghisCard
{
    public const int Gold = 15;

    public GenghisPlunder() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(9, 3);
        WithVar("Gold", Gold);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        var attack = await Hit(ctx, cp);
        if (attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
            await PlayerCmd.GainGold(Gold, Owner);
    }
}

/// <summary>공포 — Uncommon skill, 1: 1 (2) Weak and 1 Vulnerable to ALL enemies.</summary>
public sealed class GenghisTerror : GenghisCard
{
    public GenghisTerror() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithPower<WeakPower>(1, 1);
        WithPower<VulnerablePower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await OnAllEnemies<WeakPower>(ctx);
        await OnAllEnemies<VulnerablePower>(ctx);
    }
}

/// <summary>칸의 명령 — Uncommon skill, 1: 2 (3) Strength. Exhaust.</summary>
public sealed class GenghisKhansDecree : GenghisCard
{
    public GenghisKhansDecree() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<StrengthPower>(2, 1);
        WithKeywords(CardKeyword.Exhaust);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<StrengthPower>(ctx);
}

/// <summary>매 사냥 — Uncommon attack, 1: 5 (7) x2, draw 1.</summary>
public sealed class GenghisFalconry : GenghisCard
{
    public const int Hits = 2;

    public GenghisFalconry() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(5, 2);
        WithVar("Hits", Hits);
        WithCards(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Hit(ctx, cp, Hits);
        await DrawCards(ctx);
    }
}

/// <summary>대몽골 — Rare power, 3 (2): whenever you play an Attack, deal 3 damage to ALL enemies.</summary>
public sealed class GenghisGreatMongol : GenghisCard
{
    public GenghisGreatMongol() : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<GenghisEmpirePower>(3);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<GenghisEmpirePower>(ctx);
}

/// <summary>대칸 — Rare attack, 3: 14 (18) x2 to ALL enemies.</summary>
public sealed class GenghisGreatKhan : GenghisCard
{
    public const int Hits = 2;

    public GenghisGreatKhan() : base(3, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
        WithDamage(14, 4);
        WithVar("Hits", Hits);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp, Hits);
}

/// <summary>쿠릴타이 — Rare skill, 1: gain 3 energy, draw 3 (4). Exhaust.</summary>
public sealed class GenghisKurultai : GenghisCard
{
    public GenghisKurultai() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithEnergy(3);
        WithCards(3, 1);
        WithKeywords(CardKeyword.Exhaust);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainEnergy();
        await DrawCards(ctx);
    }
}

/// <summary>끝없는 원정 — Rare skill, 1: 3 (5) 기마 군단.</summary>
public sealed class GenghisEndlessCampaign : GenghisCard
{
    public GenghisEndlessCampaign() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { WithPower<GenghisHordePower>(3, 2); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<GenghisHordePower>(ctx);
}
