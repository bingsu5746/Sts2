using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Curses;

/// <summary>
/// 치지 못한 왕의 방패 — Caladbolg forge-failure curse (content doc §3.1). Unplayable.
/// At the end of your turn, if this is in your hand, the enemy with the highest HP gains 6 Block.
/// Deviation (engine): enemy block is cleared at the start of the enemy's own turn (Creature.AfterTurnStart ->
/// ClearBlock), so Block given at the end of the player's turn would vanish at once. The enemy instead gets
/// <see cref="UnstruckKingsShieldPower"/>, which grants the 6 Block right after its block is cleared at the start of
/// its turn — it keeps it through your next turn.
/// Source line: 「『쿨리의 소 떼 습격』에서 코나하르 왕을 치지 못하게 되자」.
/// </summary>
public sealed class UnstruckKingsShield : SwordCurseCard
{
    public override SwordId Sword => SwordId.Caladbolg;

    public UnstruckKingsShield()
    {
        WithBlock(6);
    }

    public override bool HasTurnEndInHandEffect => true;

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        var combatState = Owner.Creature.CombatState;
        if (combatState == null) return;
        var target = combatState.HittableEnemies.OrderByDescending(c => c.CurrentHp).FirstOrDefault();
        if (target == null) return;
        await PowerCmd.Apply<UnstruckKingsShieldPower>(choiceContext, target, DynamicVars.Block.BaseValue,
            Owner.Creature, this);
    }
}
