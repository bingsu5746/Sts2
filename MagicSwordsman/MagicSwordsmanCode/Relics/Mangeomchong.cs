using MegaCrit.Sts2.Core.Context;
using MagicSwordsman.MagicSwordsmanCode.Visuals;
using System.Runtime.CompilerServices;
using MagicSwordsman.MagicSwordsmanCode.Cards;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Dialogue;
using MagicSwordsman.MagicSwordsmanCode.RestSite;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
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
/// First sword (사용자 결정 2026-10-09): there is no fixed starting sword any more. When the run is created the relic
/// rolls one random sword out of all 10 (간장·막야 = one pair) — see RollStartingSword. The roll stays SEALED (only a
/// saved run counter, nothing owned, no cards) until the run-start roulette reveals it (AwakenFirstSword), so nothing
/// (deck, relic text, sword viewer, rewards at Neow) shows it before that (bug report 2026-10-16).
///
/// Effects:
///  - Card rewards and shops only offer common cards + cards of owned swords.
///  - Rest site: adds "마검 강화" (SwordForgeRestSiteOption).
///  - Combat: first sword summon of each combat -> Block 3 + draw 1 (once). Drives the per-combat sword state and
///    the owned-sword lifecycle hooks of every SwordBehavior.
/// </summary>
// Not sealed: OpenedMangeomchong (Ancient upgrade, Relics/OpenedMangeomchong.cs) derives from it so that every
// player.GetRelic<Mangeomchong>() keeps working after the upgrade. The [SavedProperty] setters are protected (not
// private) because the game sets saved properties by reflection on the runtime type, and .NET hides an inherited
// PRIVATE setter from a derived type's PropertyInfo.
public class Mangeomchong : MagicSwordsmanRelic
{
    private const string SwordListVar = "SwordList";

    // Empty until the run's random first sword is awakened (AwakenFirstSword, run-start roulette or fallback).
    private int[] _ownedSwordIds = [];
    private int[] _swordLevels = new int[16];
    private int[] _everOwnedIds = [];
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

    /// <summary>
    /// Ancient "Touch of Orobas" upgrade (content doc §9). BaseLib's StarterUpgradePatches calls this; the run data is
    /// copied into the new relic by Relics/OpenedMangeomchong.cs (Harmony prefix on RelicCmd.Replace).
    /// </summary>
    public override RelicModel? GetUpgradeReplacement() =>
        this is OpenedMangeomchong ? null : ModelDb.Relic<OpenedMangeomchong>();

    /// <summary>Counter on the relic icon: number of owned swords.</summary>
    public override int DisplayAmount => IsMutable ? _ownedSwordIds.Length : 0;

    // =====================================================================================
    // Saved properties
    // =====================================================================================

    [SavedProperty]
    public int[] OwnedSwordIds
    {
        get => _ownedSwordIds;
        protected set
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
        protected set
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
        protected set
        {
            AssertMutable();
            _everOwnedIds = value ?? [];
        }
    }

    [SavedProperty]
    public int ExtraSlots
    {
        get => _extraSlots;
        protected set
        {
            AssertMutable();
            _extraSlots = value;
        }
    }

