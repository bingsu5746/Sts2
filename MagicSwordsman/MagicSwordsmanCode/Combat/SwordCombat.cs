using MagicSwordsman.MagicSwordsmanCode.Visuals;
using System.Runtime.CompilerServices;
using MagicSwordsman.MagicSwordsmanCode.Cards;
using MagicSwordsman.MagicSwordsmanCode.Dialogue;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Combat;

/// <summary>
/// Static combat API for swords (spec §3). All mutations of the current sword go through here.
///
/// Rules implemented:
///  - No current sword at combat start (state is reset by Mangeomchong.BeforeCombatStart).
///  - The first time a sword's card is played in a combat the sword is summoned (once per sword per combat)
///    and becomes current. The first summon of the combat triggers Mangeomchong (Block 3 + draw 1, once).
///  - Playing any sword card makes that sword current ("C 방식"); if the sword refuses
///    (<see cref="SwordBehavior.CanBecomeCurrent"/>), the card still resolves, the current sword just stays.
///  - Onimaru-style swords (<see cref="SwordDefinition.EmergesAtCombatStart"/>) are made present by
///    <see cref="Emerge"/> at combat start — that is NOT a summon and does not make them current.
/// </summary>
public static class SwordCombat
{
    private static readonly ConditionalWeakTable<Player, SwordCombatState> States = new();

    // ------------------------------------------------------------------ state access

    /// <summary>
    /// The player's sword state for the current combat, or null outside combat. (Lifecycle: Reset in
    /// Mangeomchong.BeforeCombatStart, Clear at the end of Mangeomchong.AfterCombatEnd, so it is still readable
    /// inside SwordBehavior.OnCombatEnd.)
    /// </summary>
    public static SwordCombatState? Get(Player player) => States.TryGetValue(player, out var s) ? s : null;

    /// <summary>Like <see cref="Get"/> but creates the state when missing (still null outside combat).</summary>
    public static SwordCombatState? GetOrCreate(Player player)
    {
        if (player.PlayerCombatState == null) return null;
        return States.GetValue(player, _ => new SwordCombatState());
    }

    /// <summary>Fresh state for a new combat. Called by Mangeomchong.BeforeCombatStart.</summary>
    internal static SwordCombatState Reset(Player player)
    {
        var s = new SwordCombatState();
        States.AddOrUpdate(player, s);
        return s;
    }

    /// <summary>Drops the state after combat. Called by Mangeomchong.AfterCombatEnd.</summary>
    internal static void Clear(Player player)
    {
        States.Remove(player);
        SwordVisuals.Clear(player);
    }

    public static SwordId? CurrentSword(Player player) => Get(player)?.Current;

    public static bool IsCurrent(Player player, SwordId sword) => Get(player)?.Current == sword;

    public static bool IsPresent(Player player, SwordId sword) => Get(player)?.Present.Contains(sword) ?? false;

    /// <summary>
    /// How many times the current sword changed during the player's current turn (none -> sword counts, content doc
    /// §0.3 "전환"). 0 outside combat.
    /// </summary>
    public static int SwitchesThisTurn(Player player) =>
        Get(player)?.SwitchesInTurn(player.PlayerCombatState?.TurnNumber ?? 0) ?? 0;

    /// <summary>
    /// Per-combat counter (kept on the sword's LevelOwner) with a temporary level bonus for this combat only,
    /// e.g. Gram's "레긴의 재단조" (+2). Added by <see cref="LevelOf"/>, clamped to 0..MaxLevel.
    /// </summary>
    public const string CombatLevelBonusKey = "combat_level_bonus";

    /// <summary>
    /// Upgrade level (0..5) of a sword for this player (0 when not owned / no relic): the saved level plus the
    /// temporary combat bonus (<see cref="CombatLevelBonusKey"/>), clamped to MaxLevel.
    /// </summary>
    public static int LevelOf(Player player, SwordId sword)
    {
        var relic = player.GetRelic<Mangeomchong>();
        if (relic == null) return 0;
        var level = relic.GetLevel(sword);
        if (!relic.Owns(sword)) return level;
        var bonus = Get(player)?.GetCounter(SwordRegistry.GetDefinition(sword).LevelOwner, CombatLevelBonusKey) ?? 0;
        return bonus == 0 ? level : Math.Clamp(level + bonus, 0, SwordRegistry.MaxLevel);
    }

