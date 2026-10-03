using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Gram;

/// <summary>
/// 발뭉·노퉁 — Gram, Power, Rare, cost 2 (1 from Gram level 3), self.
/// This combat your attacks deal +(Gram level + 1) damage whatever the current sword is. While Gram is current,
/// this replaces Gram's own +L (no stacking) — see <see cref="BalmungNothungPower"/> and GramBehavior.
/// Lore: the same sword is called Balmung in the Nibelungenlied and Nothung in Wagner's opera.
/// </summary>
public sealed class GramBalmungNothung : GramCard
{
    public GramBalmungNothung() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithCalculatedVar("Bonus", 1, 1, static (card, _) => card is MagicSwordCard m ? m.SwordLevel : 0);
        WithTip(typeof(BalmungNothungPower));
    }

    public override int? CostAtLevel(int level) => level >= 3 ? 1 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<BalmungNothungPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }
}
