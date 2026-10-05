using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Kusanagi;

// 쿠사나기노쓰루기 — 계승 · 정화 · 10장 (content doc §2.3). Basic 2 · Common 3 · Uncommon 3 · Rare 2.

/// <summary>풀 베기 (Basic): 7 damage (+1/L). Choose a status/curse card in hand and exhaust it.</summary>
public sealed class KusanagiGrassCutter : KusanagiCard
{
    public KusanagiGrassCutter() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithDamage(7);
        WithDamagePerLevel(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        await KusanagiPurify.ExhaustOneFromHand(choiceContext, Owner, this);
    }
}
