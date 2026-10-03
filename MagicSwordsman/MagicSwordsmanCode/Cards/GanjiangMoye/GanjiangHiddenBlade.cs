using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

/// <summary>
/// 숨긴 수컷 검 — Ganjiang, Attack, Uncommon, cost 1, one enemy, Retain. Deal 7 (+1/L) damage.
/// Every time it is retained over a turn end, it deals +3 damage for the rest of the combat.
/// Retention detected with the game hook AfterFlush(retainedCards) (no game card does this; hook verified in Hook.cs).
/// Lore: Ganjiang was killed for hiding the male sword.
/// </summary>
public sealed class GanjiangHiddenBlade : GanjiangMoyeCard
{
    private int _retainBonus;

    public GanjiangHiddenBlade() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(7);
        WithDamagePerLevel(1);
        WithVar("RetainBonus", 3);
        WithKeywords(CardKeyword.Retain);
    }

    protected override SwordId Side => SwordId.Ganjiang;
    protected override bool HasPairEffect => false;

    protected override int ExtraDamage(Creature? target) => _retainBonus;

    public override Task AfterFlush(PlayerChoiceContext choiceContext, Player player,
        IReadOnlyCollection<CardModel> flushedCards, IReadOnlyCollection<CardModel> retainedCards)
    {
        if (IsMutable && player == Owner && retainedCards.Contains(this))
            _retainBonus += DynamicVars["RetainBonus"].IntValue;
        return base.AfterFlush(choiceContext, player, flushedCards, retainedCards);
    }

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_dramatic_stab")
            .Execute(choiceContext);
    }
}
