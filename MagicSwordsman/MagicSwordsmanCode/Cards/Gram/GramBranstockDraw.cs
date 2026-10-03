using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Gram;

/// <summary>
/// 브란스톡에서 뽑다 — Gram, Skill, Common, cost 0, self. Draw 1. Vigor 2 (+⌊L/2⌋).
/// Lore (research doc): Odin thrust Gram into the tree Branstock in the Völsung hall; only Sigmund could draw it.
/// </summary>
public sealed class GramBranstockDraw : GramCard
{
    public GramBranstockDraw() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithCards(1);
        WithHalfLevelVar("Vigor", 2);
        WithTip(typeof(VigorPower));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, HalfLevelVar("Vigor"), Owner.Creature, this);
    }
}