    [SavedProperty]
    public List<SerializableCard> StoredCards
    {
        get => _storedCards;
        protected set
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
        protected set
        {
            AssertMutable();
            _runCounters = value ?? "";
            UpdateSwordList(); // the sealed first sword (a run counter) changes the description line
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

    /// <summary>Not owned yet, not the sealed first sword, and the sword + its partners fit into the free slots.</summary>
    public bool CanAcquire(SwordId sword)
    {
        var group = SwordRegistry.WithPartners(sword);
        if (group.Any(Owns) || group.Any(IsSealedFirstSword)) return false;
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
        if (!firstTime[sword]) SwordTalk.OnReacquired(Owner, sword); // back in the tomb: maybe a line (Dialogue)

        Flash();
        // First acquisition in the run: a short "new sword" card (presentation only, local player only).
        if (firstTime.Values.Any(f => f) && LocalContext.IsMe(Owner)) SwordAcquiredPopup.Show(sword);
        // 【조합】 cards this sword completes: "새 조합이 열렸다" (shown after the sword popup closes; presentation only)
        if (LocalContext.IsMe(Owner)) UnionUnlockedPopup.Show(this, group);
        MainFile.Logger.Info($"[Mangeomchong] acquired {string.Join(",", group)}");
        return true;
    }

    /// <summary>
    /// Removes the sword (and partners) from Mangeomchong and moves all of their cards from the deck into storage.
    /// The level is reset to 0 ([Claude] decision: a lost/shattered sword comes back un-upgraded).
    /// A forge shatter calls this for every sword, Gram included (its protection was removed, 사용자 결정 2026-10-09).
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
        SwordTalk.OnReleased(Owner, sword); // farewell line (Dialogue/SwordTalk.cs)
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
        await SwordMastery.AfterLevelChanged(this, owner, old, level); // level-5 reward (Swords/SwordMastery.cs)
        if (level > old) SwordTalk.OnUpgraded(Owner, owner); // 친밀도 + maybe a line (Dialogue/SwordTalk.cs)
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
        if (LocalContext.IsMe(Owner)) Visuals.Sfx.Curse();
        SwordTalk.OnCursed(Owner, canonicalCurse); // the sword that cursed him may speak (Dialogue/SwordTalk.cs)
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
            // Short description (사용자 요청 2026-10-04): the owned-sword list only appears once there is one, on its own lines.
            var lines = OwnedSwords.Select(s =>
                $"- {SwordRegistry.DisplayName(s)} ({GetLevel(s)}/{SwordRegistry.MaxLevel})").ToList();
            // The run's first sword before the run-start roulette: a sealed line that never names it.
            if (SealedFirstSword != null)
            {
                var sealedLine = MegaCrit.Sts2.Core.Localization.LocString.GetIfExists(Events.SwordLore.Table,
                    Events.SwordLore.Prefix + "." + SealedLineKey);
                lines.Add("- " + (sealedLine?.GetFormattedText() ?? "???"));
            }
            sv.StringValue = lines.Count == 0 ? "" : "\n" + string.Join("\n", lines);
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

    /// <summary>
    /// Common cards (Sword == null) and cards of owned swords; other pools untouched. 【조합】 cards also need their
    /// second sword (Cards/Union/UnionCard.cs).
    /// </summary>
    public bool IsCardAllowed(CardModel card) =>
        (card is not MagicSwordCard { Sword: { } s } || Owns(s)) && Cards.Union.UnionCatalog.IsAllowed(this, card);

    public override async Task AfterObtained()
    {
        await base.AfterObtained();
        // New run: RunManager.FinalizeStartingRelics calls this for every player's starting relics on every client.
        // Not on load (the saved properties already hold the swords / the sealed roll), and not after the Ancient
        // upgrade (OpenedMangeomchong copies the run data, RunCounters included, before RelicCmd.Replace obtains it).
        if (_ownedSwordIds.Length == 0 && _everOwnedIds.Length == 0 && StartingSword == null && SealedFirstSword == null)
            RollStartingSword();
        UpdateSwordList();
    }

    /// <summary>
    /// Run counter: the run's random first sword as (int)SwordId + 1, set when it is AWAKENED (granted)
    /// (0 = not awakened yet / run from before 2026-10-09).
    /// </summary>
    public const string StartingSwordCounter = "start.first_sword";

    /// <summary>
    /// Run counter: the seeded first-sword roll as (int)SwordId + 1 while it is still sealed (0 = nothing sealed).
    /// Saved with the relic (RunCounters). Never shown anywhere; cleared by <see cref="AwakenFirstSword"/>.
    /// </summary>
    public const string SealedFirstSwordCounter = "start.first_sword_sealed";

    /// <summary>SwordLore generic key of the relic description line for the sealed first sword.</summary>
    private const string SealedLineKey = "SEALED_FIRST_SWORD";

    /// <summary>The run's random first sword (pair leader) once awakened, or null (sealed / runs before the rule).</summary>
    public SwordId? StartingSword =>
        GetRunCounter(StartingSwordCounter) is var v and > 0 ? (SwordId)(v - 1) : null;

    /// <summary>
    /// The rolled first sword (pair leader) while it is still sealed, else null. Only the run-start event
    /// (TombResonanceEvent, roulette target) may read it; UI must not show it.
    /// </summary>
    public SwordId? SealedFirstSword =>
        GetRunCounter(SealedFirstSwordCounter) is var v and > 0 ? (SwordId)(v - 1) : null;

    /// <summary>True for the sealed first sword and its partner (reserved: never offered while sealed).</summary>
    public bool IsSealedFirstSword(SwordId sword) =>
        SealedFirstSword is { } sealedSword && SwordRegistry.WithPartners(sealedSword).Contains(sword);

    /// <summary>
    /// 사용자 결정 2026-10-09: the first sword is no longer Gram but one of all 10 swords (간장·막야 = one pair) at random.
    /// Seeded: a named Rng on the player's own seed (run seed + player slot), so the roll is fixed for a seed, differs
    /// between the players of a multiplayer run, is identical on every client, and advances no game Rng.
    /// Bug report 2026-10-16: granting it here (deck cards + owned sword) showed it in the deck view / relic text while
    /// Neow's gift was still being chosen. So only the roll is stored here (sealed, saved run counter); the sword and its
    /// StarterCards are granted by <see cref="AwakenFirstSword"/> at the run-start roulette (or the fallback before the
    /// first other room, see <see cref="BeforeRoomEntered"/>).
    /// </summary>
    private void RollStartingSword()
    {
        var candidates = SwordRegistry.StartingSwordCandidates();
        if (candidates.Count == 0) return;
        var rng = new MegaCrit.Sts2.Core.Random.Rng(Owner.PlayerRng.Seed, "magicswordsman_starting_sword");
        var sword = candidates[rng.NextInt(candidates.Count)];
        SetRunCounter(SealedFirstSwordCounter, (int)SwordRegistry.GroupLeader(sword) + 1);
        MainFile.Logger.Info("[Mangeomchong] starting sword rolled (sealed until the run-start reveal)");
    }

    /// <summary>
    /// Grants the sealed first sword: owned + ever owned, its StarterCards (2 per sword or pair) into the deck, and
    /// OnAcquiredAsStartingSword (the run-level part of OnAcquired; the 간장·막야 Max HP cost is waived for the random
    /// first sword, 사용자 결정 2026-10-09). No slot check (it is the first sword), no SwordCardPick (offered by the
    /// run-start pick, TombResonanceEvent.OfferStartCardPicks), no popup (the caller decides). Uses no Rng and runs on
    /// every client in the same order (event option / room hooks), so multiplayer stays in sync.
    /// Returns the sword, or null when nothing was sealed (already awakened, or a run from before this rule).
    /// </summary>
    public async Task<SwordId?> AwakenFirstSword(bool previewCards)
    {
        if (SealedFirstSword is not { } sword) return null;
        SetRunCounter(SealedFirstSwordCounter, 0); // first: a re-entrant call finds nothing sealed
        var group = SwordRegistry.WithPartners(sword);
        SetRunCounter(StartingSwordCounter, (int)sword + 1);
        if (group.Any(Owns))
        {
            UpdateSwordList();
            return sword;
        }

        var firstTime = group.Where(s => !EverOwned(s)).ToList();
        OwnedSwordIds = _ownedSwordIds.Concat(group.Select(s => (int)s)).Distinct().ToArray();
        EverOwnedIds = _everOwnedIds.Concat(group.Select(s => (int)s)).Distinct().ToArray();

        var results = new List<CardPileAddResult>();
        foreach (var s in firstTime)
        {
            foreach (var canonical in SwordRegistry.Get(s).StarterCards)
            {
                var card = Owner.RunState.CreateCard(canonical, Owner);
                results.Add(await CardPileCmd.Add(card, PileType.Deck));
            }
        }

        if (previewCards && results.Count > 0) CardCmd.PreviewCardPileAdd(results);
        foreach (var s in group) SwordRegistry.Get(s).OnAcquiredAsStartingSword(Owner);
        UpdateSwordList();
        Flash();
        MainFile.Logger.Info($"[Mangeomchong] first sword awakened: {string.Join(",", group)}");
        return sword;
    }

    /// <summary>
    /// Fallback for the sealed first sword: the run-start pick (TombResonanceEvent, act 0) normally awakens it. If any
    /// other room is entered first (a combat, shop, rest site, ? event, or anything after act 0 — e.g. the pick was
    /// skipped or lost), the sword is granted right before that room starts, so the run is never swordless. Combat rooms
    /// set up the fight in room.Enter, which runs after this hook (RunManager.EnterRoomInternal). The Ancient (Neow)
    /// room and the run-start pick itself are exempt. Not called for the map room or for pre-finished rooms on load.
    /// The first sword's 1-of-3 card pick is NOT offered here (a room hook has no synced player-choice context, so it is
    /// not multiplayer-safe); TombResonanceEvent.OfferPendingFirstSwordPick offers it at the next 만검총 event (after the
    /// act 1 boss) instead, guarded by TombResonanceEvent.FirstSwordPickKey.
    /// </summary>
    public override async Task BeforeRoomEntered(AbstractRoom room)
    {
        await base.BeforeRoomEntered(room);
        if (SealedFirstSword == null || IsRunStartRoom(room)) return;
        await AwakenWithoutRoulette();
    }

    private bool IsRunStartRoom(AbstractRoom room) =>
        Owner.RunState.CurrentActIndex == 0 &&
        room is EventRoom { CanonicalEvent: AncientEventModel or Events.TombResonanceEvent };

    private async Task AwakenWithoutRoulette()
    {
        if (await AwakenFirstSword(previewCards: false) is { } sword && LocalContext.IsMe(Owner))
            SwordAcquiredPopup.Show(sword, "POPUP_HEADER_FIRST");
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        await base.AfterRoomEntered(room);
        UpdateSwordList();
        SwordTalk.OnRoomEntered(Owner, room);
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
        // NoCardPoolModifications is NOT a reason to skip: that flag stops relics from adding/swapping pools
        // (PrismaticGem, DingyRug), while this filter only removes unowned swords' cards. SeaGlass,
        // TheFutureOfPotions and InfestedAutomaton build rewards from the owner's own pool with that flag and must
        // still respect spec §2 [확정] (rewards = common cards + owned swords' cards only).
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

    // Shop sword offer "칼집째 놓인 검" (spec §5 [확정], content doc §6.5) — rules in SwordShopOffer.
    public override void ModifyMerchantCardCreationResults(Player player, List<CardCreationResult> cards) =>
        SwordShopOffer.ModifyCreationResults(this, player, cards);

    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal cost) =>
        SwordShopOffer.ModifyPrice(this, player, entry, cost);

    public override Task AfterItemPurchased(Player player, MerchantEntry itemPurchased, int goldSpent) =>
        SwordShopOffer.AfterPurchased(this, player, itemPurchased, goldSpent);

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner) return false;
        if (player.GetRelic<CooledFurnace>() != null) return false; // 식은 용광로: no 마검 강화 at rest sites
        options.Add(new SwordForgeRestSiteOption(player));
        return true;
    }

