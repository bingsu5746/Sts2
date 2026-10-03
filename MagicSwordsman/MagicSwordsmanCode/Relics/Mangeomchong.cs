using System.Runtime.CompilerServices;
using MagicSwordsman.MagicSwordsmanCode.Cards;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.RestSite;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Relics;

/// <summary>
/// 만검총(萬劍塚) — starting relic and the run-level owner of all sword data (spec §2).
///
/// Saved (all [SavedProperty]; types verified as supported by the game + BaseLib: int, int[], string,
/// List&lt;SerializableCard&gt;; BaseLib's PostModInitPatch registers mod types into SavedPropertiesTypeCache):
///  - OwnedSwordIds   int[]  owned SwordIds (as ints)
///  - SwordLevels     int[]  level per SwordId value (index = (int)SwordId); pairs use the leader's index
///  - EverOwnedIds    int[]  swords owned at least once (starter cards are granted only the first time)
///  - ExtraSlots      int    slot expansions from relics / events
///  - StoredCards     List&lt;SerializableCard&gt;  cards of lost swords (pattern: PaelsTooth)
///  - RunCounters     string "key=value;..." free-form per-run counters for content (Tyrfing curse count...)
///
/// Effects:
///  - Card rewards and shops only offer common cards + cards of owned swords.
///  - Rest site: adds "마검 강화" (SwordForgeRestSiteOption).
///  - Combat: first sword summon of each combat -> Block 3 + draw 1 (once). Drives the per-combat sword state and
///    the owned-sword lifecycle hooks of every SwordBehavior.
/// </summary>
public sealed class Mangeomchong : MagicSwordsmanRelic
{
    private const string SwordListVar = "SwordList";

