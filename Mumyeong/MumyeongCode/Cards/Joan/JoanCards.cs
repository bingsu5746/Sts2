using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Mumyeong.MumyeongCode.Cards.Joan;

// 잔 다르크 (spec §6.2): 신념 (JoanFaithPower) = Block at the end of every turn, never decays; late cards turn Faith
// into damage.

public abstract class JoanCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : FigureCard(FigureId.Joan, cost, type, rarity, target);

/// <summary>깃발 들기 — Basic skill, 1: 2 (3) Faith.</summary>
public sealed class JoanRaiseBanner : JoanCard
{
    public JoanRaiseBanner() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self) { WithPower<JoanFaithPower>(2, 1); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<JoanFaithPower>(ctx);
}

/// <summary>성녀의 검 — Basic attack, 1: 7 (10), 1 Faith.</summary>
public sealed class JoanMaidensSword : JoanCard
{
    public JoanMaidensSword() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithDamage(7, 3);
        WithPower<JoanFaithPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Hit(ctx, cp);
        await Self<JoanFaithPower>(ctx);
    }
}

/// <summary>돌격 — Common attack, 1: 9 (12).</summary>
public sealed class JoanCharge : JoanCard
{
    public JoanCharge() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { WithDamage(9, 3); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>기도 — Common skill, 1: 6 (9) Block, 1 Faith.</summary>
public sealed class JoanPrayer : JoanCard
{
    public JoanPrayer() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(6, 3);
        WithPower<JoanFaithPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainBlock(cp);
        await Self<JoanFaithPower>(ctx);
    }
}

/// <summary>목소리 — Common skill, 0: draw 1 (2), 1 Faith.</summary>
public sealed class JoanVoices : JoanCard
{
    public JoanVoices() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithCards(1, 1);
        WithPower<JoanFaithPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await DrawCards(ctx);
        await Self<JoanFaithPower>(ctx);
    }
}

/// <summary>포위 풀기 — Common attack, 2: 9 (12) to ALL enemies.</summary>
public sealed class JoanLiftTheSiege : JoanCard
{
    public JoanLiftTheSiege() : base(2, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { WithDamage(9, 3); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>결심 — Common skill, 1: 7 (10) Block. If you have 5+ Faith, draw 1.</summary>
public sealed class JoanResolve : JoanCard
{
    public const int FaithNeeded = 5;

    public JoanResolve() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(7, 3);
        WithCards(1);
        WithVar("FaithNeeded", FaithNeeded);
    }

    protected override bool ShouldGlowGoldInternal => OwnerPower<JoanFaithPower>(this) >= FaithNeeded;

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainBlock(cp);
        if (OwnerPower<JoanFaithPower>(this) >= FaithNeeded) await DrawCards(ctx);
    }
}

/// <summary>집결 — Common skill, 1: 2 (3) Faith... plus 4 Block so it is never a dead card.</summary>
public sealed class JoanRally : JoanCard
{
    public JoanRally() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithPower<JoanFaithPower>(2, 1);
        WithBlock(4);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Self<JoanFaithPower>(ctx);
        await GainBlock(cp);
    }
}

/// <summary>오를레앙 — Uncommon attack, 2: 8 (11) x2, 1 Faith.</summary>
public sealed class JoanOrleans : JoanCard
{
    public const int Hits = 2;

    public JoanOrleans() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(8, 3);
        WithVar("Hits", Hits);
        WithPower<JoanFaithPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Hit(ctx, cp, Hits);
        await Self<JoanFaithPower>(ctx);
    }
}

/// <summary>랭스의 대관식 — Uncommon skill, 1 (0): gain 2 energy, 2 Faith. Exhaust.</summary>
public sealed class JoanCoronation : JoanCard
{
    public JoanCoronation() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithEnergy(2);
        WithPower<JoanFaithPower>(2);
        WithKeywords(CardKeyword.Exhaust);
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainEnergy();
        await Self<JoanFaithPower>(ctx);
    }
}

