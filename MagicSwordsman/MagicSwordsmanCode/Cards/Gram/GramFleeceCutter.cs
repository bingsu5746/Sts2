using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Gram;

/// <summary>
/// 양털 가르기 — Gram, Attack, Common, cost 0, one enemy. Deal 4 (+⌊L/2⌋) damage. 【연속】 draw 1.
/// Lore: a tuft of wool floated down the river was cut by simply touching the blade.
/// </summary>
public sealed class GramFleeceCutter : GramCard
{
    public GramFleeceCutter() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(4);
        WithCards(1);
    }

    protected override int LevelDamageBonus(int level) => level / 2;

    protected override bool HasComboEffect => true;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combo = IsCombo;
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (combo) await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}
