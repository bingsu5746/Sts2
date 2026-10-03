using MagicSwordsman.MagicSwordsmanCode.Cards;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 햐드닝아비그 (DainsleifHjadningavig): of the cards Dainsleif returns to the draw pile, the first 2 each turn go on
/// TOP instead of a random position (not cards that cost 0 — handled in <see cref="DainsleifReturn.Decide"/>), and
/// the first Dainsleif card you play each turn costs 0. Single (a second copy adds nothing, like Barricade).
/// </summary>
public sealed class DainsleifHjadningavigPower : MagicSwordsmanPower
{
    public const int TopCardsPerTurn = 2;

    private int _freeUsedTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    private int TurnNumber => Owner.Player?.PlayerCombatState?.TurnNumber ?? 0;

    private bool IsOwnDainsleifCard(CardModel card) =>
        card is MagicSwordCard { Sword: SwordId.Dainsleif } && card.Owner?.Creature == Owner;

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!IsOwnDainsleifCard(card) || _freeUsedTurn == TurnNumber || originalCost <= 0m) return false;
        modifiedCost = 0m;
        return true;
    }

    // BeforeCardPlayed runs after the energy was paid (CardModel.OnPlayWrapper), so the played card was still free.
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (IsOwnDainsleifCard(cardPlay.Card)) _freeUsedTurn = TurnNumber;
        return Task.CompletedTask;
    }
}

/// <summary>
/// 빗나가지 않는 칼 (DainsleifNeverMisses): whenever Dainsleif returns a card to the draw pile, deal Amount damage to
/// a random enemy (JuggernautPower pattern: Unpowered, Rng.CombatTargets).
/// </summary>
public sealed class DainsleifNeverMissesPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature != Owner || cardPlay.ResultPile != PileType.Draw) return;
        if (!DainsleifReturn.ConsumeReturned(cardPlay.Card)) return;
        var player = Owner.Player;
        var enemies = CombatState?.HittableEnemies;
        if (player == null || enemies == null || enemies.Count == 0) return;
        var target = player.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target == null) return;
        Flash();
        await CreatureCmd.Damage(choiceContext, target, Amount, ValueProp.Unpowered, Owner, null);
    }
}
