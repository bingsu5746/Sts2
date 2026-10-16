using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

/// <summary>
/// 백광 (Uncommon): 5 damage (+1/L) to ALL enemies. Then spend HALF your Light (rounded up) and apply that much Weak
/// to ALL enemies (1 Light -> 1 Weak, 4 -> 2, 5 -> 3). Nothing happens without Light.
/// Reworked again 2026-10-15 (user request: spending differed only by numbers — "spend 1 / spend 2 / spend 3"): the
/// fixed "spend 2" became "spend half", so the card scales with a big store of Light but never empties it.
/// Source: 「누구도 벗어나지 못했고」 — when the sword of light was drawn none could escape it; half the light is
/// enough to blind everyone.
/// </summary>
public sealed class SolaisInescapable : SolaisCard
{
    public SolaisInescapable() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithDamage(5);
        WithDamagePerLevel(1);
        WithCalculatedVar("HalfLight", 0,
            static (card, _) => card is SolaisInescapable s ? (s.Light + 1) / 2 : 0);
        WithTip(typeof(SolaisLightPower));
        WithTip(typeof(WeakPower));
    }

    protected override bool ShouldGlowGoldInternal => Light > 0;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").SpawningHitVfxOnEachCreature()
            .Execute(choiceContext);
        if (Owner.Creature.CombatState is not { } cs) return;
        var enemies = cs.HittableEnemies.ToList();
        if (enemies.Count == 0) return;
        var spent = await SolaisLightSpend.SpendHalf(choiceContext, Owner, this);
        if (spent <= 0) return;
        await PowerCmd.Apply<WeakPower>(choiceContext, enemies, spent, Owner.Creature, this);
    }
}
