using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

/// <summary>
/// 핀디아스의 새벽 (Common): draw 2 cards. At the start of your next turn, gain 2 Light (<see cref="SolaisDawnPower"/>).
/// Cost 0 from level 5 (balance 2026-10-09, was level 3).
/// Reworked 2026-10-16 (user request: diversify how Light is GAINED): it was "draw 2, +1 if you have Light" (only
/// read Light). Now it is a delayed source — the light arrives with the next dawn, after the end-of-turn cost was
/// already settled (so it also refills the sheath after a turn spent holding another sword).
/// Source: 「네 가지 보물 중 하나로, 북방 도시 핀디아스에서 가져왔다」.
/// </summary>
public sealed class SolaisFromFindias : SolaisCard
{
    public SolaisFromFindias() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithCards(2);
        WithPower<SolaisDawnPower>(2);
        WithTip(typeof(SolaisLightPower));
    }

    protected override int? CostAtLevel(int level) => level >= 5 ? 0 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        await PowerCmd.Apply<SolaisDawnPower>(choiceContext, Owner.Creature,
            DynamicVars["SolaisDawnPower"].BaseValue, Owner.Creature, this);
    }
}
