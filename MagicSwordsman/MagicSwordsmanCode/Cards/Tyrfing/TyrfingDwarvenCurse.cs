using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Tyrfing;

/// <summary>
/// 난쟁이의 저주 (Rare power): this combat, whenever a curse card is exhausted gain 1 Strength (from level 3 also
/// 4 Block). When played you may choose a curse in your hand and exhaust it.
/// </summary>
public sealed class TyrfingDwarvenCurse : TyrfingCard
{
    public TyrfingDwarvenCurse() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<TyrfingDwarvenCursePower>(1);
    }

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<TyrfingDwarvenCursePower>(choiceContext, Owner.Creature,
            DynamicVars["TyrfingDwarvenCursePower"].BaseValue, Owner.Creature, this);

        if (!PileType.Hand.GetPile(Owner).Cards.Any(c => c.Type == CardType.Curse)) return;
        var chosen = (await CardSelectCmd.FromHand(choiceContext, Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, 1), c => c.Type == CardType.Curse, this))
            .FirstOrDefault();
        if (chosen != null) await CardCmd.Exhaust(choiceContext, chosen);
    }
}
