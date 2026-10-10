using MagicSwordsman.MagicSwordsmanCode.Cards;
using MagicSwordsman.MagicSwordsmanCode.Cards.Common;
using MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;
using MagicSwordsman.MagicSwordsmanCode.Cards.Union;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MagicSwordsman.MagicSwordsmanCode.Dialogue;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Potions;

// Second batch of 마검사 potions (user request 2026-10-09 "포션 좋다 유물이나"): items tied to the swords. No potion reads
// or changes 친밀도 (affinity drives dialogue only; 2026-10-10). All combat-only and self-targeted, like the first batch (CommonPotions.cs).

/// <summary>
/// 숫돌 기름 (Common): this combat, the current sword (none: choose an owned sword) counts as 1 level higher (max 5);
/// draw 1 card. Half of 담금질 물 (+2, Uncommon) plus a card. Base comparison: FlexPotion / StrengthPotion (Common).
/// </summary>
public sealed class WhetstoneOil : MagicSwordsmanPotion
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Levels", 1m), new CardsVar(1)];

    public override bool PassesCustomUsabilityCheck =>
        CombatManager.Instance.IsInProgress && Owner?.GetRelic<Mangeomchong>() is { OwnedSwords.Count: > 0 };

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        await CommonCardRules.TemperSword(Owner, DynamicVars["Levels"].IntValue, choiceContext);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}

/// <summary>
/// 검부름 향 (Common): summon a RANDOM owned sword that has not come out this combat and make it current, then draw 2.
/// Unlike 부름의 물약 there is no choice and the cards are plain draws. Random pick: Rng.CombatCardSelection (game
/// Anointed uses the same stream for "random from a list" in combat). Base comparison: SwiftPotion (Common, draw 3):
/// one card fewer, plus a summon (which can trigger 만검총's first-summon bonus).
/// </summary>
public sealed class RallyingIncense : MagicSwordsmanPotion
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    public override bool PassesCustomUsabilityCheck =>
        CombatManager.Instance.IsInProgress && Owner != null && CommonCardRules.NotYetOutCandidates(Owner).Count > 0;

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        var player = Owner;
        var candidates = CommonCardRules.NotYetOutCandidates(player).OrderBy(s => (int)s).ToList();
        if (candidates.Count > 0)
        {
            var sword = candidates[player.RunState.Rng.CombatCardSelection.NextInt(candidates.Count)];
            await SwordCombat.SwitchTo(player, sword, choiceContext, SwitchReason.SwapEffect);
        }

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, player);
    }
}

/// <summary>
/// 검총의 메아리 (Uncommon): choose 1 of 3 random sword cards (Attack/Skill, Common..Rare, no 【조합】) of swords you do
/// NOT own; it costs 0 this turn. Pattern and numbers: game AttackPotion (Common: 1 of 3 attacks, free this turn) —
/// one rarity higher because sword cards are stronger than an average character card.
/// A card of an unowned sword resolves its own effect but never summons / switches (SwordCombat.SwitchTo needs an
/// owned sword) and reads level 0. Onimaru is excluded: its cards command the floating Onimaru, which is not there.
/// Note: OwnedSwordCardFilterPatches drops unowned sword cards from GetDistinctForCombat, but keeps the original list
/// when nothing would remain, which is always the case here (every candidate belongs to an unowned sword).
/// </summary>
public sealed class TombEcho : MagicSwordsmanPotion
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    private const int Choices = 3;

    private List<CardModel> Candidates()
    {
        var relic = Owner.GetRelic<Mangeomchong>();
        if (relic == null) return [];
        return Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c is MagicSwordCard { Sword: { } s } && c is not UnionCard &&
                        s != SwordId.Onimaru &&
                        !SwordRegistry.WithPartners(s).Any(relic.Owns))
            .Where(c => c.Type is CardType.Attack or CardType.Skill)
            .Where(c => c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .ToList();
    }

    public override bool PassesCustomUsabilityCheck =>
        CombatManager.Instance.IsInProgress && Owner != null && Candidates().Count > 0;

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        var pool = Candidates();
        if (pool.Count == 0) return;
        var cards = CardFactory.GetDistinctForCombat(Owner, pool, Choices, Owner.RunState.Rng.CombatCardGeneration)
            .ToList();
        var card = await CardSelectCmd.FromChooseACardScreen(choiceContext, cards, Owner, canSkip: true);
        if (card == null) return;
        card.SetToFreeThisTurn();
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
    }
}

/// <summary>
/// 봉인 소금 (Uncommon): exhaust every sword curse (<see cref="SwordCurseCard"/>: forge failure curses, 티르빙's deeds,
/// 니벨룽의 보물…) in your hand, draw pile and discard pile — for this combat only, the deck keeps them — and gain 4
/// Block for each. Base comparison: Ashwater (Uncommon: exhaust any cards from hand) — narrower target, but reaches
/// every combat pile and pays Block (Unpowered, like the game's relic Block).
/// </summary>
public sealed class SealingSalt : MagicSwordsmanPotion
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(4m, ValueProp.Unpowered)];

    public override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromKeyword(CardKeyword.Exhaust)];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        var player = Owner;
        var curses = PileType.Hand.GetPile(player).Cards
            .Concat(PileType.Draw.GetPile(player).Cards)
            .Concat(PileType.Discard.GetPile(player).Cards)
            .Where(c => c is SwordCurseCard)
            .ToList();
        foreach (var card in curses) await CardCmd.Exhaust(choiceContext, card);
        for (var i = 0; i < curses.Count; i++)
            await CreatureCmd.GainBlock(player.Creature, DynamicVars.Block, null);
    }
}

/// <summary>
/// 합환주 (Rare): this turn every Ganjiang/Moye card triggers 【짝】 (same rule as the card 암수 한 쌍:
/// <see cref="PairRules.MakeAlwaysThisTurn"/>), and your next card this turn is played twice (the game's
/// DuplicationPower, removed at the end of the turn). Base comparison: Duplicator (Uncommon: DuplicationPower 1) —
/// one rarity higher for the 【짝】 part.
/// </summary>
public sealed class TwinCupWine : MagicSwordsmanPotion
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromKeyword(MagicSwordsmanKeywords.Pair)];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        PairRules.MakeAlwaysThisTurn(Owner);
        await PowerCmd.Apply<DuplicationPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, null);
    }
}
