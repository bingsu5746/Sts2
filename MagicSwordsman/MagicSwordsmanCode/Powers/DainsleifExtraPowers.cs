using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 난쟁이의 담금질 (DainsleifDwarfForged): whenever Dainsleif returns one of the owner's Attack cards to the draw pile
/// (only Dainsleif sends a played card to the Draw result pile), that card instance deals Amount more damage per hit
/// for the rest of this combat. Does not consume the 빗나가지 않는 칼 return mark.
/// </summary>
public sealed class DainsleifDwarfForgedPower : MagicSwordsmanPower
{
    // Per-instance state lives in internal data (a field initializer would be shared with the canonical copy).
    private sealed class Data
    {
        public readonly Dictionary<CardModel, int> Bonus = new();
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new Data();

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        if (card.Owner?.Creature != Owner || card.Type != CardType.Attack || cardPlay.ResultPile != PileType.Draw)
            return Task.CompletedTask;
        var bonus = GetInternalData<Data>().Bonus;
        bonus[card] = bonus.GetValueOrDefault(card) + Amount;
        Flash();
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        if (cardSource == null || !props.IsPoweredAttack()) return 0m;
        return GetInternalData<Data>().Bonus.GetValueOrDefault(cardSource);
    }
}
