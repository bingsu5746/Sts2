using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 빛의 검 (SolaisSwordOfLight): when you end your turn with Claíomh Solais' effect active (current, or inherited by
/// Kusanagi), deal 2 (3 from Solais level 3) × Amount damage per Light to ALL enemies; Light is not consumed.
/// Runs at BeforeSideTurnEndEarly (game PlatingPower timing), i.e. before the behavior's end-of-turn charge.
/// </summary>
public sealed class SolaisSwordOfLightPower : MagicSwordsmanPower
{
    public const int DamagePerLight = 2;
    public const int DamagePerLightHigh = 3;
    public const int HighFromLevel = 3;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<SolaisLightPower>()];

    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Owner.IsDead || Owner.Player is not { } player) return;
        if (!SolaisLight.TryGetActiveEffect(player, out _)) return;
        var light = SolaisLight.Get(player);
        if (light <= 0 || Owner.CombatState is not { } combatState) return;

        var perLight = SwordCombat.LevelOf(player, SwordId.ClaiomhSolais) >= HighFromLevel
            ? DamagePerLightHigh
            : DamagePerLight;
        var enemies = combatState.HittableEnemies.ToList();
        if (enemies.Count == 0) return;
        Flash();
        await CreatureCmd.Damage(choiceContext, enemies, light * perLight * Amount, ValueProp.Unpowered, Owner, null);
    }
}