    // =====================================================================================
    // Combat hooks: sword state + owned-sword lifecycle
    // =====================================================================================

    public override async Task BeforeCombatStart()
    {
        // Last-resort fallback: never fight with the first sword sealed. Should be unreachable — every combat room is
        // entered through RunManager.EnterRoomInternal, which runs Hook.BeforeRoomEntered (our fallback) before
        // room.Enter, and the only exempt rooms (Neow, the run-start TombResonanceEvent) start no fight (verified in the
        // decompiled RunManager, 2026-10-16). If it ever happens, the starter cards go to the DECK only (this fight's
        // piles were already built from the deck); they are deliberately not put into the draw pile (a combat copy +
        // pile position here is untested), so this is only logged (review fix 2026-10-16).
        if (SealedFirstSword != null)
        {
            MainFile.Logger.Warn("[Mangeomchong] first sword still sealed at combat start: awakened now; its starter " +
                                 "cards join the deck from the next combat on");
            await AwakenWithoutRoulette();
        }
        SwordCombat.Reset(Owner);
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
        SwordTalk.OnCombatStart(Owner);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner) return;
        SwordVisuals.Sync(Owner); // presentation only (creates the rig once the combat room exists, e.g. Onimaru)
        SwordRulesHint.OnTurnStart(Owner); // presentation only: first-combat rules hint, once per run, local player
        MotionDirector.OnTurnStart(Owner);
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
            SwordTalk.SnapshotCombat(Owner); // which swords were out, for the victory affinity gain
            SwordCombat.Clear(Owner);
        }
    }

    // ---- presentation only: which combat motion Ensifer plays (MotionDirector)

    /// <summary>A guard motion whenever the owner gains Block in combat.</summary>
    public override Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
    {
        if (amount > 0 && creature == Owner.Creature) MotionDirector.OnBlockGained(Owner, amount);
        return Task.CompletedTask;
    }

    /// <summary>Skills / powers get a casting motion; attacks remember the card for the attack variant.</summary>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner && cardPlay.IsFirstInSeries) MotionDirector.OnCardPlayed(Owner, cardPlay);
        return Task.CompletedTask;
    }

    /// <summary>A hit reaction sized to the damage taken (or a guarded flinch when Block took all of it).</summary>
    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner.Creature && result.UnblockedDamage > 0) SwordTalk.OnOwnerDamaged(Owner);
        if (target == Owner.Creature && dealer != target && !props.HasFlag(ValueProp.SkipHurtAnim))
            MotionDirector.OnDamageReceived(Owner, result.UnblockedDamage, result.WasFullyBlocked, dealer,
                result.BlockedDamage, props.HasFlag(ValueProp.Move));
        else if (dealer == Owner.Creature && target != Owner.Creature && props.HasFlag(ValueProp.Move))
            MotionDirector.OnDealtDamage(Owner, target);
        return Task.CompletedTask;
    }

    /// <summary>Presentation only: the boss-kill cut-in when the boss fight's last enemy falls (BossKillCutIn).</summary>
    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented,
        float deathAnimLength)
    {
        BossKillCutIn.OnDeath(Owner, creature, wasRemovalPrevented);
        return Task.CompletedTask;
    }

    public override Task AfterCombatVictory(CombatRoom room)
    {
        if (!Owner.Creature.IsDead) MotionDirector.OnVictory(Owner);
        SwordTalk.OnVictory(Owner, room); // 친밀도 for the swords that were out + maybe a line
        return Task.CompletedTask;
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
