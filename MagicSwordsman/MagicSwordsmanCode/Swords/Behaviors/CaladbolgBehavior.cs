using System.Runtime.CompilerServices;
using BaseLib.Patches.Features;
using HarmonyLib;
using MagicSwordsman.MagicSwordsmanCode.Cards.Caladbolg;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 칼라드볼그 (Caladbolg). Spec §7 [확정] / content doc §1.1, §2.10, 0.4 #16:
///  - Current effect: an attack that targets ONE enemy (a single-target card attack) also hits up to 2 other random
///    enemies (no duplicates) with the same damage — "up to 3 enemies". All-enemy and random attacks are unchanged.
///  - Cost: when there is only one enemy, the owner's attacks deal less damage: 20/18/16/14/12/10% less at level
///    0..5 (ModifyDamageMultiplicative). Not inherited.
///  - Kusanagi inherits: hits at most 2 enemies (1 extra), no penalty.
///  - 울스터의 영웅 (<see cref="CaladbolgUlsterHeroPower"/>): "up to 3" becomes "every enemy" and the penalty is halved
///    (Caladbolg's own effect only; the inherited version keeps 2 targets).
///  - 레테의 검 (<see cref="CaladbolgLethePower"/>): the next single-target attack(s) this turn hit up to 3 enemies
///    whatever the current sword is, and have no single-enemy penalty.
///
/// How the extra targets work (the game has no "extra target" hook): in BeforeAttack (fired by
/// AttackCommand.Execute before the first hit) the attack's target list is replaced with BaseLib's
/// <c>AttackCommand.TargetingFiltered</c> (BaseLib patches AttackCommand.GetPossibleTargets). With more than one valid
/// target, AttackCommand deals each hit to all of them in one CreatureCmd.Damage call, so every target gets its own
/// modifiers (Vulnerable...), every hit of a multi-hit attack spreads, and AfterAttack sees all results.
/// </summary>
public sealed class CaladbolgBehavior : SwordBehavior
{
    /// <summary>[확정] Caladbolg hits up to 3 enemies.</summary>
    public const int MaxTargets = 3;

    /// <summary>[확정] Kusanagi's inherited version hits up to 2 enemies.</summary>
    public const int InheritedMaxTargets = 2;

    public override SwordId Id => SwordId.Caladbolg;

    public override IEnumerable<CardModel> StarterCards =>
        [ModelDb.Card<CaladbolgHardBlade>(), ModelDb.Card<CaladbolgFairyMound>()];

    /// <summary>치지 못한 왕의 방패 (content doc §3.1).</summary>
    public override CardModel? FailureCurse => ModelDb.Card<UnstruckKingsShield>();

    /// <summary>Single-enemy damage reduction in percent at a level: 20, 18, 16, 14, 12, 10.</summary>
    public static int PenaltyPercent(int level) => 20 - 2 * Math.Clamp(level, 0, SwordRegistry.MaxLevel);

    public override Task BeforeAttack(SwordContext ctx, AttackCommand command)
    {
        if (!ctx.IsPreview) CaladbolgSpread.TryExpand(command, ctx.Player);
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(SwordContext ctx, Creature? target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (ctx.IsInherited) return 1m; // costs are not inherited
        if (dealer != ctx.Creature || !props.IsPoweredAttack()) return 1m;
        if (target == null || target.Side == ctx.Creature.Side) return 1m;
        if (CaladbolgSpread.HittableEnemyCount(ctx.Creature) != 1) return 1m;
        if (ctx.Creature.HasPower<CaladbolgLethePower>()) return 1m; // 레테의 검: no single-enemy penalty

        decimal percent = PenaltyPercent(ctx.LevelFor(cardSource));
        if (ctx.Creature.HasPower<CaladbolgUlsterHeroPower>()) percent /= 2m;
        return 1m - percent / 100m;
    }
}

/// <summary>Extra targets of single-target attacks (Caladbolg current / inherited, 레테의 검).</summary>
public static class CaladbolgSpread
{
    /// <summary>Unlimited targets (울스터의 영웅).</summary>
    public const int AllEnemies = int.MaxValue;

