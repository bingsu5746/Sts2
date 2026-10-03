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
/// 햐드닝아비그 (Rare power): cost 2 (1 from level 3). Of the cards Dainsleif returns, the first 2 each turn go on
/// TOP of the draw pile (not 0-cost cards). The first Dainsleif card you play each turn costs 0.
/// </summary>
public sealed class DainsleifHjadningavig : DainsleifCard
{
    public DainsleifHjadningavig() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithVar("TopCards", DainsleifHjadningavigPower.TopCardsPerTurn);
        WithTip(new TooltipSource(_ => MegaCrit.Sts2.Core.HoverTips.HoverTipFactory.FromPower<DainsleifHjadningavigPower>()));
    }

    protected override int? CostAtLevel(int level) => level >= 3 ? 1 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<DainsleifHjadningavigPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }
}