/// <summary>흔들리지 않는 깃발 — Uncommon power, 1: at the start of your turn, gain 1 (2) Faith.</summary>
public sealed class JoanUnwaveringBanner : JoanCard
{
    public JoanUnwaveringBanner() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { WithPower<JoanBannerPower>(1, 1); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<JoanBannerPower>(ctx);
}

/// <summary>심문 — Uncommon skill, 1: 2 (3) Vulnerable and 1 Weak.</summary>
public sealed class JoanTrial : JoanCard
{
    public JoanTrial() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithPower<VulnerablePower>(2, 1);
        WithPower<WeakPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await OnTarget<VulnerablePower>(ctx, cp);
        await OnTarget<WeakPower>(ctx, cp);
    }
}

/// <summary>화형대 — Uncommon power, 1: whenever you lose HP, gain 1 (2) Faith.</summary>
public sealed class JoanPyre : JoanCard
{
    public JoanPyre() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { WithPower<JoanPyrePower>(1, 1); }
    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<JoanPyrePower>(ctx);
}

/// <summary>열의 — Uncommon attack, 1: 4 + 2 per Faith (6 + 3 per Faith).</summary>
public sealed class JoanZeal : JoanCard
{
    public JoanZeal() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithCalculatedDamage(4, 2, static (card, _) => OwnerPower<JoanFaithPower>(card), upgrade: 2, bonusUpgrade: 1);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>성광 — Uncommon attack, 1: 6 (8) to ALL enemies, 1 Weak to ALL enemies.</summary>
public sealed class JoanHolyLight : JoanCard
{
    public JoanHolyLight() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithDamage(6, 2);
        WithPower<WeakPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await Hit(ctx, cp);
        await OnAllEnemies<WeakPower>(ctx);
    }
}

/// <summary>굴하지 않음 — Uncommon skill, 2: 14 (18) Block, 1 Faith.</summary>
public sealed class JoanUnyielding : JoanCard
{
    public JoanUnyielding() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(14, 4);
        WithPower<JoanFaithPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await GainBlock(cp);
        await Self<JoanFaithPower>(ctx);
    }
}

/// <summary>순교 — Rare skill, 1: lose 6 HP, gain 6 (8) Faith. Exhaust.</summary>
public sealed class JoanMartyrdom : JoanCard
{
    public const int HpLoss = 6;

    public JoanMartyrdom() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithVar("HpLoss", HpLoss);
        WithPower<JoanFaithPower>(6, 2);
        WithKeywords(CardKeyword.Exhaust);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        await LoseHp(ctx, HpLoss);
        await Self<JoanFaithPower>(ctx);
    }
}

/// <summary>심판 — Rare power, 2 (1): at the end of your turn, deal damage equal to your Faith to ALL enemies.</summary>
public sealed class JoanJudgment : JoanCard
{
    public JoanJudgment() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<JoanJudgmentPower>(1);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Self<JoanJudgmentPower>(ctx);
}

/// <summary>철회서를 찢다 — Rare attack, 2: 10 + 3 per Faith (14 + 3 per Faith).</summary>
public sealed class JoanTearTheAbjuration : JoanCard
{
    public JoanTearTheAbjuration() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithCalculatedDamage(10, 3, static (card, _) => OwnerPower<JoanFaithPower>(card), upgrade: 4);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp) => Hit(ctx, cp);
}

/// <summary>성인 — Rare skill, 1 (0): double your Faith. Exhaust.</summary>
public sealed class JoanSainthood : JoanCard
{
    public JoanSainthood() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithCostUpgradeBy(-1);
    }

    protected override bool ShouldGlowGoldInternal => OwnerPower<JoanFaithPower>(this) > 0;

    protected override async Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cp)
    {
        var faith = OwnerPower<JoanFaithPower>(this);
        if (faith > 0) await Self<JoanFaithPower>(ctx, faith);
    }
}