    private static readonly ConditionalWeakTable<AttackCommand, object> Handled = new();

    private static readonly System.Reflection.FieldInfo? SingleTargetField =
        AccessTools.Field(typeof(AttackCommand), "_singleTarget");

    public static int HittableEnemyCount(Creature creature)
    {
        var state = creature.CombatState;
        return state == null ? 0 : state.HittableEnemies.Count;
    }

    /// <summary>A card attack of the player's creature that targets exactly one enemy.</summary>
    public static bool IsEligible(AttackCommand command, Creature owner) =>
        command.Attacker == owner && command.IsSingleTargeted && !command.IsRandomlyTargeted &&
        command.ModelSource is CardModel && command.TargetSide != owner.Side;

    /// <summary>
    /// How many enemies a single-target attack of this player may hit right now (1 = no spreading):
    /// the best of Caladbolg current (3, or every enemy with 울스터의 영웅), Kusanagi inheriting Caladbolg (2)
    /// and a pending 레테의 검 (3).
    /// </summary>
    public static int MaxTargetsFor(Player player)
    {
        var max = 1;
        if (SwordCombat.CurrentSword(player) is { } current)
        {
            foreach (var (behavior, ctx) in SwordCombat.CurrentEffects(player, current, preview: false))
            {
                if (behavior.Id != SwordId.Caladbolg) continue;
                if (ctx.IsInherited) max = Math.Max(max, CaladbolgBehavior.InheritedMaxTargets);
                else if (player.Creature.HasPower<CaladbolgUlsterHeroPower>()) max = AllEnemies;
                else max = Math.Max(max, CaladbolgBehavior.MaxTargets);
            }
        }

        if (player.Creature.HasPower<CaladbolgLethePower>()) max = Math.Max(max, CaladbolgBehavior.MaxTargets);
        return max;
    }

    /// <summary>
    /// Adds up to (max - 1) other random hittable enemies to an eligible attack. Idempotent per attack: the first hook
    /// that runs (CurrentSwordPower or 레테의 검) computes the combined maximum, later calls do nothing.
    /// </summary>
    public static void TryExpand(AttackCommand command, Player player)
    {
        var owner = player.Creature;
        if (!IsEligible(command, owner)) return;
        if (Handled.TryGetValue(command, out _)) return;
        Handled.AddOrUpdate(command, new object());

        var max = MaxTargetsFor(player);
        if (max <= 1) return;

        var state = owner.CombatState;
        if (state == null) return;
        if (SingleTargetField?.GetValue(command) is not Creature primary)
        {
            MainFile.Logger.Warn("[Caladbolg] could not read the attack's target; attack not spread");
            return;
        }

        var others = state.HittableEnemies.Where(c => c != primary).ToList();
        if (others.Count == 0) return;
        if (max != AllEnemies && others.Count > max - 1)
        {
            // Same RNG stream the game uses for random attack targets (AttackCommand.Execute).
            player.RunState.Rng.CombatTargets.Shuffle(others);
            others = others.Take(max - 1).ToList();
        }

        var targets = new List<Creature> { primary };
        targets.AddRange(others);
        try
        {
            command.TargetingFiltered(targets);
        }
        catch (ArgumentException e)
        {
            // TargetingFiltered was already used on this command by someone else.
            MainFile.Logger.Warn($"[Caladbolg] attack already has custom targets, not spread: {e.Message}");
        }
    }

    /// <summary>Number of different creatures an attack damaged (or hit into block).</summary>
    public static int DistinctTargetsHit(AttackCommand command) =>
        command.Results.SelectMany(r => r).Select(r => r.Receiver).Distinct().Count();
}
