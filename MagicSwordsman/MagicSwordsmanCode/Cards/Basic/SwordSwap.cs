using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Basic;

/// <summary>
/// 검 바꾸기 — common card (spec §6): cost 0, Retain. Choose one of your swords (other than the current one)
/// and make it current; if it has not come out yet this combat it is summoned (Mangeomchong bonus applies).
/// Upgrade (normal Smith, it is a common card): draw 1 card.
/// </summary>
public sealed class SwordSwap : MagicSwordCard
{
    public SwordSwap() : base(0, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
        WithKeywords(CardKeyword.Retain);
        WithCards(0, 1);
    }

    protected override bool IsPlayableExtra => !IsMutable || Owner?.PlayerCombatState == null ||
                                               SwordCombat.SwitchCandidates(Owner).Count > 0;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var candidates = SwordCombat.SwitchCandidates(Owner);
        if (candidates.Count > 0)
        {
            SwordId? chosen = candidates.Count == 1 ? candidates[0] : await Choose(choiceContext, candidates);
            if (chosen is { } sword)
                await SwordCombat.SwitchTo(Owner, sword, choiceContext, SwitchReason.SwapEffect);
        }

        if (DynamicVars.Cards.IntValue > 0)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    private Task<SwordId?> Choose(PlayerChoiceContext choiceContext, IReadOnlyList<SwordId> candidates) =>
        SwordAcquisition.PickSword(Owner, candidates, choiceContext, canSkip: false,
            new LocString("card_selection", "MAGICSWORDSMAN-CHOOSE_SWORD_TO_SWITCH"));
}
