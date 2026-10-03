using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

/// <summary>
/// 삼백 아이의 풀무 — Moye, Skill, Common, cost 0, self. Gain 3 (+1/L) Block. 【짝】 draw 1.
/// Lore: 300 children worked the bellows.
/// </summary>
public sealed class MoyeBellows : GanjiangMoyeCard
{
    public MoyeBellows() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(3);
        WithBlockPerLevel(1);
        WithCards(1);
    }

    protected override SwordId Side => SwordId.Moye;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        await CommonActions.CardBlock(this, cardPlay);
        if (pair) await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}
