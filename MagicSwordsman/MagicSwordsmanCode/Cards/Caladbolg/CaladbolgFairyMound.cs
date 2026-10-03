using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Caladbolg;

/// <summary>요정 언덕 (Basic): 5 Block (+1/L); +3 if there are 2+ enemies. Source: 「'요정 언덕에서 온 레테의 검'」.</summary>
public sealed class CaladbolgFairyMound : CaladbolgCard
{
    public CaladbolgFairyMound() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
        WithBlock(5);
        WithBlockPerLevel(1);
        WithVar("ExtraBlock", 3);
    }

    protected override decimal BonusBlock() => EnemyCount >= 2 ? DynamicVars["ExtraBlock"].BaseValue : 0m;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }
}
