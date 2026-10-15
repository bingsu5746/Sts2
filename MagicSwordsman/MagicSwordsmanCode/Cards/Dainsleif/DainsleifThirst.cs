using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Dainsleif;

/// <summary>
/// 다인의 갈증 (Common): 5 damage (+1/L). Every time this card is DRAWN, it deals 2 more damage (3 from level 3) for
/// the rest of the combat (per card instance; Void pattern: AfterCardDrawn with card == this).
/// User request 2026-10-15 (#21): a Dainsleif card that grows each time it is drawn — Dainsleif's current effect keeps
/// putting played cards back into the draw pile, so this card is redrawn (and grows) far more often than usual.
/// Lore (Skáldskaparmál, high confidence): "every time it is drawn (from the sheath) it must cause a man's death" —
/// the card-draw / sword-draw pun: each drawing makes the blade thirstier.
/// </summary>
public sealed class DainsleifThirst : DainsleifCard
{
    private int _bonus;

    public DainsleifThirst() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(5);
        WithDamagePerLevel(1);
        WithCalculatedVar("Growth", 2, static (card, _) => card is MagicSwordCard { SwordLevel: >= 3 } ? 1 : 0);
    }

    private int Growth => SwordLevel >= 3 ? 3 : 2;

    protected override decimal BonusDamage(Creature? target) => _bonus;

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (ReferenceEquals(card, this) && IsMutable) _bonus += Growth;

        return base.AfterCardDrawn(choiceContext, card, fromHandDraw);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}
