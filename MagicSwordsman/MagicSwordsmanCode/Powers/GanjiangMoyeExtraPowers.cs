using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 쌍룡 (card TwinYanpingDragons): this combat, whenever a 【짝】 (Ganjiang/Moye pair) effect triggers, deal Amount
/// damage to a random enemy. Triggered by Cards/GanjiangMoye/PairRules. Unpowered damage, like the game's
/// JuggernautPower (no Strength / current-sword bonus, no double counting).
/// </summary>
public sealed class TwinDragonsPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task OnPairTriggered()
    {
        if (Amount <= 0 || Owner == null || Owner.IsDead || Owner.Player == null) return;
        var enemies = CombatState.HittableEnemies;
        if (enemies.Count == 0) return;
        var target = Owner.Player.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target == null) return;
        Flash();
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), target, Amount, ValueProp.Unpowered, Owner, null);
    }
}
