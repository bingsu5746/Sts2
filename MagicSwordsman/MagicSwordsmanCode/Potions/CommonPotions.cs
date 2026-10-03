using MagicSwordsman.MagicSwordsmanCode.Cards;
using MagicSwordsman.MagicSwordsmanCode.Cards.Common;
using MagicSwordsman.MagicSwordsmanCode.Cards.Kusanagi;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace MagicSwordsman.MagicSwordsmanCode.Potions;

// 마검사 potions, content doc §8. All are combat-only, self-targeted (pattern: game LiquidMemories / Ashwater:
// TargetType.Self, effects on Owner).

/// <summary>
/// 부름의 물약 (Common): choose an owned sword that has not come out this combat, summon it and make it current,
/// then put 2 random cards of that sword from your draw pile into your hand (random pick: game Anointed —
/// TakeRandom with Rng.CombatCardSelection).
/// </summary>
public sealed class SummoningDraught : MagicSwordsmanPotion
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    /// <summary>Only usable in combat while some owned sword has not come out yet (otherwise it would do nothing).</summary>
    public override bool PassesCustomUsabilityCheck =>
        CombatManager.Instance.IsInProgress && Owner != null && CommonCardRules.NotYetOutCandidates(Owner).Count > 0;

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        var player = Owner;
        if (await CommonCardRules.ChooseAndSummon(player, choiceContext) is not { } sword) return;
        var cards = PileType.Draw.GetPile(player).Cards
            .Where(c => CommonCardRules.IsCardOfSword(c, sword))
            .TakeRandom(DynamicVars.Cards.IntValue, player.RunState.Rng.CombatCardSelection)
            .ToList();
        if (cards.Count > 0) await CardPileCmd.Add(cards, PileType.Hand);
    }
}

/// <summary>
/// 정화수 (Common): exhaust every status and curse card in your hand, then remove one of your debuffs. Uses the same
/// 정화 rules as 쿠사나기 cards (<see cref="KusanagiPurify"/>: the visible debuff with the largest amount).
/// </summary>
public sealed class PurifyingWater : MagicSwordsmanPotion
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    public override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromKeyword(CardKeyword.Exhaust)];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        await KusanagiPurify.ExhaustAllFromHand(choiceContext, Owner);
        await KusanagiPurify.RemoveOneDebuff(Owner.Creature);
    }
}

/// <summary>
/// 담금질 물 (Uncommon): for the rest of this combat, your current sword counts as 2 levels higher (max 5). With no
/// current sword, choose one of your swords. Uses the framework's per-combat level bonus
/// (<see cref="SwordCombat.CombatLevelBonusKey"/>).
/// </summary>
public sealed class QuenchingWater : MagicSwordsmanPotion
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Levels", 2m)];

    public override bool PassesCustomUsabilityCheck
    {
        get
        {
            if (!CombatManager.Instance.IsInProgress || Owner == null) return false;
            var relic = Owner.GetRelic<Mangeomchong>();
            return relic != null && relic.OwnedSwords.Count > 0;
        }
    }

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        await CommonCardRules.TemperSword(Owner, DynamicVars["Levels"].IntValue, choiceContext);
    }
}

/// <summary>
/// 칼날 정수 (Rare): this turn, sword cards in your hand cost 1 less (min 0). Cost change: game Stomp
/// (EnergyCost.AddThisTurn(-1); the game clamps the cost at 0).
/// </summary>
public sealed class BladeEssence : MagicSwordsmanPotion
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("CostReduction", 1m)];

    protected override Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        var reduction = DynamicVars["CostReduction"].IntValue;
        foreach (var card in PileType.Hand.GetPile(Owner).Cards.ToList())
        {
            if (card is MagicSwordCard { IsSwordCard: true } && !card.EnergyCost.CostsX)
                card.EnergyCost.AddThisTurn(-reduction);
        }
        return Task.CompletedTask;
    }
}