    private int[] _ownedSwordIds = [(int)SwordId.Gram];
    private int[] _swordLevels = new int[16];
    private int[] _everOwnedIds = [(int)SwordId.Gram];
    private int _extraSlots;
    private List<SerializableCard> _storedCards = new();
    private string _runCounters = "";

    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(3m, ValueProp.Unpowered),
        new CardsVar(1),
        new StringVar(SwordListVar),
    ];

    public override bool ShowCounter => IsMutable;

    /// <summary>Counter on the relic icon: number of owned swords.</summary>
    public override int DisplayAmount => IsMutable ? _ownedSwordIds.Length : 0;

    // =====================================================================================
    // Saved properties
    // =====================================================================================

    [SavedProperty]
    public int[] OwnedSwordIds
    {
        get => _ownedSwordIds;
        private set
        {
            AssertMutable();
            _ownedSwordIds = value ?? [];
            UpdateSwordList();
        }
    }

    [SavedProperty]
    public int[] SwordLevels
    {
        get => _swordLevels;
        private set
        {
            AssertMutable();
            _swordLevels = value ?? new int[16];
            UpdateSwordList();
        }
    }

    [SavedProperty]
    public int[] EverOwnedIds
    {
        get => _everOwnedIds;
        private set
        {
            AssertMutable();
            _everOwnedIds = value ?? [];
        }
    }

    [SavedProperty]
    public int ExtraSlots
    {
        get => _extraSlots;
        private set
        {
            AssertMutable();
            _extraSlots = value;
        }
    }

    [SavedProperty]
    public List<SerializableCard> StoredCards
    {
        get => _storedCards;
        private set
        {
            AssertMutable();
            _storedCards.Clear();
            if (value != null) _storedCards.AddRange(value);
        }
    }

    [SavedProperty]
    public string RunCounters
    {
        get => _runCounters;
        private set
        {
            AssertMutable();
            _runCounters = value ?? "";
        }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        // Arrays are replaced (never mutated in place), but the list must not be shared between clones.
        _storedCards = new List<SerializableCard>(_storedCards);
    }

    // =====================================================================================
    // Sword ownership API
    // =====================================================================================

    public IReadOnlyList<SwordId> OwnedSwords => _ownedSwordIds.Select(i => (SwordId)i).ToList();

    public bool Owns(SwordId sword) => _ownedSwordIds.Contains((int)sword);

    public bool EverOwned(SwordId sword) => _everOwnedIds.Contains((int)sword);

    public int SlotCapacity => SwordRegistry.BaseSlotCapacity + _extraSlots;

    public int SlotsUsed => OwnedSwords.Sum(s => SwordRegistry.GetDefinition(s).SlotCost);

    public int FreeSlots => SlotCapacity - SlotsUsed;

    /// <summary>Spec §2: base 3 slots, relics/events may add 1-2.</summary>
    public void AddExtraSlots(int amount)
    {
        ExtraSlots = Math.Max(0, _extraSlots + amount);
    }

    /// <summary>Not owned yet and the sword + its partners fit into the free slots.</summary>
    public bool CanAcquire(SwordId sword)
    {
        var group = SwordRegistry.WithPartners(sword);
        if (group.Any(Owns)) return false;
        return SwordRegistry.GroupSlotCost(sword) <= FreeSlots;
    }

    /// <summary>
    /// Adds the sword (and its partners: Ganjiang &lt;-&gt; Moye) to Mangeomchong.
    /// First acquisition in the run: grants each sword's StarterCards. Re-acquisition: restores the stored cards.
    /// Returns false when already owned or there is no room (unless <paramref name="ignoreSlotLimit"/>);
    /// an event that wants to replace a sword should call <see cref="LoseSword"/> first.
    /// </summary>
    public async Task<bool> AcquireSword(SwordId sword, bool ignoreSlotLimit = false)
    {
        var group = SwordRegistry.WithPartners(sword);
        if (group.Any(Owns)) return false;
        if (!ignoreSlotLimit && SwordRegistry.GroupSlotCost(sword) > FreeSlots) return false;

        var firstTime = new Dictionary<SwordId, bool>();
        foreach (var s in group) firstTime[s] = !EverOwned(s);

        OwnedSwordIds = _ownedSwordIds.Concat(group.Select(s => (int)s)).Distinct().ToArray();
        EverOwnedIds = _everOwnedIds.Concat(group.Select(s => (int)s)).Distinct().ToArray();

        var results = new List<CardPileAddResult>();
        foreach (var s in group)
        {
            if (firstTime[s])
            {
                foreach (var canonical in SwordRegistry.Get(s).StarterCards)
                {
                    var card = Owner.RunState.CreateCard(canonical, Owner);
                    results.Add(await CardPileCmd.Add(card, PileType.Deck));
                }
            }
        }

        results.AddRange(await RestoreStoredCards(group));
        if (results.Count > 0) CardCmd.PreviewCardPileAdd(results);

        foreach (var s in group) await SwordRegistry.Get(s).OnAcquired(Owner, firstTime[s]);

        Flash();
        MainFile.Logger.Info($"[Mangeomchong] acquired {string.Join(",", group)}");
        return true;
    }

    /// <summary>
    /// Removes the sword (and partners) from Mangeomchong and moves all of their cards from the deck into storage.
    /// The level is reset to 0 ([Claude] decision: a lost/shattered sword comes back un-upgraded).
    /// Gram cannot be lost by a shatter (SwordForge handles that), but this method does not block it.
    /// </summary>
    public async Task LoseSword(SwordId sword)
    {
        var group = SwordRegistry.WithPartners(sword);
        if (!group.Any(Owns)) return;

        var toStore = Owner.Deck.Cards
            .Where(c => c is MagicSwordCard { Sword: { } s } && group.Contains(s))
            .ToList();
        foreach (var card in toStore) _storedCards.Add(card.ToSerializable());
        if (toStore.Count > 0) await CardPileCmd.RemoveFromDeck(toStore);

        OwnedSwordIds = _ownedSwordIds.Where(i => !group.Contains((SwordId)i)).ToArray();
        foreach (var s in group) WriteLevel(s, 0);
        UpdateSwordList();

        foreach (var s in group) await SwordRegistry.Get(s).OnLost(Owner);
        Flash();
        MainFile.Logger.Info($"[Mangeomchong] lost {string.Join(",", group)}; stored {toStore.Count} cards");
    }

    private async Task<List<CardPileAddResult>> RestoreStoredCards(IReadOnlyList<SwordId> group)
    {
        var results = new List<CardPileAddResult>();
        var restored = new List<SerializableCard>();
        foreach (var saved in _storedCards.ToList())
        {
            if (StoredSwordOf(saved) is not { } s || !group.Contains(s)) continue;

            CardModel card;
            try
            {
                card = CardModel.FromSerializable(saved); // pattern: PaelsTooth
            }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"[Mangeomchong] could not restore stored card {saved.Id}: {e.Message}");
                continue;
            }

            if (!Owner.RunState.ContainsCard(card)) Owner.RunState.AddCard(card, Owner);
            results.Add(await CardPileCmd.Add(card, PileType.Deck));
            restored.Add(saved);
        }

        foreach (var s in restored) _storedCards.Remove(s);
        return results;
    }

    /// <summary>The sword a stored card belongs to (looked up on the canonical model; null if unknown/common).</summary>
    private static SwordId? StoredSwordOf(SerializableCard saved)
    {
        if (saved.Id is not { } id) return null;
        return ModelDb.GetByIdOrNull<CardModel>(id) is MagicSwordCard { Sword: { } s } ? s : null;
    }

    /// <summary>Number of stored cards that belong to a sword or its partners (for UI / events).</summary>
    public int StoredCardCount(SwordId sword)
    {
        var group = SwordRegistry.WithPartners(sword);
        return _storedCards.Count(saved => StoredSwordOf(saved) is { } s && group.Contains(s));
    }

    // =====================================================================================
    // Levels
    // =====================================================================================

    /// <summary>Saved level 0..5 (pairs share the leader's level). 0 if not owned.</summary>
    public int GetLevel(SwordId sword)
    {
        if (!Owns(sword)) return 0;
        var idx = (int)SwordRegistry.GetDefinition(sword).LevelOwner;
        return idx < _swordLevels.Length ? _swordLevels[idx] : 0;
    }

    /// <summary>Sets the level (clamped 0..5) and notifies the sword's (and partners') behaviors.</summary>
    public async Task SetLevel(SwordId sword, int level)
    {
        var owner = SwordRegistry.GetDefinition(sword).LevelOwner;
        level = Math.Clamp(level, 0, SwordRegistry.MaxLevel);
        var old = GetLevel(owner);
        if (old == level) return;
        WriteLevel(owner, level);
        UpdateSwordList();
        foreach (var s in SwordRegistry.WithPartners(owner))
            await SwordRegistry.Get(s).OnLevelChanged(Owner, old, level);
    }

    private void WriteLevel(SwordId sword, int level)
    {
        var idx = (int)SwordRegistry.GetDefinition(sword).LevelOwner;
        var copy = new int[Math.Max(_swordLevels.Length, idx + 1)];
        Array.Copy(_swordLevels, copy, _swordLevels.Length);
        copy[idx] = level;
        SwordLevels = copy;
    }

    // =====================================================================================
    // Misc helpers for content
    // =====================================================================================

    /// <summary>Adds a curse (or any canonical card) to the deck with the standard preview.</summary>
    public async Task AddCurse(CardModel canonicalCurse)
    {
        if (canonicalCurse.Type == CardType.Curse)
        {
            await CardPileCmd.AddCursesToDeck([canonicalCurse], Owner);
            return;
        }

        var card = Owner.RunState.CreateCard(canonicalCurse, Owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck));
    }

    /// <summary>Per-run integer counter (saved). Use a unique key, e.g. "tyrfing.curses".</summary>
    public int GetRunCounter(string key)
    {
        foreach (var part in _runCounters.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0] == key && int.TryParse(kv[1], out var v)) return v;
        }

        return 0;
    }

    public void SetRunCounter(string key, int value)
    {
        if (key.Contains(';') || key.Contains('=')) throw new ArgumentException("key must not contain ';' or '='");
        var parts = _runCounters.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith(key + "=", StringComparison.Ordinal))
            .ToList();
        parts.Add($"{key}={value}");
        RunCounters = string.Join(';', parts);
    }

    public int AddRunCounter(string key, int delta)
    {
        var v = GetRunCounter(key) + delta;
        SetRunCounter(key, v);
        return v;
    }

    private void UpdateSwordList()
    {
        if (!IsMutable) return;
        if (DynamicVars[SwordListVar] is not StringVar sv) return;
        try
        {
            sv.StringValue = string.Join("\n",
                OwnedSwords.Select(s =>
                    $"- {SwordRegistry.DisplayName(s)} ({GetLevel(s)}/{SwordRegistry.MaxLevel})"));
        }
        catch (Exception)
        {
            sv.StringValue = string.Join("\n", OwnedSwords.Select(s => $"- {s}"));
        }

        InvokeDisplayAmountChanged();
    }

    // =====================================================================================
    // Run hooks: rewards / shop / rest site
    // =====================================================================================

    /// <summary>Common cards (Sword == null) and cards of owned swords; other pools untouched.</summary>
    public bool IsCardAllowed(CardModel card) => card is not MagicSwordCard { Sword: { } s } || Owns(s);

    public override async Task AfterObtained()
    {
        await base.AfterObtained();
        UpdateSwordList();
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        await base.AfterRoomEntered(room);
        UpdateSwordList();
    }

    /// <summary>
    /// Our filter delegates, so repeated calls on the same options object (CardFactory calls the hook once per
    /// generated card and WithCardPools mutates the options in place) do not wrap the filter again and again.
    /// </summary>
    private static readonly ConditionalWeakTable<Func<CardModel, bool>, object> OwnFilters = new();

    /// <summary>Below this many allowed non-basic cards the filter is skipped (CardFactory would throw).</summary>
    private const int MinFilteredRewardCards = 3;

    public override CardCreationOptions ModifyCardRewardCreationOptions(Player player, CardCreationOptions options)
    {
        if (player != Owner) return options;
        if (options.Flags.HasFlag(CardCreationFlags.NoCardPoolModifications)) return options;
        if (options.CustomCardPool != null) return options;
        if (options.CardPools.Count == 0) return options;
        var previous = options.CardPoolFilter;
        if (previous != null && OwnFilters.TryGetValue(previous, out _)) return options; // already filtered

        Func<CardModel, bool> filter = card => (previous == null || previous(card)) && IsCardAllowed(card);

        // Safety: CardFactory.CreateForReward throws when no valid card remains. While the mod has very few common
        // cards (early development) fall back to the unfiltered pool instead of crashing the reward screen.
        var allowed = options.CardPools
            .SelectMany(p => p.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
            .Where(filter)
            .Count(c => c.Rarity != CardRarity.Basic && c.Rarity != CardRarity.Ancient);
        if (allowed < MinFilteredRewardCards)
        {
            MainFile.Logger.Warn($"[Mangeomchong] only {allowed} allowed reward cards; sword filter skipped. " +
                                 "Add more common (Sword == null) cards.");
            return options;
        }

        OwnFilters.AddOrUpdate(filter, new object());
        return options.WithCardPools(options.CardPools.ToList(), filter);
    }

    public override IEnumerable<CardModel> ModifyMerchantCardPool(Player player, IEnumerable<CardModel> options)
    {
        if (player != Owner) return options;
        var all = options.ToList();
        var filtered = all.Where(IsCardAllowed).ToList();

        // Safety: CardFactory.CreateForMerchant throws if no card of a requested type remains. If filtering would
        // empty a type (e.g. no common Power card exists yet), keep that type's original cards.
        foreach (var type in new[] { CardType.Attack, CardType.Skill, CardType.Power })
        {
            var hadType = all.Any(c => c.Type == type && c.Rarity != CardRarity.Basic);
            var hasType = filtered.Any(c => c.Type == type && c.Rarity != CardRarity.Basic);
            if (hadType && !hasType) filtered.AddRange(all.Where(c => c.Type == type));
        }

        return filtered;
    }

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner) return false;
        options.Add(new SwordForgeRestSiteOption(player));
        return true;
    }

    // =====================================================================================
    // Combat hooks: sword state + owned-sword lifecycle
    // =====================================================================================

    public override Task BeforeCombatStart()
    {
        SwordCombat.Reset(Owner);
        return Task.CompletedTask;
    }

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player != Owner || Owner.PlayerCombatState is not { TurnNumber: 1 }) return;
        if (SwordCombat.Get(Owner) == null) SwordCombat.Reset(Owner);

        foreach (var sword in OwnedSwords)
        {
            if (SwordRegistry.GetDefinition(sword).EmergesAtCombatStart)
                SwordCombat.Emerge(Owner, sword); // Onimaru: present, not summoned, not current
        }

        foreach (var sword in OwnedSwords)
            await SwordRegistry.Get(sword).OnCombatStart(SwordCombat.ContextFor(Owner, sword), choiceContext);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner) return;
        foreach (var sword in OwnedSwords)
            await SwordRegistry.Get(sword).OnPlayerTurnStart(SwordCombat.ContextFor(Owner, sword), choiceContext);
    }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner.Creature)) return;
        foreach (var sword in OwnedSwords)
            await SwordRegistry.Get(sword).OnPlayerTurnEnd(SwordCombat.ContextFor(Owner, sword), choiceContext);
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        try
        {
            foreach (var sword in OwnedSwords)
                await SwordRegistry.Get(sword).OnCombatEnd(SwordCombat.ContextFor(Owner, sword), room);
        }
        finally
        {
            SwordCombat.Clear(Owner);
        }
    }

    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        if (card is not MagicSwordCard { Sword: { } sword } msc || !card.IsMutable || card.Owner != Owner)
            return false;
        return SwordRegistry.Get(sword).ModifyCardKeywords(SwordCombat.ContextFor(Owner, sword), msc, keywords);
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card is not MagicSwordCard { Sword: { } sword } msc || !card.IsMutable || card.Owner != Owner)
            return false;
        return SwordRegistry.Get(sword)
            .TryModifyEnergyCost(SwordCombat.ContextFor(Owner, sword), msc, originalCost, out modifiedCost);
    }

    // ---- Preview of a sword card in hand whose sword is not current yet (see SwordCombat.EffectiveSwordFor).
    // Playing such a card switches first, so its numbers must already show the NEW sword's effect. The visible
    // CurrentSwordPower skips exactly these card sources, so nothing is counted twice. (Also covers the very first
    // sword card of a combat, when there is no current sword and therefore no CurrentSwordPower yet.)

    private IEnumerable<(SwordBehavior Behavior, SwordContext Ctx)> PreviewEffects(CardModel? cardSource)
    {
        if (cardSource == null || !IsMutable) return [];
        var (sword, preview) = SwordCombat.EffectiveSwordFor(Owner, cardSource);
        if (sword is not { } s || !preview) return [];
        return SwordCombat.CurrentEffects(Owner, s, preview: true);
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        decimal sum = 0m;
        foreach (var (b, ctx) in PreviewEffects(cardSource))
            sum += b.ModifyDamageAdditive(ctx, target, amount, props, dealer, cardSource);
        return sum;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        decimal mult = 1m;
        foreach (var (b, ctx) in PreviewEffects(cardSource))
            mult *= b.ModifyDamageMultiplicative(ctx, target, amount, props, dealer, cardSource);
        return mult;
    }

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource,
        CardPlay? cardPlay)
    {
        decimal sum = 0m;
        foreach (var (b, ctx) in PreviewEffects(cardSource))
            sum += b.ModifyBlockAdditive(ctx, target, block, props, cardSource, cardPlay);
        return sum;
    }

    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        decimal mult = 1m;
        foreach (var (b, ctx) in PreviewEffects(cardSource))
            mult *= b.ModifyBlockMultiplicative(ctx, target, block, props, cardSource, cardPlay);
        return mult;
    }

    /// <summary>Spec §2: the first sword summon of each combat -> Block 3 + draw 1. Called by SwordCombat.Summon.</summary>
    internal async Task TriggerFirstSummonBonus(PlayerChoiceContext choiceContext)
    {
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}
