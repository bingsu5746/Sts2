using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Kusanagi;

/// <summary>
/// 거울 (Uncommon): cost 1 (0 from level 3). If Kusanagi is inheriting a sword's effect, choose a card of THAT sword
/// from your draw pile, put it into your hand, it costs 1 less this turn. Otherwise draw 2 cards.
/// </summary>
public sealed class KusanagiMirror : KusanagiCard
{
    public KusanagiMirror() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithCards(2);
    }

    protected override int? CostAtLevel(int level) => level >= 3 ? 0 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        SwordId? inherited = null;
        if (SwordCombat.CurrentSword(Owner) == SwordId.Kusanagi)
            inherited = SwordRegistry.Get(SwordId.Kusanagi).GetInheritedSword(SwordCombat.ContextFor(Owner, SwordId.Kusanagi));

        if (inherited is { } sword)
        {
            var options = PileType.Draw.GetPile(Owner).Cards
                .Where(c => c is MagicSwordCard { Sword: { } s } && s == sword)
                .ToList();
            if (options.Count > 0)
            {
                var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, options, Owner,
                    new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();
                if (chosen != null)
                {
                    await CardPileCmd.Add(chosen, PileType.Hand);
                    chosen.EnergyCost.AddThisTurn(-1, reduceOnly: true);
                    return;
                }
            }
        }

        // Not inheriting (or no card of the inherited sword left in the draw pile): draw 2.
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}
