using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Gram;

/// <summary>
/// 두 조각 — Gram, Attack, Common, cost 1, one enemy. Deal 4 damage twice; from Gram level 4, +1 per hit.
/// Gram's current-sword effect applies to every hit. Lore: Regin re-forged the two pieces.
/// </summary>
public sealed class GramTwoShards : GramCard
{
    private const int Hits = 2;

    public GramTwoShards() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(4);
        WithVar("Hits", Hits);
    }

    protected override int LevelDamageBonus(int level) => level >= 4 ? 1 : 0;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay, Hits)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}
