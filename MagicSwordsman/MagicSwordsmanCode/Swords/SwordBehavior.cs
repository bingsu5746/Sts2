using MagicSwordsman.MagicSwordsmanCode.Cards;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Swords;

/// <summary>
/// Base class for one magic sword's rules. ONE instance per sword (stateless singleton): keep per-combat
/// state in <see cref="SwordContext.Combat"/> counters and per-run state in Mangeomchong run counters.
///
/// Three groups of extension points:
///  1. Run level (deck, acquisition, upgrades) — <see cref="StarterCards"/>, <see cref="FailureCurse"/>,
///     <see cref="OnAcquired"/>, <see cref="OnLost"/>, <see cref="OnLevelChanged"/>.
///  2. Owned-sword lifecycle in combat — called by Mangeomchong for EVERY owned sword whether or not it is
///     current: <see cref="OnCombatStart"/>, <see cref="OnPlayerTurnStart"/>, <see cref="OnPlayerTurnEnd"/>,
///     <see cref="OnCombatEnd"/>, <see cref="ModifyCardKeywords"/>, <see cref="TryModifyEnergyCost"/>,
///     plus switching: <see cref="CanBecomeCurrent"/>, <see cref="CanPlayCard"/>, <see cref="OnSummoned"/>,
///     <see cref="OnBecomeCurrent"/>, <see cref="OnLeaveCurrent"/>, <see cref="OnAnySwordSwitched"/>.
///  3. "Current sword effect" (현재 검 효과) — dispatched by CurrentSwordPower ONLY while this sword is current
///     (or inherited by Kusanagi, then ctx.IsInherited == true): Modify* and After* combat hooks below.
///     These mirror the game's AbstractModel hooks one-to-one and receive the same arguments, so read the game
///     source (e.g. StrengthPower, DexterityPower) for semantics. IMPORTANT: they fire for every creature's
///     damage/block in the combat — always check dealer/target against ctx.Creature.
/// </summary>
public abstract class SwordBehavior
{
    public abstract SwordId Id { get; }

    public SwordDefinition Definition => SwordRegistry.GetDefinition(Id);

    // =====================================================================================
    // 1. Run level
    // =====================================================================================

    /// <summary>
    /// Canonical cards added to the deck the FIRST time this sword is ever acquired (spec §5: 2 cards).
    /// Use ModelDb.Card&lt;T&gt;() — evaluated lazily at acquisition time, never at registration.
    /// For a pair (Ganjiang/Moye) each sword grants its own list.
    /// </summary>
    public virtual IEnumerable<CardModel> StarterCards => [];

    /// <summary>
    /// Canonical curse card added on a failed gamble upgrade (spec §4, user: "저주는 카드마다 다르게").
    /// Return ModelDb.Card&lt;YourCurse&gt;(). Null = no curse yet: failure always lowers the level instead
    /// (and at level 0 nothing happens). Content agents: implement your curse in Curses/ and return it here.
    /// </summary>
    public virtual CardModel? FailureCurse => null;

    /// <summary>After the sword (and its partners) were added to Mangeomchong. firstTime = never owned before in this run.</summary>
    public virtual Task OnAcquired(Player player, bool firstTime) => Task.CompletedTask;

    /// <summary>
    /// The run-level part of <see cref="OnAcquired"/> (firstTime = true) for the random first sword of a run, which is
    /// granted while the run is still being created (Mangeomchong.AfterObtained via RunManager.FinalizeStartingRelics,
    /// before any room or UI exists). Must change state directly and synchronously — no commands, no visuals.
    /// Same effect as the first acquisition (e.g. 간장·막야 Max HP cost). Default: nothing.
    /// </summary>
    public virtual void OnAcquiredAsStartingSword(Player player)
    {
    }

    /// <summary>After the sword was removed from Mangeomchong (its cards are already moved into storage).</summary>
    public virtual Task OnLost(Player player) => Task.CompletedTask;

    /// <summary>After the saved level of this sword changed (rest-site upgrade, failure, events...).</summary>
    public virtual Task OnLevelChanged(Player player, int oldLevel, int newLevel) => Task.CompletedTask;

    // =====================================================================================
    // 2a. Switching rules (combat)
    // =====================================================================================

    /// <summary>
    /// Whether the sword may become current right now. Return false for costs such as
    /// Durandal (HP &lt;= 30%), Skofnung (first turn), Kusanagi (switch-in limit used up).
    /// When this returns false for a card play, the card's own effect still happens (spec §3 C rule),
    /// the current sword simply does not change.
    /// </summary>
    public virtual bool CanBecomeCurrent(SwordContext ctx, SwitchReason reason) => true;

    /// <summary>Whether one of this sword's cards may be played at all (Skofnung: not on turn 1).</summary>
    public virtual bool CanPlayCard(SwordContext ctx, MagicSwordCard card) => true;

