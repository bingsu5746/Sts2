using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 방패를 버리다 (DainsleifShieldCastAside, 2026-10-17): whenever a CARD would give you Block while Dáinsleif is your
/// current sword (its cost turns that Block into 0 — DainsleifBehavior.ModifyBlockMultiplicative; the cost is not
/// inherited by Kusanagi, so neither is this), deal Amount damage to a random enemy instead.
/// Hook: BeforeBlockGained (runs once per CreatureCmd.GainBlock, before the block modifiers); damage/target pattern:
/// the game's JuggernautPower / DainsleifNeverMissesPower (Unpowered, Rng.CombatTargets).
/// </summary>
public sealed class DainsleifShieldCastAsidePower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
    {
        if (creature != Owner || cardSource == null || amount <= 0m || !Owner.IsAlive) return;
        if (Owner.Player is not { } player || SwordCombat.CurrentSword(player) != SwordId.Dainsleif) return;
        var enemies = CombatState?.HittableEnemies;
        if (enemies == null || enemies.Count == 0) return;
        var target = player.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target == null) return;
        Flash();
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), target, Amount, ValueProp.Unpowered, Owner, null);
    }
}
