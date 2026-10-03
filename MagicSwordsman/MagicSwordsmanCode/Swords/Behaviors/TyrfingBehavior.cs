using HarmonyLib;
using MagicSwordsman.MagicSwordsmanCode.Cards.Tyrfing;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 티르빙. Spec §7 [확정]:
///  - Current effect: the owner's attacks ignore block (모든 공격, level does not change it — cards scale instead).
///    Kusanagi inherits: only the FIRST attack each turn ignores block.
///  - Cost: the first 3 times Tyrfing cards are used in the whole RUN, each adds a permanent (Eternal) curse:
///    스바프를라미의 최후 → 햐르마르의 죽음 → 앙간튀르의 죽음 (content doc §3.2). Implemented in
///    <see cref="TyrfingCard"/> with the saved Mangeomchong run counter <see cref="TyrfingCurses.RunCounterKey"/>.
///
/// How "ignore block" works (the game has no hook that changes a hit's ValueProp):
///  BeforeAttack (forwarded by CurrentSwordPower while Tyrfing is current/inherited) registers the AttackCommand in
///  <see cref="TyrfingPierce"/>; AfterAttack unregisters it. While registered, a Harmony prefix on
///  <c>Creature.DamageBlockInternal(decimal, ValueProp)</c> (the only place the game subtracts block from a hit,
///  called from CreatureCmd.Damage) adds <see cref="ValueProp.Unblockable"/> for creatures NOT on the attacker's
///  side — exactly what the game does for its own unblockable damage. Thorns-style counter damage to the player
///  during the attack is therefore unaffected.
/// </summary>
public sealed class TyrfingBehavior : SwordBehavior
{
    public override SwordId Id => SwordId.Tyrfing;

    public override IEnumerable<CardModel> StarterCards =>
        [ModelDb.Card<TyrfingFlameBlade>(), ModelDb.Card<TyrfingDwarvenRansom>()];

    /// <summary>하이드레크의 최후 (content doc §3.1).</summary>
    public override CardModel? FailureCurse => ModelDb.Card<HeidreksEnd>();

    private const string InheritedTurnKey = "inherited_pierce_turn";

    public override Task BeforeAttack(SwordContext ctx, AttackCommand command)
    {
        if (ctx.IsPreview || command.Attacker != ctx.Creature) return Task.CompletedTask;
        if (ctx.IsInherited)
        {
            // Kusanagi inheritance [확정]: only the first attack each turn ignores block.
            if (ctx.GetCounter(InheritedTurnKey) == ctx.TurnNumber) return Task.CompletedTask;
            ctx.SetCounter(InheritedTurnKey, ctx.TurnNumber);
        }

        TyrfingPierce.Begin(command, ctx.Creature.Side);
        return Task.CompletedTask;
    }

    public override Task AfterAttack(SwordContext ctx, PlayerChoiceContext choiceContext, AttackCommand command)
    {
        TyrfingPierce.End(command);
        return Task.CompletedTask;
    }

    // Safety net: an attack that never reached AfterAttack (e.g. the combat ended mid-attack) must not keep
    // piercing. Lifecycle hooks run for every owned sword whether current or not.
    public override Task OnCombatStart(SwordContext ctx, PlayerChoiceContext choiceContext)
    {
        TyrfingPierce.Clear();
        return Task.CompletedTask;
    }

    public override Task OnPlayerTurnEnd(SwordContext ctx, PlayerChoiceContext choiceContext)
    {
        TyrfingPierce.Clear();
        return Task.CompletedTask;
    }
}

/// <summary>Attacks that currently ignore block (see <see cref="TyrfingBehavior"/>).</summary>
public static class TyrfingPierce
{
    private static readonly List<(AttackCommand Command, CombatSide Side)> Active = [];

    public static void Begin(AttackCommand command, CombatSide attackerSide)
    {
        if (Active.Any(a => ReferenceEquals(a.Command, command))) return;
        Active.Add((command, attackerSide));
    }

    public static void End(AttackCommand command) => Active.RemoveAll(a => ReferenceEquals(a.Command, command));

    public static void Clear() => Active.Clear();

    /// <summary>True if block of <paramref name="creature"/> must be ignored right now.</summary>
    public static bool ShouldIgnoreBlock(Creature creature)
    {
        if (Active.Count == 0) return false;
        foreach (var (_, side) in Active)
            if (creature.Side != side) return true;
        return false;
    }
}

/// <summary>
/// Harmony prefix: while a Tyrfing attack is executing, hits on the other side are treated as Unblockable
/// (the original method then returns 0 blocked damage and leaves the target's block untouched).
/// Patched method verified in the decompiled game: <c>public decimal Creature.DamageBlockInternal(decimal amount,
/// ValueProp props)</c> — <c>props.HasFlag(ValueProp.Unblockable) ? 0m : Math.Min(Block, amount)</c>.
/// MainFile.Initialize runs harmony.PatchAll(assembly).
/// TODO(test): confirm in game (the method is small; Harmony/MonoMod disables inlining of patched methods).
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal))]
public static class TyrfingDamageBlockPatch
{
    [HarmonyPrefix]
    public static void Prefix(Creature __instance, ref ValueProp props)
    {
        if (TyrfingPierce.ShouldIgnoreBlock(__instance)) props |= ValueProp.Unblockable;
    }
}
