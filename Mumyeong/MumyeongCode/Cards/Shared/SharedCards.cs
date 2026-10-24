using Mumyeong.MumyeongCode.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Mumyeong.MumyeongCode.Cards.Shared;

// 공용 카드 (spec §6.0): kept to a minimum on purpose; the figure's cards carry the character.

/// <summary>타격 — Basic attack, 1: 6 (9) damage.</summary>
public sealed class MumyeongStrike : MumyeongCard
{
    public MumyeongStrike() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithDamage(6, 3);
        WithTags(CardTag.Strike);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cardPlay) => Hit(ctx, cardPlay);
}

/// <summary>수비 — Basic skill, 1: 5 (8) Block.</summary>
public sealed class MumyeongDefend : MumyeongCard
{
    public MumyeongDefend() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
        WithBlock(5, 3);
        WithTags(CardTag.Defend);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cardPlay) => GainBlock(cardPlay);
}

/// <summary>혼불 — Common skill, 0: draw 1 (2).</summary>
public sealed class SoulFlame : MumyeongCard
{
    public SoulFlame() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithCards(1, 1);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cardPlay) => DrawCards(ctx);
}

/// <summary>마지막 숨 — Common skill, 1: 8 (11) Block.</summary>
public sealed class LastBreath : MumyeongCard
{
    public LastBreath() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(8, 3);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cardPlay) => GainBlock(cardPlay);
}

/// <summary>황천 베기 — Common attack, 2: 13 (17) damage.</summary>
public sealed class UnderworldCut : MumyeongCard
{
    public UnderworldCut() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(13, 4);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cardPlay) => Hit(ctx, cardPlay);
}

/// <summary>객석의 갈채 — Uncommon power, 1: at the start of your turn, gain 3 (5) Block.</summary>
public sealed class AudienceApplause : MumyeongCard
{
    public AudienceApplause() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<ApplausePower>(3, 2);
    }

    protected override Task OnCardPlay(PlayerChoiceContext ctx, CardPlay cardPlay) => Self<ApplausePower>(ctx);
}
