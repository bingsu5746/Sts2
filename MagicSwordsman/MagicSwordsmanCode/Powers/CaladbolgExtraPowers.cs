using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 페르구스의 분노 (CaladbolgFergusWrath): this combat, whenever one of your attacks hits 2 or more enemies, deal Amount
/// damage to ALL enemies (unpowered, like Thorns; the follow-up is not an attack, so it does not chain).
/// </summary>
public sealed class CaladbolgFergusWrathPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (command.Attacker != Owner || Owner.IsDead) return;
        if (CaladbolgSpread.DistinctTargetsHit(command) < 2) return;
        var enemies = Owner.CombatState?.HittableEnemies.ToList();
        if (enemies == null || enemies.Count == 0) return;
        Flash();
        VfxCmd.PlayOnCreatures(enemies, "vfx/vfx_attack_slash");
        await CreatureCmd.Damage(choiceContext, enemies, Amount, ValueProp.Unpowered, Owner, null);
    }
}
