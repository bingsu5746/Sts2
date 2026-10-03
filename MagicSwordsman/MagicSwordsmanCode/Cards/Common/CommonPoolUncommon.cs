using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Common;

// Common (non-sword) card pool — Uncommon rarity (content doc §4.3, 14 cards). Sword == null: upgradable at the Smith.

/// <summary>
/// 두 번 바꾸기 — Skill, cost 1 (0): switch swords twice. Then choose a card of the sword you switched to last from
/// your draw pile and put it into your hand.
/// </summary>
public sealed class DoubleSwap : MagicSwordCard
{
    public DoubleSwap() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner;
        var first = await CommonCardRules.ChooseAndSwitch(player, choiceContext);
        var second = await CommonCardRules.ChooseAndSwitch(player, choiceContext);
        if ((second ?? first) is not { } sword) return;
        await CommonCardRules.PickFromPileToHand(player, PileType.Draw, choiceContext, SelectionScreenPrompt,
            c => CommonCardRules.IsCardOfSword(c, sword));
    }
}

/// <summary>
/// 검 부르기 — Skill, cost 1: choose an owned sword that has not come out this combat and summon it (it becomes the
/// current sword). Gain 5 (8) Block.
/// </summary>
public sealed class CallSword : MagicSwordCard
{
    public CallSword() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(5, 3);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonCardRules.ChooseAndSummon(Owner, choiceContext);
        await CommonActions.CardBlock(this, cardPlay);
    }
}

/// <summary>검무 — Power, cost 1: whenever your current sword changes, gain 2 (3) Block.</summary>
public sealed class SwordDance : MagicSwordCard
{
    public SwordDance() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<SwordDancePower>(2, 1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SwordDancePower>(choiceContext, Owner.Creature,
            DynamicVars[nameof(SwordDancePower)].BaseValue, Owner.Creature, this);
    }
}

/// <summary>칼바람 — Power, cost 1: whenever your current sword changes, deal 3 (4) damage to a random enemy.</summary>
public sealed class BladeGale : MagicSwordCard
{
    public BladeGale() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<BladeGalePower>(3, 1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<BladeGalePower>(choiceContext, Owner.Creature,
            DynamicVars[nameof(BladeGalePower)].BaseValue, Owner.Creature, this);
    }
}

/// <summary>잔상 베기 — Attack, cost 1: deal 5 (7) + 3 (4) per current-sword change this turn.</summary>
public sealed class AfterimageCut : MagicSwordCard
{
    public AfterimageCut() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithCalculatedDamage(5, 3, static (card, _) => CommonCardRules.SwitchesThisTurnOf(card), upgrade: 2,
            bonusUpgrade: 1);
    }

    protected override bool ShouldGlowGoldInternal => CommonCardRules.SwitchedThisTurn(CommonCardRules.CombatOwner(this));

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}

/// <summary>
/// 검총의 메아리 — Attack, cost 2: deal 6 (8) damage once per sword out (minimum 1). Hit count: game Finisher pattern
/// (calculated var read at play time). Out of combat the text shows 1 hit.
/// </summary>
public sealed class EchoOfTheTomb : MagicSwordCard
{
    private const string HitsKey = "Hits";

    public EchoOfTheTomb() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(6, 2);
        // Hits = 1 + (max(1, swords out) - 1) = max(1, swords out)
        WithCalculatedVar(HitsKey, 1,
            static (card, _) => Math.Max(1m, CommonCardRules.PresentCountOf(card)) - 1m);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hits = Math.Max(1, (int)((CalculatedVar)DynamicVars[HitsKey]).Calculate(cardPlay.Target));
        await CommonActions.CardAttack(this, cardPlay, hits)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}

/// <summary>검의 기억 — Skill, cost 1 (0): choose a sword card in your discard pile and put it into your hand.</summary>
public sealed class SwordMemory : MagicSwordCard
{
    public SwordMemory() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonCardRules.PickFromPileToHand(Owner, PileType.Discard, choiceContext, SelectionScreenPrompt,
            static c => c is MagicSwordCard { IsSwordCard: true });
    }
}

