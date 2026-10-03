using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Curses;

/// <summary>
/// 롤랑의 바위 — Durandal's forge-failure curse (content doc §3.1). Unplayable.
/// While in your hand, enemy attack hits deal +1 damage to you. After you finish 10 combats with this card in your
/// deck it removes itself (game curse Guilty pattern: [SavedProperty] counter + AfterCombatEnd + RemoveFromDeck).
/// Source line: 「바위를 열 번 내리쳐도 흠집조차 나지 않았다」.
/// </summary>
public sealed class RolandsRock : SwordCurseCard
{
    public const int ExtraDamage = 1;
    public const int CombatsToVanish = 10;

    private int _combatsSeen;

    public override SwordId Sword => SwordId.Durandal;

    public RolandsRock()
    {
        WithVar("ExtraDamage", ExtraDamage);
        WithVar("Combats", CombatsToVanish);
    }

    /// <summary>Combats finished with this card in the deck (saved with the card, like Guilty.CombatsSeen).</summary>
    [SavedProperty]
    public int CombatsSeen
    {
        get => _combatsSeen;
        set
        {
            AssertMutable();
            _combatsSeen = value;
            DynamicVars["Combats"].BaseValue = Math.Max(0, CombatsToVanish - value);
        }
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        if (!IsMutable || Owner == null || Pile?.Type != PileType.Hand) return 0m;
        return DurandalBehavior.IsEnemyAttackOn(Owner.Creature, target, props, dealer) ? ExtraDamage : 0m;
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        if (Pile?.Type != PileType.Deck) return;
        CombatsSeen++;
        if (CombatsSeen >= CombatsToVanish && Pile?.Type == PileType.Deck)
            await CardPileCmd.RemoveFromDeck(this);
    }
}
