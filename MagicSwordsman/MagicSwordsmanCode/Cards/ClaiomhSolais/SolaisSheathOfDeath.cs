using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

// 클라이브 솔라시 — 빛 · 발도 · 10장 (content doc §2.9). Basic 2 · Common 3 · Uncommon 3 · Rare 2.
// Source line: 「『마그 투이레드 2차 전투』: 죽음의 칼집에서 뽑히면 누구도 벗어나지 못했고」.

/// <summary>죽음의 칼집 (Basic): 【발도】 4 damage (+1/L) + K per Light, ignores Block.</summary>
public sealed class SolaisSheathOfDeath : SolaisDrawCutCard
{
    public SolaisSheathOfDeath() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithDamage(4);
        WithDamagePerLevel(1);
        WithLightDamageVar("PerLight", 4, 1);
        WithTip(typeof(SolaisLightPower));
    }

    protected override async Task OnDrawCut(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DrawCutAttack(cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}
