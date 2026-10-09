using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Cards.Gram;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Basic;

/// <summary>
/// 부서진 칼날 — Gram's Basic attack (spec §6 [확정]): cost 1, 7 damage. Playing it summons Gram.
/// Since 2026-10-09 it is one of Gram's two starter cards (GramBehavior.StarterCards, with 간직한 조각), granted with
/// Gram like every sword's starter cards, instead of sitting in the fixed starting deck.
/// Like every sword card it cannot be upgraded. Content doc §2.1 / §0.2: Gram cards scale at half rate because
/// Gram's current-sword effect (+L) already raises every attack, so it gains +⌊L/2⌋ damage (5단계 실효 7+2+5 = 14).
/// Derives from <see cref="GramCard"/> so the scaling goes through the same damage hook as the other Gram cards.
/// </summary>
public sealed class BrokenBlade : GramCard
{
    public BrokenBlade() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithDamage(7);
    }

    protected override int LevelDamageBonus(int level) => level / 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}
