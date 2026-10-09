using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Gram;

/// <summary>
/// 간직한 조각 — Gram, Skill, Basic (starter), cost 1, self. Gain 5 (+1/L) Block. 【연속】 draw 1.
/// Added 2026-10-09 when Gram stopped being the fixed first sword: every sword now brings 2 Basic starter cards
/// (an attack and a skill), and Gram only had 부서진 칼날. Numbers match the other starter skills (막야 막기 5 +1/L,
/// 【짝】 draw 1). Block is not raised by Gram's current-sword effect, so it scales at the normal +1/L (content doc §0.2).
/// Lore (Völsunga saga ch. 12): the dying Sigmund told Hjördis to keep the shards of the sword for their unborn son;
/// she kept them, and Regin later forged them into Gram again for Sigurd.
/// </summary>
public sealed class GramKeptShards : GramCard
{
    public GramKeptShards() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
        WithBlock(5);
        WithBlockPerLevel(1);
        WithCards(1);
    }

    protected override bool HasComboEffect => true;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combo = IsCombo;
        await CommonActions.CardBlock(this, cardPlay);
        if (combo) await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}