    /// <summary>Builds the context object passed to behaviors.</summary>
    public static SwordContext ContextFor(Player player, SwordId sword)
    {
        var state = Get(player);
        return new SwordContext
        {
            Player = player,
            Sword = sword,
            Level = LevelOf(player, sword),
            IsCurrent = state?.Current == sword,
            IsPresent = state?.Present.Contains(sword) ?? false,
            Combat = state,
            PreviousSword = state?.Previous,
        };
    }

    /// <summary>
    /// Context for previewing a sword that is NOT current yet as if a card had just switched to it
    /// (IsCurrent = true, IsPreview = true, PreviousSword = the sword current right now).
    /// </summary>
    public static SwordContext PreviewContextFor(Player player, SwordId sword)
    {
        var state = Get(player);
        return new SwordContext
        {
            Player = player,
            Sword = sword,
            Level = LevelOf(player, sword),
            IsCurrent = true,
            IsPresent = true,
            IsPreview = true,
            Combat = state,
            PreviousSword = state?.Current,
        };
    }

    /// <summary>
    /// Which sword's current-effect applies to numbers coming from <paramref name="cardSource"/>:
    ///  - a sword card of this player whose sword is not current but COULD become current (CanSwitchTo with
    ///    <see cref="SwitchReason.CardPlayed"/>) -> that sword, IsPreview = true (the card is still in hand: playing
    ///    it switches first, so this is exactly what the real hit will use);
    ///  - anything else -> the current sword (may be null), IsPreview = false.
    /// CurrentSwordPower dispatches the non-preview case, Mangeomchong the preview case, so the two never stack.
    /// </summary>
    public static (SwordId? Sword, bool IsPreview) EffectiveSwordFor(Player player, CardModel? cardSource)
    {
        var state = Get(player);
        if (state == null) return (null, false);
        if (cardSource is MagicSwordCard { Sword: { } s } card && card.IsMutable && card.Owner == player &&
            s != state.Current && CanSwitchTo(player, s, SwitchReason.CardPlayed))
            return (s, true);
        return (state.Current, false);
    }

    /// <summary>
    /// The behaviors whose current-effect hooks run for <paramref name="sword"/>: the sword itself, then (Kusanagi)
    /// the inherited sword with ctx.IsInherited = true. Used by CurrentSwordPower and Mangeomchong.
    /// </summary>
    public static IEnumerable<(SwordBehavior Behavior, SwordContext Ctx)> CurrentEffects(Player player, SwordId sword,
        bool preview)
    {
        var behavior = SwordRegistry.Get(sword);
        var ctx = preview ? PreviewContextFor(player, sword) : ContextFor(player, sword);
        yield return (behavior, ctx);

        if (behavior.GetInheritedSword(ctx) is { } inheritedId && inheritedId != sword)
        {
            var inherited = SwordRegistry.Get(inheritedId);
            if (!inherited.CanBeInherited) yield break;
            var baseCtx = ContextFor(player, inheritedId);
            yield return (inherited, new SwordContext
            {
                Player = player, Sword = inheritedId, Level = baseCtx.Level, IsInherited = true,
                IsCurrent = baseCtx.IsCurrent, IsPresent = baseCtx.IsPresent, Combat = baseCtx.Combat,
                IsPreview = preview, PreviousSword = baseCtx.PreviousSword,
            });
        }
    }

    // ------------------------------------------------------------------ queries

    /// <summary>Whether the sword could become current right now (ownership, vault, behavior rules).</summary>
    public static bool CanSwitchTo(Player player, SwordId sword, SwitchReason reason)
    {
        var state = Get(player);
        var relic = player.GetRelic<Mangeomchong>();
        if (state == null || relic == null) return false;
        if (!relic.Owns(sword)) return false;
        if (state.ReturnedToVault.Contains(sword)) return false;
        return SwordRegistry.Get(sword).CanBecomeCurrent(ContextFor(player, sword), reason);
    }

