using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Basic;

/// <summary>
/// 부서진 칼날 — Gram's starting card (spec §6): cost 1, 7 damage. Playing it summons Gram.
/// Like every sword card it cannot be upgraded; it gains +1 damage per Gram level instead.
/// </summary>
public sealed class BrokenBlade : MagicSwordCard
{
    public override SwordId? Sword => SwordId.Gram;

    public BrokenBlade() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithDamage(7);
        WithDamagePerLevel(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}
