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

/// <summary>목숨값 (Basic): 5 Block (+1/L). Your next Tyrfing attack this turn deals +3 (+1/L) damage.</summary>
public sealed class TyrfingDwarvenRansom : TyrfingCard
{
    public TyrfingDwarvenRansom() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
        WithBlock(5);
        WithBlockPerLevel(1);
        WithLevelVar("Bonus", 3, 1);
        WithTip(new TooltipSource(_ => MegaCrit.Sts2.Core.HoverTips.HoverTipFactory.FromPower<TyrfingRansomPower>()));
    }

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<TyrfingRansomPower>(choiceContext, Owner.Creature, LevelVar("Bonus"), Owner.Creature, this);
    }
}
