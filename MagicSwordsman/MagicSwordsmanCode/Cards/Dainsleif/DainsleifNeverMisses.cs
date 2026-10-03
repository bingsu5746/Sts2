using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Dainsleif;

/// <summary>
/// 빗나가지 않는 칼 (Rare power): whenever Dainsleif returns a card to the draw pile, deal 3 (+⌊L/2⌋) damage to a
/// random enemy. Capped by the 12-per-turn return limit.
/// </summary>
public sealed class DainsleifNeverMisses : DainsleifCard
{
    public DainsleifNeverMisses() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithCalculatedVar("Hit", 3, (card, _) => card is MagicSwordCard m ? m.SwordLevel / 2 : 0);
        WithTip(new TooltipSource(_ => MegaCrit.Sts2.Core.HoverTips.HoverTipFactory.FromPower<DainsleifNeverMissesPower>()));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var amount = 3 + SwordLevel / 2;
        await PowerCmd.Apply<DainsleifNeverMissesPower>(choiceContext, Owner.Creature, amount, Owner.Creature, this);
    }
}