    /// <summary>Owned swords the SwordSwap card can switch to (excludes the current sword).</summary>
    public static IReadOnlyList<SwordId> SwitchCandidates(Player player)
    {
        var relic = player.GetRelic<Mangeomchong>();
        var state = Get(player);
        if (relic == null || state == null) return [];
        return relic.OwnedSwords
            .Where(s => s != state.Current)
            .Where(s => CanSwitchTo(player, s, SwitchReason.SwapEffect))
            .ToList();
    }

    /// <summary>Card-level play restriction (e.g. Skofnung on turn 1). Used by MagicSwordCard.IsPlayable.</summary>
    public static bool CanPlaySwordCard(MagicSwordCard card)
    {
        if (card.Sword is not { } sword || !card.IsMutable) return true;
        var owner = card.Owner;
        if (owner?.PlayerCombatState == null) return true;
        return SwordRegistry.Get(sword).CanPlayCard(ContextFor(owner, sword), card);
    }

    // ------------------------------------------------------------------ mutations

    /// <summary>
    /// Called by <see cref="MagicSwordCard"/> BEFORE the card's own effect: summons the sword on its first use
    /// this combat and makes it current. Safe to call for common cards (does nothing).
    /// </summary>
    public static async Task OnSwordCardPlayed(MagicSwordCard card, PlayerChoiceContext choiceContext)
    {
        if (card.Sword is not { } sword || !card.IsMutable) return;
        var owner = card.Owner;
        if (owner?.PlayerCombatState == null) return;
        SwordAffinity.OnCardPlayed(owner, sword);
        await SwitchTo(owner, sword, choiceContext, SwitchReason.CardPlayed);
    }

    /// <summary>
    /// Makes <paramref name="sword"/> the current sword (summoning it first if needed).
    /// Returns true if the sword is current afterwards.
    /// </summary>
    public static async Task<bool> SwitchTo(Player player, SwordId sword, PlayerChoiceContext choiceContext,
        SwitchReason reason)
    {
        var state = GetOrCreate(player);
        if (state == null) return false;
        if (state.Current == sword) return true;

        // Spec §3 [확정]: the first card of a sword summons it, even when the sword cannot become current
        // (e.g. Durandal at HP 30% or less — its cost only forbids the switch). A refused swap effect does not summon.
        if (reason == SwitchReason.CardPlayed && !state.Present.Contains(sword) &&
            !state.ReturnedToVault.Contains(sword) && player.GetRelic<Mangeomchong>() is { } owner &&
            owner.Owns(sword))
            await Summon(player, sword, choiceContext);

        if (!CanSwitchTo(player, sword, reason)) return false;

        var behavior = SwordRegistry.Get(sword);

        // 1) summon on first use (spec §3) — emerged swords (Onimaru) are already present: no summon.
        if (!state.Present.Contains(sword))
            await Summon(player, sword, choiceContext);

        // 2) leave the old sword
        var old = state.Current;
        if (old is { } oldSword)
            await SwordRegistry.Get(oldSword).OnLeaveCurrent(ContextFor(player, oldSword), sword, choiceContext);

        // 3) switch
        state.Previous = old;
        state.Current = sword;
        state.SwitchCount++;
        var switchTurn = player.PlayerCombatState?.TurnNumber ?? 0;
        if (state.LastSwitchTurn != switchTurn)
        {
            state.LastSwitchTurn = switchTurn;
            state.SwitchesInLastSwitchTurn = 0;
        }
        state.SwitchesInLastSwitchTurn++;
        if (sword == SwordId.Kusanagi && old != null) state.KusanagiSwitchIns++;
        SwordVisuals.Sync(player);
        MotionDirector.OnSwitched(player);

        await EnsureCurrentSwordPower(player, choiceContext);
        player.Creature.GetPower<CurrentSwordPower>()?.RefreshIcon();

        await behavior.OnBecomeCurrent(ContextFor(player, sword), old, choiceContext);

        // 4) notify every owned sword + listeners (powers / relics / combat cards implementing ISwordListener)
        var relic = player.GetRelic<Mangeomchong>();
        if (relic != null)
        {
            foreach (var owned in relic.OwnedSwords.ToList())
                await SwordRegistry.Get(owned).OnAnySwordSwitched(ContextFor(player, owned), old, sword, choiceContext);
        }

        foreach (var listener in Listeners(player))
            await listener.AfterSwordSwitched(player, old, sword, choiceContext);

        MainFile.Logger.Info($"[Sword] {player.NetId}: current sword {old?.ToString() ?? "none"} -> {sword}");
        return true;
    }

