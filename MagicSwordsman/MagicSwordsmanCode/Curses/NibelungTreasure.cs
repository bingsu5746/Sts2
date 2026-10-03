using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Curses;

/// <summary>
/// 니벨룽의 보물 — Gram's level-5 curse [확정] (content doc §3.2). Cost 1, playable, Eternal (cannot be removed from
/// the deck), Exhaust (gone for this combat, stays in the deck). Play: gain 8 Gold (PlayerCmd.GainGold).
/// Given by GramBehavior.OnLevelChanged the first time Gram reaches level 5 in a run.
/// Lore (research doc): the Nibelung treasure Gram won destroyed two royal houses.
/// </summary>
public sealed class NibelungTreasure : SwordCurseCard
{
    public override SwordId Sword => SwordId.Gram;

    public NibelungTreasure() : base(playable: true, cost: 1)
    {
        WithVar("Gold", 8);
        WithKeywords(CardKeyword.Eternal, CardKeyword.Exhaust);
    }

    protected override async Task OnCursePlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PlayerCmd.GainGold(DynamicVars["Gold"].IntValue, Owner);
    }
}
