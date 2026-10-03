using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Gram;

/// <summary>
/// 다시 벼린 그람 — Gram, Attack, Uncommon, cost 1, one enemy. Deal 6 (+⌊L/2⌋) damage. Choose a Gram Attack card
/// in the discard pile and put it into your hand (selection pattern: game card Hologram).
/// Lore: Regin re-forged the two pieces.
/// </summary>
public sealed class GramReforged : GramCard
{
    public GramReforged() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(6);
    }

    protected override int LevelDamageBonus(int level) => level / 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        var discard = PileType.Discard.GetPile(Owner);
        if (!discard.Cards.Any(IsGramAttack)) return;
        var picked = (await CardSelectCmd.FromCombatPile(choiceContext, discard, Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1), IsGramAttack)).FirstOrDefault();
        if (picked != null) await CardPileCmd.Add(picked, PileType.Hand);
    }

    private static bool IsGramAttack(MegaCrit.Sts2.Core.Models.CardModel card) =>
        card is MagicSwordCard { Sword: Swords.SwordId.Gram, Type: CardType.Attack };
}
