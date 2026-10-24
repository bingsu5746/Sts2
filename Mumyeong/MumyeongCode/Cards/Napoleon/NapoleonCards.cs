using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Mumyeong.MumyeongCode.Cards.Napoleon;

// 나폴레옹 (spec §6.6): energy and card flow, artillery.

public abstract class NapoleonCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : FigureCard(FigureId.Napoleon, cost, type, rarity, target);

/// <summary>대포 — Basic attack, 2: 15 (20).</summary>
public sealed class NapoleonCannon : NapoleonCard
{
    public NapoleonCannon() : base(2, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { WithDamage(15, 5); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>명령 — Basic skill, 1: draw 1 (2), 1 energy next turn.</summary>
public sealed class NapoleonOrders : NapoleonCard
{
    public NapoleonOrders() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
        WithCards(1, 1);
        WithPower<EnergyNextTurnPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await DrawCards(ctx);
        await Self<EnergyNextTurnPower>(ctx);
    }
}

/// <summary>총검 — Common attack, 1: 8 (11).</summary>
public sealed class NapoleonBayonet : NapoleonCard
{
    public NapoleonBayonet() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { WithDamage(8, 3); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>산탄 — Common attack, 1: 5 (7) to ALL enemies.</summary>
public sealed class NapoleonGrapeshot : NapoleonCard
{
    public NapoleonGrapeshot() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { WithDamage(5, 2); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>강행군 — Common skill, 1: draw 2 (3).</summary>
public sealed class NapoleonForcedMarch : NapoleonCard
{
    public NapoleonForcedMarch() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { WithCards(2, 1); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => DrawCards(ctx);
}

/// <summary>방진 — Common skill, 1: 8 (11) Block.</summary>
public sealed class NapoleonInfantrySquare : NapoleonCard
{
    public NapoleonInfantrySquare() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { WithBlock(8, 3); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => GainBlock(cp);
}

/// <summary>법전 — Common skill, 0: draw 1, 3 (5) Block.</summary>
public sealed class NapoleonCivilCode : NapoleonCard
{
    public NapoleonCivilCode() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithCards(1);
        WithBlock(3, 2);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainBlock(cp);
        await DrawCards(ctx);
    }
}

/// <summary>흉갑 기병 — Common attack, 2: 7 (9) x2.</summary>
public sealed class NapoleonCuirassiers : NapoleonCard
{
    public const int Hits = 2;

    public NapoleonCuirassiers() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(7, 2);
        WithVar("Hits", Hits);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp, Hits);
}

/// <summary>포병대 — Uncommon power, 1: at the start of your turn, deal 6 (9) damage to a random enemy.</summary>
public sealed class NapoleonArtillery : NapoleonCard
{
    public NapoleonArtillery() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { WithPower<NapoleonArtilleryPower>(6, 3); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<NapoleonArtilleryPower>(ctx);
}

/// <summary>전격전 — Uncommon power, 1 (0): every 4th card you play in a turn gives 1 energy.</summary>
public sealed class NapoleonBlitz : NapoleonCard
{
    public NapoleonBlitz() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<NapoleonBlitzPower>(1);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<NapoleonBlitzPower>(ctx);
}

/// <summary>아우스터리츠 — Uncommon attack, 2: 12 (16), 2 Weak.</summary>
public sealed class NapoleonAusterlitz : NapoleonCard
{
    public NapoleonAusterlitz() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(12, 4);
        WithPower<WeakPower>(2);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Hit(ctx, cp);
        await OnTarget<WeakPower>(ctx, cp);
    }
}

/// <summary>대포대 — Uncommon attack, 3: 15 (20) to ALL enemies.</summary>
public sealed class NapoleonGrandBattery : NapoleonCard
{
    public NapoleonGrandBattery() : base(3, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { WithDamage(15, 5); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>기동 — Uncommon skill, 1: 7 (10) Block, draw 1.</summary>
public sealed class NapoleonManeuver : NapoleonCard
{
    public NapoleonManeuver() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(7, 3);
        WithCards(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainBlock(cp);
        await DrawCards(ctx);
    }
}

/// <summary>근위대 — Uncommon skill, 2: 16 (21) Block.</summary>
public sealed class NapoleonImperialGuard : NapoleonCard
{
    public NapoleonImperialGuard() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { WithBlock(16, 5); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => GainBlock(cp);
}

/// <summary>야망 — Uncommon skill, 1: gain 2 (3) energy. Exhaust.</summary>
public sealed class NapoleonAmbition : NapoleonCard
{
    public NapoleonAmbition() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithEnergy(2, 1);
        WithKeywords(CardKeyword.Exhaust);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => GainEnergy();
}

/// <summary>징병 — Uncommon skill, 1: 1 (2) Strength, draw 1.</summary>
public sealed class NapoleonConscription : NapoleonCard
{
    public NapoleonConscription() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<StrengthPower>(1, 1);
        WithCards(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Self<StrengthPower>(ctx);
        await DrawCards(ctx);
    }
}

/// <summary>대관식 — Rare power, 3 (2): +1 max energy.</summary>
public sealed class NapoleonCoronation : NapoleonCard
{
    public NapoleonCoronation() : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<NapoleonCoronationPower>(1);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<NapoleonCoronationPower>(ctx);
}

/// <summary>백일천하 — Rare skill, 0: gain 3 energy, draw 3 (4). Exhaust.</summary>
public sealed class NapoleonHundredDays : NapoleonCard
{
    public NapoleonHundredDays() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
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

/// <summary>대육군 — Rare attack, 3: 6 (8) x5.</summary>
public sealed class NapoleonGrandeArmee : NapoleonCard
{
    public const int Hits = 5;

    public NapoleonGrandeArmee() : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(6, 2);
        WithVar("Hits", Hits);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp, Hits);
}

/// <summary>불가능은 없다 — Rare skill, 1 (0): this turn, your next card is played twice (game DuplicationPower). Exhaust.</summary>
public sealed class NapoleonNothingImpossible : NapoleonCard
{
    public NapoleonNothingImpossible() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithPower<DuplicationPower>(1);
        WithKeywords(CardKeyword.Exhaust);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<DuplicationPower>(ctx);
}
