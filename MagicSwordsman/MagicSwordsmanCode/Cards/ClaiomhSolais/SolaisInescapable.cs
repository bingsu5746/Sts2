using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

/// <summary>
/// 백광 (Uncommon): 5 damage (+1/L) to ALL enemies. Then, if you have 2+ Light, spend 2 Light: apply 1 Weak to ALL
/// enemies (2 from level 3).
/// Reworked 2026-10-15 (user request #38): it used to be a third "consume all Light, X + K per Light" 【발도】 card.
/// Now it spends a fixed 2 Light for a blinding debuff instead (Weak = enemies swing half-blind).
/// Source: 「누구도 벗어나지 못했고」 — when the sword of light was drawn none could escape it; here no one escapes
/// the glare.
/// </summary>
public sealed class SolaisInescapable : SolaisCard
{
    public const int LightCost = 2;

    public SolaisInescapable() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithDamage(5);
        WithDamagePerLevel(1);
        WithVar("LightCost", LightCost);
        WithCalculatedVar("Weak", 1, static (card, _) => card is MagicSwordCard { SwordLevel: >= 3 } ? 1 : 0);
        WithTip(typeof(SolaisLightPower));
        WithTip(typeof(WeakPower));
    }

    private int WeakAmount => SwordLevel >= 3 ? 2 : 1;

    protected override bool ShouldGlowGoldInternal => Light >= LightCost;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").SpawningHitVfxOnEachCreature()
            .Execute(choiceContext);
        if (Owner.Creature.CombatState is not { } cs) return;
        var enemies = cs.HittableEnemies.ToList();
        if (enemies.Count == 0) return;
        if (!await SolaisLightSpend.TrySpend(choiceContext, Owner, LightCost, this)) return;
        await PowerCmd.Apply<WeakPower>(choiceContext, enemies, WeakAmount, Owner.Creature, this);
    }
}