    /// <summary>
    /// Brings a sword out of Mangeomchong without making it current. Once per sword per combat.
    /// Triggers Mangeomchong's first-summon bonus. Returns false if it was already present.
    /// </summary>
    public static async Task<bool> Summon(Player player, SwordId sword, PlayerChoiceContext choiceContext)
    {
        var state = GetOrCreate(player);
        if (state == null || state.Present.Contains(sword)) return false;

        state.Present.Add(sword);
        state.Summoned.Add(sword);
        SwordVisuals.Sync(player);
        MotionDirector.OnSummoned(player, sword);
        await SwordRegistry.Get(sword).OnSummoned(ContextFor(player, sword), choiceContext);

        if (!state.FirstSummonBonusUsed)
        {
            state.FirstSummonBonusUsed = true;
            var relic = player.GetRelic<Mangeomchong>();
            if (relic != null) await relic.TriggerFirstSummonBonus(choiceContext);
        }

        foreach (var listener in Listeners(player))
            await listener.AfterSwordSummoned(player, sword, choiceContext);
        SwordTalk.OnSummoned(player, sword); // presentation: maybe a short exchange (Dialogue/SwordTalk.cs)
        return true;
    }

    /// <summary>
    /// The sword comes out on its own (Onimaru at combat start): present, NOT summoned, NOT current,
    /// no Mangeomchong bonus (spec §3).
    /// </summary>
    public static void Emerge(Player player, SwordId sword)
    {
        var state = GetOrCreate(player);
        state?.Present.Add(sword);
        SwordVisuals.Sync(player);
    }

    /// <summary>
    /// Sends a sword back into Mangeomchong for the rest of the combat (Kusanagi after its switch-ins are used up).
    /// If it was current, there is no current sword afterwards and the CurrentSwordPower is removed.
    /// </summary>
    public static async Task ReturnToVault(Player player, SwordId sword, PlayerChoiceContext choiceContext)
    {
        var state = Get(player);
        if (state == null) return;
        state.Present.Remove(sword);
        state.ReturnedToVault.Add(sword);
        SwordVisuals.Sync(player);
        if (state.Current == sword)
        {
            state.Previous = sword;
            state.Current = null;
            await EnsureCurrentSwordPower(player, choiceContext);
        }
    }

    /// <summary>Applies / refreshes / removes the visible "current sword" power to match the state.</summary>
    public static async Task EnsureCurrentSwordPower(Player player, PlayerChoiceContext choiceContext)
    {
        var state = Get(player);
        var creature = player.Creature;
        var power = creature.GetPower<CurrentSwordPower>();
        if (state?.Current == null)
        {
            if (power != null) await PowerCmd.Remove(power);
            return;
        }

        if (power == null)
            power = await PowerCmd.Apply<CurrentSwordPower>(choiceContext, creature, 1, creature, null);
        power?.Refresh();
    }

    /// <summary>Models of this player that want sword events (powers, relics, cards in combat piles).</summary>
    public static List<ISwordListener> Listeners(Player player)
    {
        var list = new List<ISwordListener>();
        list.AddRange(player.Creature.Powers.OfType<ISwordListener>());
        list.AddRange(player.Relics.OfType<ISwordListener>());
        if (player.PlayerCombatState != null)
            list.AddRange(player.PlayerCombatState.AllCards.OfType<ISwordListener>());
        return list;
    }
}

/// <summary>
/// Implement on a power, relic or card (in a combat pile) to react to sword events. Default no-ops.
/// </summary>
public interface ISwordListener
{
    Task AfterSwordSwitched(Player player, SwordId? from, SwordId to, PlayerChoiceContext choiceContext) =>
        Task.CompletedTask;

    Task AfterSwordSummoned(Player player, SwordId sword, PlayerChoiceContext choiceContext) => Task.CompletedTask;
}