    /// <summary>The first time this combat the sword comes out of Mangeomchong (before it becomes current).</summary>
    public virtual Task OnSummoned(SwordContext ctx, PlayerChoiceContext choiceContext) => Task.CompletedTask;

    /// <summary>Right after this sword became current. previous = the sword that was current before (or null).</summary>
    public virtual Task OnBecomeCurrent(SwordContext ctx, SwordId? previous, PlayerChoiceContext choiceContext) =>
        Task.CompletedTask;

    /// <summary>Right before another sword becomes current (this sword stops being current).</summary>
    public virtual Task OnLeaveCurrent(SwordContext ctx, SwordId next, PlayerChoiceContext choiceContext) =>
        Task.CompletedTask;

    /// <summary>Called on every OWNED sword after any switch (pair bonuses, counters...).</summary>
    public virtual Task OnAnySwordSwitched(SwordContext ctx, SwordId? from, SwordId to,
        PlayerChoiceContext choiceContext) => Task.CompletedTask;

    // =====================================================================================
    // 2b. Owned-sword lifecycle (called for every owned sword; check ctx.IsCurrent / ctx.IsPresent)
    // =====================================================================================

    /// <summary>Start of combat (turn 1, before the opening hand is drawn). Onimaru emerges here.</summary>
    public virtual Task OnCombatStart(SwordContext ctx, PlayerChoiceContext choiceContext) => Task.CompletedTask;

    /// <summary>Start of each of the owner's turns (after energy/draw).</summary>
    public virtual Task OnPlayerTurnStart(SwordContext ctx, PlayerChoiceContext choiceContext) => Task.CompletedTask;

    /// <summary>End of the owner's turn (Claiomh Solais charges light here if it is current, loses it otherwise).</summary>
    public virtual Task OnPlayerTurnEnd(SwordContext ctx, PlayerChoiceContext choiceContext) => Task.CompletedTask;

    /// <summary>After combat ends (Kusanagi's HP loss). Combat state is still readable here.</summary>
    public virtual Task OnCombatEnd(SwordContext ctx, CombatRoom room) => Task.CompletedTask;

    /// <summary>
    /// Change keywords of THIS sword's cards in combat (Kusanagi exhausted: add Exhaust + Ethereal).
    /// Return true if anything changed. Called only for cards whose Sword == Id.
    /// </summary>
    public virtual bool ModifyCardKeywords(SwordContext ctx, MagicSwordCard card, ISet<CardKeyword> keywords) => false;

    /// <summary>Change the energy cost of THIS sword's cards in combat. Return true if modified.</summary>
    public virtual bool TryModifyEnergyCost(SwordContext ctx, MagicSwordCard card, decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        return false;
    }

    // =====================================================================================
    // 3. Current sword effect (dispatched by CurrentSwordPower while current / inherited)
    // =====================================================================================

    /// <summary>
    /// Kusanagi only: which sword's current-effect to run additionally (as inherited). Default: none.
    /// </summary>
    public virtual SwordId? GetInheritedSword(SwordContext ctx) => null;

    /// <summary>Whether Kusanagi may inherit this sword's effect. Default true.</summary>
    public virtual bool CanBeInherited => true;

    public virtual decimal ModifyDamageAdditive(SwordContext ctx, Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource) => 0m;

    public virtual decimal ModifyDamageMultiplicative(SwordContext ctx, Creature? target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource) => 1m;

    public virtual decimal ModifyBlockAdditive(SwordContext ctx, Creature target, decimal block, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay) => 0m;

    public virtual decimal ModifyBlockMultiplicative(SwordContext ctx, Creature target, decimal block,
        ValueProp props, CardModel? cardSource, CardPlay? cardPlay) => 1m;

    public virtual int ModifyAttackHitCount(SwordContext ctx, AttackCommand attack, int hitCount) => hitCount;

    /// <summary>Dainsleif: send played cards to the draw pile instead of the discard pile.</summary>
    public virtual (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(SwordContext ctx,
        CardModel card, bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position) =>
        (pileType, position);

    public virtual Task BeforeCardPlayed(SwordContext ctx, CardPlay cardPlay) => Task.CompletedTask;

    public virtual Task AfterCardPlayed(SwordContext ctx, PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Task.CompletedTask;

    public virtual Task BeforeAttack(SwordContext ctx, AttackCommand command) => Task.CompletedTask;

    public virtual Task AfterAttack(SwordContext ctx, PlayerChoiceContext choiceContext, AttackCommand command) =>
        Task.CompletedTask;

    public virtual Task AfterDamageGiven(SwordContext ctx, PlayerChoiceContext choiceContext, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource) => Task.CompletedTask;

    public virtual Task AfterDamageReceived(SwordContext ctx, PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource) => Task.CompletedTask;

    public override string ToString() => $"{GetType().Name}({Id})";
}

/// <summary>Used for swords that have no behavior registered yet. Has no effect at all.</summary>
public sealed class PlaceholderSwordBehavior(SwordId id) : SwordBehavior
{
    public override SwordId Id { get; } = id;
}
