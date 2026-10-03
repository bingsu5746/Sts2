using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Common;

// Common (non-sword) card pool — Common rarity (content doc §4.2, 15 cards). Sword == null: these cards never change
// the current sword (except the 검 바꾸기 family) and so carry the current sword's effect. Upgradable at the Smith.

/// <summary>날아드는 칼 — Attack, cost 1: switch to one of your swords, then deal 6 (9) damage.</summary>
public sealed class FlyingSwap : MagicSwordCard
{
    public FlyingSwap() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(6, 3);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonCardRules.ChooseAndSwitch(Owner, choiceContext);
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}

/// <summary>막으며 바꾸기 — Skill, cost 1: switch to one of your swords, then gain 6 (9) Block.</summary>
public sealed class GuardSwap : MagicSwordCard
{
    public GuardSwap() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(6, 3);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonCardRules.ChooseAndSwitch(Owner, choiceContext);
        await CommonActions.CardBlock(this, cardPlay);
    }
}

/// <summary>바꿔 쥐기 — Skill, cost 0: switch to one of your swords. Draw 1. Exhaust (upgrade: no Exhaust).</summary>
public sealed class FlipGrip : MagicSwordCard
{
    public FlipGrip() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithCards(1);
        WithKeyword(CardKeyword.Exhaust, UpgradeType.Remove);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonCardRules.ChooseAndSwitch(Owner, choiceContext);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}

/// <summary>연격 — Attack, cost 1: deal 6 (9). If the current sword changed this turn, draw 1.</summary>
public sealed class LinkedStrike : MagicSwordCard
{
    public LinkedStrike() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(6, 3);
        WithCards(1);
    }

    protected override bool ShouldGlowGoldInternal => CommonCardRules.SwitchedThisTurn(CommonCardRules.CombatOwner(this));

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (CommonCardRules.SwitchedThisTurn(Owner))
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}

/// <summary>찌르기 — Attack, cost 0: deal 3 (5). If the current sword changed this turn, hit 1 more time.</summary>
public sealed class QuickThrust : MagicSwordCard
{
    public QuickThrust() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(3, 2);
    }

    protected override bool ShouldGlowGoldInternal => CommonCardRules.SwitchedThisTurn(CommonCardRules.CombatOwner(this));

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hits = CommonCardRules.SwitchedThisTurn(Owner) ? 2 : 1;
        await CommonActions.CardAttack(this, cardPlay, hits)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}

/// <summary>받아넘기기 — Skill, cost 1: gain 6 (9) Block, +3 (+4) if the current sword changed this turn.</summary>
public sealed class Deflection : MagicSwordCard
{
    public Deflection() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        // CalculationBase 6 (+3), CalculationExtra 3 (+1) x (switched this turn ? 1 : 0)
        WithCalculatedBlock(6, 3, static (card, _) => CommonCardRules.SwitchedThisTurnOf(card), upgrade: 3,
            bonusUpgrade: 1);
    }

    protected override bool ShouldGlowGoldInternal => CommonCardRules.SwitchedThisTurn(CommonCardRules.CombatOwner(this));

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);
    }
}

/// <summary>검막 — Skill, cost 1: gain 4 (6) Block + 2 (3) per sword out.</summary>
public sealed class SwordScreen : MagicSwordCard
{
    public SwordScreen() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithCalculatedBlock(4, 2, static (card, _) => CommonCardRules.PresentCountOf(card), upgrade: 2,
            bonusUpgrade: 1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);
    }
}

/// <summary>떠도는 칼날 — Attack, cost 1: deal 5 (7) + 2 (3) per sword out.</summary>
public sealed class HoveringBlades : MagicSwordCard
{
    public HoveringBlades() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithCalculatedDamage(5, 2, static (card, _) => CommonCardRules.PresentCountOf(card), upgrade: 2,
            bonusUpgrade: 1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}

/// <summary>연속 베기 — Attack, cost 1: deal 5 (7) damage twice (TwinStrike-like multi-hit carrier).</summary>
public sealed class DoubleSlash : MagicSwordCard
{
    private const int Hits = 2;

    public DoubleSlash() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(5, 2);
        WithVar("Hits", Hits);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay, Hits)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}

/// <summary>세 번 찌르기 — Attack, cost 1: deal 3 (4) damage 3 times.</summary>
public sealed class TripleThrust : MagicSwordCard
{
    private const int Hits = 3;

    public TripleThrust() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(3, 1);
        WithVar("Hits", Hits);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay, Hits)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}

/// <summary>원을 그리는 칼 — Attack, cost 1: deal 6 (9) damage to ALL enemies.</summary>
public sealed class ArcSlash : MagicSwordCard
{
    public ArcSlash() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
        WithDamage(6, 3);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}

/// <summary>칼자루 치기 — Attack, cost 1: deal 7 (9). Apply 1 (2) Weak (pattern: game card SuckerPunch).</summary>
public sealed class PommelBash : MagicSwordCard
{
    public PommelBash() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(7, 2);
        WithPower<WeakPower>(1, 1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);
        await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, DynamicVars.Power<WeakPower>().BaseValue,
            Owner.Creature, this);
    }
}

/// <summary>칼끝 읽기 — Skill, cost 1: gain 5 (8) Block. Draw 2 (game card Backflip).</summary>
public sealed class ReadTheEdge : MagicSwordCard
{
    public ReadTheEdge() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(5, 3);
        WithCards(2);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}

/// <summary>
/// 소환의 자세 — Skill, cost 1: gain 6 (9) Block. Choose a card of an owned sword that has not come out this combat
/// from your draw pile and put it into your hand.
/// </summary>
public sealed class SummoningStance : MagicSwordCard
{
    public SummoningStance() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(6, 3);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);
        var player = Owner;
        await CommonCardRules.PickFromPileToHand(player, PileType.Draw, choiceContext, SelectionScreenPrompt,
            c => CommonCardRules.IsCardOfNotYetSummonedSword(player, c));
    }
}

/// <summary>내리찍기 — Attack, cost 2: deal 12 (16). Apply 1 (2) Vulnerable.</summary>
public sealed class HeavyChop : MagicSwordCard
{
    public HeavyChop() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(12, 4);
        WithPower<VulnerablePower>(1, 1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target,
            DynamicVars.Power<VulnerablePower>().BaseValue, Owner.Creature, this);
    }
}
