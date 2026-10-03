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

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

/// <summary>
/// 핀디아스에서 온 검 (Common): draw 2 cards; if you have Light, draw 1 more. Cost 0 from level 3.
/// Source: 「네 가지 보물 중 하나로, 북방 도시 핀디아스에서 가져왔다」.
/// </summary>
public sealed class SolaisFromFindias : SolaisCard
{
    public SolaisFromFindias() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithCards(2);
        WithVar("ExtraCards", 1);
        WithTip(typeof(SolaisLightPower));
    }

    protected override int? CostAtLevel(int level) => level >= 3 ? 0 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var count = DynamicVars.Cards.IntValue;
        if (Light > 0) count += DynamicVars["ExtraCards"].IntValue;
        await CardPileCmd.Draw(choiceContext, count, Owner);
    }
}
