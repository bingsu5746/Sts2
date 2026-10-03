using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

/// <summary>막야 막기 — Moye, Skill, Basic (starter), cost 1, self. Gain 5 (+1/L) Block. 【짝】 draw 1.</summary>
public sealed class MoyeGuard : GanjiangMoyeCard
{
    public MoyeGuard() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
        WithBlock(5);
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
