using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Common;

// Common (non-sword) card pool — Rare rarity (content doc §4.4, 9 cards). Sword == null: upgradable at the Smith.

/// <summary>마검 공명 — Power, cost 1: whenever your current sword changes, draw 1 card (max 2 (3) times per turn).</summary>
public sealed class SwordResonance : MagicSwordCard
{
    public SwordResonance() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<SwordResonancePower>(2, 1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SwordResonancePower>(choiceContext, Owner.Creature,
            DynamicVars[nameof(SwordResonancePower)].BaseValue, Owner.Creature, this);
    }
}

/// <summary>검성 — Power, cost 2 (1) (balance 2026-10-07, was 3 (2)): the first time your current sword changes each turn, gain 1 energy.</summary>
public sealed class SwordSaint : MagicSwordCard
{
    public SwordSaint() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithEnergy(1);
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SwordSaintPower>(choiceContext, Owner.Creature, DynamicVars.Energy.BaseValue,
            Owner.Creature, this);
    }
}

/// <summary>
/// 만검귀종 — Skill, cost 0: gain 1 energy per sword out (max 3). Exhaust. Upgrade: also draw 2 cards.
/// </summary>
public sealed class ThousandSwordsReturn : MagicSwordCard
{
    public const int MaxEnergy = 3;
    private const string SwordEnergyKey = "SwordEnergy";

    public ThousandSwordsReturn() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithEnergy(1);
        WithCards(0, 2);
        WithCalculatedVar(SwordEnergyKey, 0,
            static (card, _) => Math.Min(MaxEnergy, CommonCardRules.PresentCountOf(card)));
        WithKeywords(CardKeyword.Exhaust);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var swords = Math.Min(MaxEnergy, CommonCardRules.PresentCount(Owner));
        var energy = DynamicVars.Energy.BaseValue * swords;
        if (energy > 0) await PlayerCmd.GainEnergy(energy, Owner);
        if (DynamicVars.Cards.IntValue > 0)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}

/// <summary>
/// 검총의 네 자루 — Power, cost 2 (1): when you summon your 2nd / 3rd / 4th sword this combat, gain 1 energy /
/// draw 2 cards / gain 2 Strength.
/// </summary>
public sealed class FourSwordsOfTheTomb : MagicSwordCard
{
    public FourSwordsOfTheTomb() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithVar("EnergyAt2", FourSwordsOfTheTombPower.EnergyAt2);
        WithVar("CardsAt3", FourSwordsOfTheTombPower.CardsAt3);
        WithVar("StrengthAt4", FourSwordsOfTheTombPower.StrengthAt4);
        WithTip(typeof(StrengthPower));
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FourSwordsOfTheTombPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);

        // Balance 2026-10-07: milestones already passed this combat pay out now (once), so playing it late is not a loss.
        var summoned = SwordCombat.Get(Owner)?.Summoned.Count ?? 0;
        if (summoned >= 2) await PlayerCmd.GainEnergy(FourSwordsOfTheTombPower.EnergyAt2, Owner);
        if (summoned >= 3) await CardPileCmd.Draw(choiceContext, FourSwordsOfTheTombPower.CardsAt3, Owner);
        if (summoned >= 4)
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, FourSwordsOfTheTombPower.StrengthAt4,
                Owner.Creature, this);
    }
}

/// <summary>만검의 벽 — Skill, cost 2: gain 10 (14) Block + 4 (5) per sword out.</summary>
public sealed class WallOfSwords : MagicSwordCard
{
    public WallOfSwords() : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithCalculatedBlock(10, 4, static (card, _) => CommonCardRules.PresentCountOf(card), upgrade: 4,
            bonusUpgrade: 1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);
    }
}

/// <summary>
/// 검진 — Power, cost 2: at the end of your turn, gain 3 (4) Block for each sword out other than the current one.
/// </summary>
public sealed class SwordFormation : MagicSwordCard
{
    public SwordFormation() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<SwordFormationPower>(3, 1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SwordFormationPower>(choiceContext, Owner.Creature,
            DynamicVars[nameof(SwordFormationPower)].BaseValue, Owner.Creature, this);
    }
}

/// <summary>검의 메아리 — Skill, cost 1 (0): this turn, the next sword card you play is played one extra time.</summary>
public sealed class SwordEcho : MagicSwordCard
{
    public SwordEcho() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SwordEchoPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }
}

/// <summary>
/// 일섬 — Attack, cost 1: Retain. Deal 10 (14) damage. If this is the first card you play this turn, the damage is
/// doubled. "First card" check: game LethalityPower (CardPlaysStarted this turn, ignoring this play and replays).
/// </summary>
public sealed class Issen : MagicSwordCard
{
    public Issen() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(10, 4);
        WithKeywords(CardKeyword.Retain);
    }

    private bool IsFirstCardThisTurn()
    {
        if (!IsMutable || CombatState == null || Owner?.PlayerCombatState == null) return false;
        var pile = Pile;
        var inPlay = pile != null && pile.Type == PileType.Play;
        if (inPlay && CurrentPlayIndex > 0) return false;
        var owner = Owner;
        var started = CombatManager.Instance.History.CardPlaysStarted.Count(e =>
            e.HappenedThisTurn(CombatState) && e.CardPlay.Card.Owner == owner);
        return started <= (inPlay ? 1 : 0);
    }

    protected override bool ShouldGlowGoldInternal => IsFirstCardThisTurn();

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (!ReferenceEquals(cardSource, this) || !props.IsPoweredAttack()) return 1m;
        return IsFirstCardThisTurn() ? 2m : 1m;
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}

/// <summary>무검의 경지 — Power, cost 2 (1): your attack cards that are not sword cards deal 3 more damage.</summary>
public sealed class Swordless : MagicSwordCard
{
    public Swordless() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<SwordlessPower>(3);
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SwordlessPower>(choiceContext, Owner.Creature,
            DynamicVars[nameof(SwordlessPower)].BaseValue, Owner.Creature, this);
    }
}
