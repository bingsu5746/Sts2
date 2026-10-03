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

/// <summary>
/// 벗어날 수 없는 빛 (Uncommon): 【발도】 4 damage (+1/L) + K per Light to ALL enemies, ignores Block.
/// Source: 「누구도 벗어나지 못했고」.
/// </summary>
public sealed class SolaisInescapable : SolaisDrawCutCard
{
    public SolaisInescapable() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithDamage(4);
        WithDamagePerLevel(1);
        WithLightDamageVar("PerLight", 4, 1);
        WithTip(typeof(SolaisLightPower));
    }

    protected override async Task OnDrawCut(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DrawCutAttack(cardPlay).WithHitFx("vfx/vfx_attack_slash").SpawningHitVfxOnEachCreature()
            .Execute(choiceContext);
    }
}