/// <summary>
/// 반격 자세 — Skill, cost 1: gain 7 (10) Block. Until your next turn, whenever you are attacked deal 3 (5) damage
/// back (the game's own FlameBarrierPower, as in game card FlameBarrier).
/// </summary>
public sealed class CounterStance : MagicSwordCard
{
    public CounterStance() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(7, 3);
        WithPower<FlameBarrierPower>(3, 2);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);
        await PowerCmd.Apply<FlameBarrierPower>(choiceContext, Owner.Creature,
            DynamicVars[nameof(FlameBarrierPower)].BaseValue, Owner.Creature, this);
    }
}

/// <summary>흘려 베기 — Attack, cost 1: deal 7 (10) damage. Gain 6 (8) Block.</summary>
public sealed class ParryCut : MagicSwordCard
{
    public ParryCut() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(7, 3);
        WithBlock(6, 2);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await CommonActions.CardBlock(this, cardPlay);
    }
}

/// <summary>
/// 기 모으기 — Skill, cost 1: next turn gain 1 energy and draw 2 (3) more cards (game EnergyNextTurnPower and
/// DrawCardsNextTurnPower).
/// </summary>
public sealed class GatherQi : MagicSwordCard
{
    public GatherQi() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithEnergy(1);
        WithTip(typeof(EnergyNextTurnPower));
        WithPower<DrawCardsNextTurnPower>(2, 1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner.Creature;
        await PowerCmd.Apply<EnergyNextTurnPower>(choiceContext, creature, DynamicVars.Energy.BaseValue, creature,
            this);
        await PowerCmd.Apply<DrawCardsNextTurnPower>(choiceContext, creature,
            DynamicVars[nameof(DrawCardsNextTurnPower)].BaseValue, creature, this);
    }
}

/// <summary>
/// 한 검 깊이 — Skill, cost 1: draw 1 card. If your current sword does not change for the rest of this turn, gain
/// 8 (11) Block at the end of the turn (OneBladeFocusPower).
/// </summary>
public sealed class OneBladeFocus : MagicSwordCard
{
    public OneBladeFocus() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithCards(1);
        WithPower<OneBladeFocusPower>(8, 3);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        await PowerCmd.Apply<OneBladeFocusPower>(choiceContext, Owner.Creature,
            DynamicVars[nameof(OneBladeFocusPower)].BaseValue, Owner.Creature, this);
    }
}

/// <summary>검심 — Power, cost 2 (1): at the start of your turn, if you have a current sword, draw 1 more card.</summary>
public sealed class SwordHeart : MagicSwordCard
{
    public SwordHeart() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<SwordHeartPower>(1);
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SwordHeartPower>(choiceContext, Owner.Creature,
            DynamicVars[nameof(SwordHeartPower)].BaseValue, Owner.Creature, this);
    }
}

/// <summary>
/// 칼날 세우기 — Skill, cost 1 (0): this combat, your current sword counts as 1 level higher (max 5). Exhaust.
/// With no current sword, choose one of your swords (same fallback as the potion 담금질 물, content doc §8).
/// </summary>
public sealed class Hone : MagicSwordCard
{
    public Hone() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("Levels", 1);
        WithKeywords(CardKeyword.Exhaust);
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonCardRules.TemperSword(Owner, DynamicVars["Levels"].IntValue, choiceContext);
    }
}

/// <summary>칼끝 겨누기 — Skill, cost 0: gain 5 (8) Vigor (game VigorPower).</summary>
public sealed class TakeAim : MagicSwordCard
{
    public TakeAim() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<VigorPower>(5, 3);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, DynamicVars[nameof(VigorPower)].BaseValue,
            Owner.Creature, this);
    }
}
