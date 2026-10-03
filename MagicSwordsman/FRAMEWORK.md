# 마검사 (MagicSwordsman) — Core Framework Guide

This file is for **content agents** (people/agents adding swords, cards, curses, events, relics, potions) and for the
**integrator**. It describes the framework that already exists, its public API, the rules you must follow and what is
verified vs. assumed.

- Spec (wins over everything): `docs/magic-swordsman-spec.md` ([확정] = user-confirmed, never change).
- Lore/flavor about a sword must come from `docs/magic-sword-research.docx`
  (plain text copy: `/tmp/claude-0/s.txt`). Never invent historical or mythological facts.
- Game API ground truth: decompiled game `/tmp/claude-0/src`, BaseLib 3.4.7 `/tmp/claude-0/baselib-src`.
  Grep them for every class/method/hook you use. Do not guess APIs.
- Build: `/tmp/claude-0/msbuild.sh <unique-tag>` must print `Build succeeded`.
- Mod id `MagicSwordsman`, namespace root `MagicSwordsman.MagicSwordsmanCode`, author bingsu5746.

---

## 1. Architecture in one picture

```
                    run level (saved)                                  combat level (not saved)
 ┌──────────────────────────────────────────────┐        ┌──────────────────────────────────────────────┐
 │ Mangeomchong (starter relic, [SavedProperty])│        │ SwordCombatState (per player, per combat)    │
 │  owned swords, levels, stored cards,         │        │  Current, Previous, Present, Summoned,       │
 │  extra slots, run counters                   │        │  FirstSummonBonusUsed, KusanagiSwitchIns,    │
 │  reward/shop filter, rest-site option,       │        │  ReturnedToVault, per-sword counters         │
 │  drives owned-sword lifecycle hooks          │        └───────────────▲──────────────────────────────┘
 └──────────────┬───────────────────────────────┘                        │ all mutations
                │                                          ┌─────────────┴──────────────┐
                ▼                                          │ SwordCombat (static API)   │
 ┌──────────────────────────────┐   lookup                 │  SwitchTo / Summon / Emerge│
 │ SwordRegistry                │◄─────────────────────────│  OnSwordCardPlayed ...     │
 │  SwordId -> SwordDefinition  │                          └─────────────▲──────────────┘
 │           -> SwordBehavior   │                                        │ before the card's effect
 └──────────────┬───────────────┘                          ┌─────────────┴──────────────┐
                │ one stateless instance per sword         │ MagicSwordCard (all cards) │
                ▼                                          │  Sword (null = common)     │
 ┌──────────────────────────────┐   delegates hooks        └────────────────────────────┘
 │ SwordBehavior subclasses     │◄──────── CurrentSwordPower (visible "현재 검" buff; current sword
 │ Swords/Behaviors/*.cs        │          + Kusanagi inheritance)
 └──────────────────────────────┘◄──────── Mangeomchong (preview of a not-yet-current sword card in hand,
                                           and the owned-sword lifecycle: combat start, turn start/end...)
```

Key ideas:

- **One behavior object per sword** (`SwordBehavior` subclass). It is a stateless singleton: keep combat state in
  `ctx` counters (`SwordCombatState`) and run state in Mangeomchong run counters. Never store fields per player.
- **The current sword's effect is applied through a real game Power** (`CurrentSwordPower`) on the player. The game
  calls power hooks (`ModifyDamageAdditive`, `AfterCardPlayed`, ...) and the power forwards them to the current
  sword's behavior (and, for Kusanagi, to the inherited sword with `ctx.IsInherited = true`). The player sees which
  sword is current and its level (icon number = level).
- **Hand preview is exact**: a sword card in hand whose sword is not current yet shows its numbers as if its sword
  were current (because playing it switches first). Mangeomchong does this preview; the power skips those card
  sources, so nothing stacks (`SwordCombat.EffectiveSwordFor`).
- **Mangeomchong** is the single owner of all run data (owned swords, levels, stored cards) and the only thing that
  is saved.

### Folder layout and ownership

| Path | Owner | Notes |
|---|---|---|
| `MagicSwordsmanCode/MainFile.cs` | framework | calls `SwordRegistry.AutoRegister` |
| `MagicSwordsmanCode/Swords/` (`SwordId`, `SwordBehavior`, `SwordContext`, `SwordDefinition`, `SwordRegistry`, `SwordAcquisition`) | framework | do not edit; ask the integrator |
| `MagicSwordsmanCode/Swords/Behaviors/<Sword>Behavior.cs` | **content agent of that sword** | replace the stub body; keep the class name; keep the lines marked FRAMEWORK PARTS (Kusanagi, Skofnung) |
| `MagicSwordsmanCode/Cards/<Sword>/` (e.g. `Cards/Gram/`, `Cards/GanjiangMoye/`, `Cards/ClaiomhSolais/`) | content agent of that sword | sword cards |
| `MagicSwordsmanCode/Cards/Common/` | common-card agent | common cards (`Sword == null`) |
| `MagicSwordsmanCode/Cards/Basic/` | framework | starting deck (Strike, Defend, BrokenBlade, SwordSwap) |
| `MagicSwordsmanCode/Cards/Tokens/` | framework | selection tokens (sword tokens, forge tokens) |
| `MagicSwordsmanCode/Curses/` | content agents | one file per curse, `<Sword>...Curse.cs`, derive from `SwordCurseCard` |
| `MagicSwordsmanCode/Powers/` | content agents may add files | `CurrentSwordPower.cs`, `MagicSwordsmanPower.cs` are framework |
| `MagicSwordsmanCode/Events/` | event agents | acquisition events, forge story events |
| `MagicSwordsmanCode/Relics/` | relic agents may add files | `Mangeomchong.cs`, `MagicSwordsmanRelic.cs` are framework |
| `MagicSwordsmanCode/Potions/` | potion agents | derive from `MagicSwordsmanPotion` |
| `MagicSwordsmanCode/Combat/`, `RestSite/`, `Character/` | framework | |
| `MagicSwordsman/localization/**` | **integrator only** | content agents write fragments instead (section 7) |
| `loc_fragments/<group>/<kor\|eng>/*.json` | content agents | merged by `tools/merge_loc_fragments.py` |
| `MagicSwordsman/images/**` | art | placeholder images are used automatically when a file is missing |

If you need a framework change (new hook, new state field), do not edit framework files in parallel with other
agents: write a `TODO(framework): ...` comment in your own file and report it.

---

## 2. Rules implemented by the framework (spec §2–§4)

Combat (spec §3):
1. Combat start: no current sword, nothing present (`Mangeomchong.BeforeCombatStart` -> `SwordCombat.Reset`).
2. Turn 1, before the opening hand: swords with `EmergesAtCombatStart` (Onimaru) **emerge**: present, not summoned,
   not current, no Mangeomchong bonus. Then `OnCombatStart` runs for every owned sword.
3. Playing a sword card: `SwordCombat.OnSwordCardPlayed` runs **before** the card's own effect:
   - if the sword is not present yet it is **summoned** (once per sword per combat) -> `OnSummoned`;
     the first summon of the combat triggers Mangeomchong: Block 3 + draw 1 (once);
   - the sword becomes **current** ("C 방식"). If `CanBecomeCurrent` refuses (Durandal HP <= 30%, Skofnung turn 1,
     Kusanagi used up / returned to the vault, sword not owned), the card still resolves and the current sword
     stays.
4. `검 바꾸기` (SwordSwap): choose an owned sword other than the current one (only swords that may switch now are
   offered) -> same `SwitchTo` path with `SwitchReason.SwapEffect`.
5. Kusanagi: at most 2 switch-ins from another sword per combat (`KusanagiSwitchIns`). When it leaves current after
   the 2nd one it goes back to the vault (`ReturnedToVault`): its cards get Exhaust + Ethereal and no longer switch.

Run level (spec §2, §4):
- Slots: base 3 (`SwordRegistry.BaseSlotCapacity`) + `Mangeomchong.ExtraSlots`. Ganjiang and Moye are acquired and
  lost together and cost 1 slot each (= 2). Moye shares Ganjiang's level (`LevelOwner`).
- Card rewards (every `CardFactory.CreateForReward`) and shops only offer common cards (`Sword == null`) and cards of
  owned swords. Safety valves: if fewer than 3 allowed non-basic reward cards exist the reward filter is skipped
  (warning in the log); if the shop filter would remove every card of a type, that type is left unfiltered. So the
  common card pool must eventually contain enough cards of every type/rarity.
- Losing a sword moves all of its cards from the deck into Mangeomchong storage (`StoredCards`, pattern: game relic
  `PaelsTooth`) and resets its level to 0 ([Claude]); re-acquiring restores them. Starter cards are only granted the
  first time a sword is ever owned in the run.
- Rest site "마검 강화" (`SwordForgeRestSiteOption`): pick sword -> pick 안전 강화 (+1) / 도박 +2 (65/32/3) /
  도박 +3 (40/52/8); gambles that would exceed level 5 are not offered. Failure: 50:50 the sword's
  `FailureCurse` or level -1 (level 0 -> curse only; no curse defined -> level -1, or nothing at 0). Shatter: lose the
  sword (cards stored), except Gram (`CanBeLost = false`) -> level 0. RNG: `Owner.RunState.Rng.Niche`.

---

## 3. Public API

All signatures below are exact (copied from the code). Namespaces:
`MagicSwordsman.MagicSwordsmanCode.{Swords, Swords.Behaviors, Combat, Cards, Curses, Relics, RestSite, Powers}`.

### 3.1 `SwordId` / `SwitchReason` (Swords/SwordId.cs)

```csharp
public enum SwordId { Gram = 0, Ganjiang = 1, Moye = 2, Kusanagi = 3, Tyrfing = 4, Dainsleif = 5,
                      Durandal = 6, Skofnung = 7, Onimaru = 8, ClaiomhSolais = 9, Caladbolg = 10 }
public enum SwitchReason { CardPlayed, SwapEffect, Other }
```
Numbers are written into saves: never renumber; add new swords at the end.

### 3.2 `SwordDefinition` (framework-owned static facts)

```csharp
public required SwordId Id { get; init; }
public int SlotCost { get; init; } = 1;
public IReadOnlyList<SwordId> Partners { get; init; } = [];   // Ganjiang <-> Moye
public SwordId? LevelOwnerOverride { get; init; }              // Moye -> Ganjiang
public SwordId LevelOwner { get; }
public bool CanBeLost { get; init; } = true;                   // Gram: false
public bool EmergesAtCombatStart { get; init; }                // Onimaru: true
public bool OfferedByAcquisition { get; init; } = true;        // Gram, Moye: false
public bool IsPairLeader { get; }
```

### 3.3 `SwordRegistry`

```csharp
public const int MaxLevel = 5;
public const int BaseSlotCapacity = 3;
public static IEnumerable<SwordId> AllSwords { get; }
public static SwordDefinition GetDefinition(SwordId id);
public static SwordBehavior Get(SwordId id);                   // PlaceholderSwordBehavior if none registered
public static bool HasRealBehavior(SwordId id);
public static void Register(SwordBehavior behavior);           // normally not needed
public static void AutoRegister(Assembly assembly);            // MainFile: every concrete SwordBehavior
public static IReadOnlyList<SwordId> WithPartners(SwordId id); // Ganjiang -> [Ganjiang, Moye]
public static int GroupSlotCost(SwordId id);
public static SwordId GroupLeader(SwordId id);                 // Moye -> Ganjiang
public static string DisplayName(SwordId id);                  // localized, from the sword token card title
public static List<SwordId> RollAcquisitionCandidates(Player player, int count, Rng rng);
public static CardModel CanonicalCard(Type cardType);
```

### 3.4 `SwordBehavior` (abstract; one subclass per sword)

```csharp
public abstract SwordId Id { get; }
public SwordDefinition Definition { get; }

// 1. run level
public virtual IEnumerable<CardModel> StarterCards => [];      // canonical cards, granted on FIRST acquisition (spec: 2)
public virtual CardModel? FailureCurse => null;                // canonical curse for forge failures
public virtual Task OnAcquired(Player player, bool firstTime);
public virtual Task OnLost(Player player);
public virtual Task OnLevelChanged(Player player, int oldLevel, int newLevel);

// 2a. switching
public virtual bool CanBecomeCurrent(SwordContext ctx, SwitchReason reason) => true;
public virtual bool CanPlayCard(SwordContext ctx, MagicSwordCard card) => true;
public virtual Task OnSummoned(SwordContext ctx, PlayerChoiceContext choiceContext);
public virtual Task OnBecomeCurrent(SwordContext ctx, SwordId? previous, PlayerChoiceContext choiceContext);
public virtual Task OnLeaveCurrent(SwordContext ctx, SwordId next, PlayerChoiceContext choiceContext);
public virtual Task OnAnySwordSwitched(SwordContext ctx, SwordId? from, SwordId to, PlayerChoiceContext choiceContext);

// 2b. owned-sword lifecycle (called for EVERY owned sword, current or not — check ctx.IsCurrent / ctx.IsPresent)
public virtual Task OnCombatStart(SwordContext ctx, PlayerChoiceContext choiceContext);     // turn 1, before hand draw
public virtual Task OnPlayerTurnStart(SwordContext ctx, PlayerChoiceContext choiceContext); // AfterPlayerTurnStart
public virtual Task OnPlayerTurnEnd(SwordContext ctx, PlayerChoiceContext choiceContext);   // BeforeSideTurnEnd (player side)
public virtual Task OnCombatEnd(SwordContext ctx, CombatRoom room);                         // state still readable
public virtual bool ModifyCardKeywords(SwordContext ctx, MagicSwordCard card, ISet<CardKeyword> keywords); // own cards only
public virtual bool TryModifyEnergyCost(SwordContext ctx, MagicSwordCard card, decimal originalCost, out decimal modifiedCost);

// 3. current-sword effect (only while current, or inherited by Kusanagi with ctx.IsInherited)
public virtual SwordId? GetInheritedSword(SwordContext ctx) => null;   // Kusanagi only
public virtual bool CanBeInherited => true;
public virtual decimal ModifyDamageAdditive(SwordContext ctx, Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) => 0m;
public virtual decimal ModifyDamageMultiplicative(SwordContext ctx, Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) => 1m;
public virtual decimal ModifyBlockAdditive(SwordContext ctx, Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay) => 0m;
public virtual decimal ModifyBlockMultiplicative(SwordContext ctx, Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay) => 1m;
public virtual int ModifyAttackHitCount(SwordContext ctx, AttackCommand attack, int hitCount) => hitCount;
public virtual (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(SwordContext ctx, CardModel card, bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position);
public virtual Task BeforeCardPlayed(SwordContext ctx, CardPlay cardPlay);
public virtual Task AfterCardPlayed(SwordContext ctx, PlayerChoiceContext choiceContext, CardPlay cardPlay);
public virtual Task BeforeAttack(SwordContext ctx, AttackCommand command);
public virtual Task AfterAttack(SwordContext ctx, PlayerChoiceContext choiceContext, AttackCommand command);
public virtual Task AfterDamageGiven(SwordContext ctx, PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource);
public virtual Task AfterDamageReceived(SwordContext ctx, PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource);
```

The group-3 hooks mirror the game's `AbstractModel` hooks one-to-one (same arguments), so read real game powers for
semantics (`StrengthPower`, `DexterityPower`, `VigorPower`, ...). They fire for **every creature's** damage/block in
the fight: always filter on `dealer == ctx.Creature` / `target == ctx.Creature`. The four damage/block Modify hooks
are also called for the hand preview (`ctx.IsPreview`): they must be pure (no state changes, no randomness).

Need another game hook forwarded (e.g. `AfterTurnEnd`, `ModifyHpLost...`)? Ask the integrator: it is a 3-line addition
in `SwordBehavior` + `CurrentSwordPower`.

### 3.5 `SwordContext` (argument of every behavior hook)

```csharp
public required Player Player { get; init; }
public required SwordId Sword { get; init; }        // for an inherited call: the ORIGINAL sword (e.g. Gram)
public required int Level { get; init; }            // 0..5 saved level of Sword
public bool IsInherited { get; init; }              // Kusanagi runs this sword's effect at half strength
public bool IsCurrent { get; init; }
public bool IsPresent { get; init; }
public SwordCombatState? Combat { get; init; }      // null outside combat
public bool IsPreview { get; init; }                // hand preview of a not-yet-current sword card
public SwordId? PreviousSword { get; init; }        // sword current before this one (preview: the current one)
public Creature Creature { get; }
public Mangeomchong? Relic { get; }
public int TurnNumber { get; }                      // 1 on the first turn, 0 outside combat
public int Scale(int full);                         // inherited: half, rounded down, min 1 (0 stays 0)
public decimal Scale(decimal full);
public int GetCounter(string key);                  // per-combat, namespaced to this sword
public void SetCounter(string key, int value);
public int AddCounter(string key, int delta);
public SwordContext AsInherited();
```

### 3.6 `SwordCombat` (static, Combat/SwordCombat.cs)

```csharp
public static SwordCombatState? Get(Player player);              // null outside combat
public static SwordCombatState? GetOrCreate(Player player);
public static SwordId? CurrentSword(Player player);
public static bool IsCurrent(Player player, SwordId sword);
public static bool IsPresent(Player player, SwordId sword);
public static int LevelOf(Player player, SwordId sword);         // 0 if not owned
public static SwordContext ContextFor(Player player, SwordId sword);
public static SwordContext PreviewContextFor(Player player, SwordId sword);
public static (SwordId? Sword, bool IsPreview) EffectiveSwordFor(Player player, CardModel? cardSource);
public static IEnumerable<(SwordBehavior Behavior, SwordContext Ctx)> CurrentEffects(Player player, SwordId sword, bool preview);
public static bool CanSwitchTo(Player player, SwordId sword, SwitchReason reason);
public static IReadOnlyList<SwordId> SwitchCandidates(Player player);
public static bool CanPlaySwordCard(MagicSwordCard card);
public static Task OnSwordCardPlayed(MagicSwordCard card, PlayerChoiceContext choiceContext);  // called by MagicSwordCard
public static Task<bool> SwitchTo(Player player, SwordId sword, PlayerChoiceContext choiceContext, SwitchReason reason);
public static Task<bool> Summon(Player player, SwordId sword, PlayerChoiceContext choiceContext); // without switching
public static void Emerge(Player player, SwordId sword);         // present without summon (Onimaru)
public static Task ReturnToVault(Player player, SwordId sword, PlayerChoiceContext choiceContext);
public static Task EnsureCurrentSwordPower(Player player, PlayerChoiceContext choiceContext);
public static List<ISwordListener> Listeners(Player player);

public interface ISwordListener   // implement on a power, relic or card in a combat pile
{
    Task AfterSwordSwitched(Player player, SwordId? from, SwordId to, PlayerChoiceContext choiceContext) => Task.CompletedTask;
    Task AfterSwordSummoned(Player player, SwordId sword, PlayerChoiceContext choiceContext) => Task.CompletedTask;
}
```

`SwitchTo` order: summon if needed (`OnSummoned`, first-summon bonus, `AfterSwordSummoned`) -> old
`OnLeaveCurrent` -> state update (`Previous`, `Current`, `SwitchCount`, `KusanagiSwitchIns`) -> power refresh ->
new `OnBecomeCurrent` -> `OnAnySwordSwitched` on every owned sword -> `ISwordListener.AfterSwordSwitched`.

### 3.7 `SwordCombatState`

```csharp
public SwordId? Current { get; internal set; }
public SwordId? Previous { get; internal set; }
public HashSet<SwordId> Present { get; }
public HashSet<SwordId> Summoned { get; }
public bool FirstSummonBonusUsed { get; internal set; }
public int KusanagiSwitchIns { get; internal set; }
public int SwitchCount { get; internal set; }
public HashSet<SwordId> ReturnedToVault { get; }
public int GetCounter(SwordId sword, string key);
public void SetCounter(SwordId sword, string key, int value);
public int AddCounter(SwordId sword, string key, int delta);
```
Not saved: the game restarts a combat from the beginning when a run is loaded.

### 3.8 `MagicSwordCard` (base of every 마검사 card; Cards/MagicSwordCard.cs)

```csharp
[Pool(typeof(MagicSwordsmanCardPool))]
public abstract class MagicSwordCard(int cost, CardType type, CardRarity rarity, TargetType target,
    bool showInCardLibrary = true, bool autoAdd = true) : ConstructedCardModel(...)
public virtual SwordId? Sword => null;                  // override in sword cards
public bool IsSwordCard { get; }
public override int MaxUpgradeLevel { get; }            // 0 for sword cards (spec §4), base value for common cards
public int SwordLevel { get; }                          // owner's level of Sword (0 for common/canonical cards)
protected int Scaled(int baseValue, int perLevel);      // baseValue + perLevel * SwordLevel
protected MagicSwordCard WithDamagePerLevel(int perLevel); // this card's powered attack damage +N per level
protected MagicSwordCard WithBlockPerLevel(int perLevel);  // this card's block +N per level
protected MagicSwordCard WithLevelVar(string name, int baseValue, int perLevel); // text var {name}
protected int LevelVar(string name);                    // value of a WithLevelVar number at play time
public int DamagePerLevel { get; }
public int BlockPerLevel { get; }
protected virtual Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay); // YOUR effect
protected virtual bool IsPlayableExtra => true;          // extra playability condition
// OnPlay is sealed: it calls SwordCombat.OnSwordCardPlayed, then OnCardPlay.
// IsPlayable is overridden: SwordBehavior.CanPlayCard (e.g. Skofnung turn 1) && IsPlayableExtra.
// Description args added to every card: {SwordLevel}, {SwordName}, {IsSwordCard}.
```
All BaseLib `ConstructedCardModel` builders work as usual: `WithDamage`, `WithBlock`, `WithCards`, `WithPower<T>`,
`WithKeywords`, `WithTags`, `WithVar`, `WithCalculatedVar`, ... (see `/tmp/claude-0/baselib-src/BaseLib.Abstracts/ConstructedCardModel.cs`).

### 3.9 `Mangeomchong` (Relics/Mangeomchong.cs) — get it with `player.GetRelic<Mangeomchong>()`

```csharp
// saved ([SavedProperty]; setters private)
public int[] OwnedSwordIds { get; }  public int[] SwordLevels { get; }  public int[] EverOwnedIds { get; }
public int ExtraSlots { get; }       public List<SerializableCard> StoredCards { get; }  public string RunCounters { get; }

public IReadOnlyList<SwordId> OwnedSwords { get; }
public bool Owns(SwordId sword);
public bool EverOwned(SwordId sword);
public int SlotCapacity { get; }  public int SlotsUsed { get; }  public int FreeSlots { get; }
public void AddExtraSlots(int amount);                    // relics/events: +1..2 slots
public bool CanAcquire(SwordId sword);                    // not owned and fits
public Task<bool> AcquireSword(SwordId sword, bool ignoreSlotLimit = false); // + partners, starter cards / restore
public Task LoseSword(SwordId sword);                     // + partners, cards -> storage, level -> 0
public int StoredCardCount(SwordId sword);
public int GetLevel(SwordId sword);                       // 0..5, pairs share the leader's level
public Task SetLevel(SwordId sword, int level);           // clamped, calls OnLevelChanged on the group
public Task AddCurse(CardModel canonicalCurse);           // adds to the deck with the standard preview
public int GetRunCounter(string key);                     // saved per-run counters, e.g. "tyrfing.curses"
public void SetRunCounter(string key, int value);         // key must not contain ';' or '='
public int AddRunCounter(string key, int delta);
public bool IsCardAllowed(CardModel card);                // reward/shop rule
```
Use `AcquireSword`/`LoseSword`/`SetLevel` only outside combat (events, rest site, map). They change the deck.

### 3.10 `SwordAcquisition` (for events / shops, spec §5)

```csharp
public static Task<SwordId?> OfferChoice(Player player, IReadOnlyList<SwordId> candidates, PlayerChoiceContext choiceContext, bool canSkip = true);
public static Task<SwordId?> OfferRandom(Player player, int count, Rng rng, PlayerChoiceContext choiceContext, bool canSkip = true);
public static Task<SwordId?> PickSword(Player player, IReadOnlyList<SwordId> swords, PlayerChoiceContext choiceContext, bool canSkip, LocString gridPrompt);
```
`OfferChoice` asks the player to release a sword when Mangeomchong is full (Gram cannot be released). Pass a game
Rng (an event's `Rng`, or `player.RunState.Rng.Niche`) — never `System.Random`.

### 3.11 `SwordForge` (rest-site rules, RestSite/SwordForge.cs)

```csharp
public enum ForgeMode { Safe, GamblePlus2, GamblePlus3 }
public enum ForgeOutcomeKind { Success, FailureLevelDown, FailureCurse, FailureNothing, Shatter, ShatterReset }
public readonly record struct ForgeOdds(int Success, int Failure, int Shatter);
public readonly record struct ForgeOutcome(ForgeOutcomeKind Kind, SwordId Sword, int OldLevel, int NewLevel);
public static int GainFor(ForgeMode mode);
public static ForgeOdds OddsFor(ForgeMode mode);
public static bool IsAllowed(ForgeMode mode, int currentLevel);
public static List<SwordId> UpgradeableSwords(Mangeomchong relic);
public static Task<ForgeOutcome> Apply(Player player, SwordId sword, ForgeMode mode, Rng rng);
```
Per-sword forge **story events** (spec §4 "검마다 강화 이벤트가 다름") can call `SwordForge.Apply` with the event's Rng.

### 3.12 `SwordCurseCard` (Curses/SwordCurseCard.cs)

```csharp
[Pool(typeof(TokenCardPool))]   // never in the game's random curse pool
public abstract class SwordCurseCard : ConstructedCardModel
protected SwordCurseCard(bool playable = false, int cost = -1, TargetType target = TargetType.None); // Unplayable unless playable
public abstract SwordId Sword { get; }
protected virtual Task OnCursePlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay);        // playable curses only
```

### 3.13 `CurrentSwordPower` (framework; you only add localization)

Per-sword text keys (optional, fall back to the generic text):
`MAGICSWORDSMAN-CURRENT_SWORD_POWER.<SWORD>.title / .description / .smartDescription` where `<SWORD>` is
`SwordId.ToString().ToUpperInvariant()` (no underscores: `CLAIOMHSOLAIS`, `GANJIANG`). Variables:
`{Level}`, `{HalfLevel}`, `{SwordName}`, `{InheritedName}`.

---

## 4. How to add a sword behavior

1. Open `Swords/Behaviors/<Sword>Behavior.cs` (the stub already exists, registration is automatic).
2. Fill `StarterCards` (2 cards, spec §5 [Claude]) and `FailureCurse`.
3. Implement the current-sword effect with group-3 hooks; use `ctx.Scale(...)` for every number so Kusanagi's
   inherited version is half (rounded down, min 1), and skip costs/penalties when `ctx.IsInherited`. If the spec's
   inherited effect is not simply "half" (Tyrfing: first attack each turn only; Caladbolg: 2 targets), branch on
   `ctx.IsInherited`.
4. Costs: `CanBecomeCurrent` / `CanPlayCard` / `OnAcquired(firstTime)` / `OnPlayerTurnEnd` etc.
5. Per-combat numbers: `ctx.GetCounter/SetCounter/AddCounter("key")`; per-run numbers:
   `ctx.Player.GetRelic<Mangeomchong>()!.AddRunCounter("<sword>.<key>", 1)`.
6. Effects that need a visible stack (Skofnung wounds, Claiomh Solais light) are ordinary powers in `Powers/`
   (derive from `MagicSwordsmanPower`) applied with `PowerCmd.Apply<T>(...)` — grep the game for usage.
7. Add `CURRENT_SWORD_POWER.<SWORD>.*` text to your powers fragment.

Worked example (already in the code, `GramBehavior`):

```csharp
public sealed class GramBehavior : SwordBehavior
{
    public override SwordId Id => SwordId.Gram;

    // current effect: the owner's powered attacks deal +Level (Kusanagi inherits half via ctx.Scale)
    public override decimal ModifyDamageAdditive(SwordContext ctx, Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (dealer != ctx.Creature) return 0m;
        if (!props.IsPoweredAttack()) return 0m;     // same filter as the game's StrengthPower
        return ctx.Scale(ctx.Level);
    }
}
```

Example of an end-of-turn rule (sketch for Claiomh Solais; verify the power API you use):

```csharp
public override Task OnPlayerTurnEnd(SwordContext ctx, PlayerChoiceContext choiceContext)
{
    if (ctx.IsCurrent) ctx.AddCounter("light", 1);   // spec: charge 1 light if current at end of turn
    else ctx.SetCounter("light", 0);                  // cost: otherwise all light is lost
    return Task.CompletedTask;
}
```
(Kusanagi's inherited half-charge needs its own handling: owned-sword lifecycle hooks are not inherited
automatically — only group-3 hooks are. Check `SwordCombat.CurrentSword(ctx.Player) == SwordId.Kusanagi &&
ctx.Combat?.Previous == Id` in your own lifecycle hook.)

---

## 5. How to add a sword card (full minimal example)

File `MagicSwordsmanCode/Cards/Gram/GramCleave.cs`:

```csharp
using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Gram;

/// <summary>그람의 일격 — example Gram card: cost 1, 9 damage, +2 damage per Gram level.</summary>
public sealed class GramCleave : MagicSwordCard
{
    public override SwordId? Sword => SwordId.Gram;     // makes it a Gram card (switch/summon, filter, no upgrade)

    public GramCleave() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(9);            // no upgrade value: sword cards cannot be upgraded
        WithDamagePerLevel(2);    // +2 per Gram level, shown green in the combat preview like Strength
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // Gram is already current here (switch happened before this method), so Gram's +Level applies too.
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}
```

Fragment `loc_fragments/gram/kor/cards.json`:
```json
{
  "MAGICSWORDSMAN-GRAM_CLEAVE.title": "그람의 일격",
  "MAGICSWORDSMAN-GRAM_CLEAVE.description": "피해를 {Damage:diff()} 줍니다.\n[gold]그람[/gold] {SwordLevel}단계 (단계마다 피해 +2)."
}
```
and the same keys in `loc_fragments/gram/eng/cards.json` (English text).

Rules for cards:
- Implement `OnCardPlay`, never `OnPlay` (sealed).
- Loc key = `MAGICSWORDSMAN-` + class name in UPPER_SNAKE_CASE (`GramCleave` -> `MAGICSWORDSMAN-GRAM_CLEAVE`);
  `.title` and `.description` are required (the analyzer fails the build with STS001 otherwise).
- Portrait: `MagicSwordsman/images/card_portraits/<gram_cleave>.png` (+ `big/`); missing -> placeholder.
- Rarity: the 2 starter cards granted on acquisition should be `CardRarity.Basic` (never offered as rewards, like
  BrokenBlade); reward cards are Common/Uncommon/Rare.
- Grant starter cards from the behavior: `public override IEnumerable<CardModel> StarterCards => [ModelDb.Card<A>(), ModelDb.Card<B>()];`
- Number scaling: `WithDamagePerLevel`/`WithBlockPerLevel` for the card's own damage/block (works in the real hit
  and the preview); any other number: `WithLevelVar("Hits", 2, 1)` + `{Hits}` in the text + `LevelVar("Hits")` at
  play time, or `Scaled(base, perLevel)`. Out of combat the text shows base values (BaseLib/game `CalculatedVar`
  only evaluates in combat — verified in `CalculatedVar.Calculate`), so also show `{SwordLevel}` in the text.
- A card that must work only with a condition: override `IsPlayableExtra`.
- Shared rules for all cards of one sword (e.g. Tyrfing's run curse on use): make an intermediate
  `abstract class TyrfingCard : MagicSwordCard` in `Cards/Tyrfing/` that overrides `OnCardPlay` (sealed) and calls a
  new abstract method.
- Twin-sword cards (간장·막야 쌍검): use `Sword => SwordId.Ganjiang` (the pair is always owned together, so the reward
  filter is correct); the "counts as both" pair bonus is content logic.
- Common cards: same, without overriding `Sword`; they are upgradable at the normal Smith (use upgrade values).
- Curse: `public sealed class GramGreedCurse : SwordCurseCard { public override SwordId Sword => SwordId.Gram; }`
  in `Curses/`, returned from the behavior's `FailureCurse` (`ModelDb.Card<GramGreedCurse>()`).

---

## 6. Gotchas

- **RNG**: only game Rngs (`player.RunState.Rng.Niche`, `.CombatTargets`, an event's `Rng`, `player.PlayerRng.*`).
  Never `System.Random` (breaks seeds and multiplayer).
- **Multiplayer**: every API takes the `Player`; never use a global "the player". Combat state is per player.
- **Hooks fire for everyone**: filter on `ctx.Creature`, `card.Owner == ctx.Player`.
- **Preview purity**: the 4 damage/block Modify hooks run many times per frame for previews; no side effects.
- **SwordId values are saved** — do not reorder the enum.
- **Do not edit** main localization files or framework files; use fragments / TODO(framework) notes.
- **Text**: Korean first (kor), English (eng) for every key. Use the game's markup (`[gold]...[/gold]`,
  `{Damage:diff()}`, `{Block:diff()}`, `{Cards:diff()}`, `{IfUpgraded:show:...|}`).
- **Images**: missing images fall back to placeholders and only log an info line.

---

## 7. Localization fragments (mandatory convention)

Content agents must **not** edit `MagicSwordsman/localization/<lang>/*.json`. Write fragments instead:

```
MagicSwordsman/loc_fragments/<group>/<kor|eng>/<cards|powers|relics|events|potions|card_keywords>.json
```
- `<group>`: your work unit, lower-case, e.g. `gram`, `ganjiang_moye`, `kusanagi`, `common`, `events_act1`,
  `relics_slots`. One agent per group folder.
- Each file is one flat JSON object `{ "KEY": "text", ... }`, UTF-8, same keys in `kor` and `eng`.
- Other tables (`card_selection`, `static_hover_tips`, `rest_site_ui`, `ancients`) are allowed when needed; the merge
  script warns but merges them.
- The build already sees fragment keys: the csproj target `AddLocFragmentsForAnalyzer` copies fragments to
  `obj/.../loc_fragments_localization/...` and passes them to the localization analyzer, so your build passes before
  the merge. **The game itself only loads the merged main files.**
- Integrator: `python3 tools/merge_loc_fragments.py` (dry run, exit 1 on conflicts) then `--write`
  (`--group <name>` to merge one group, `--overwrite` to let fragments replace different main values). Re-running is
  safe (identical keys are skipped).

---

## 8. Verified vs. unverified

Verified against the decompiled game / BaseLib (and the build):
- `[SavedProperty]` on relics supports `int`, `int[]`, `string`, `List<SerializableCard>` (game `SavedProperties`,
  BaseLib `SavePatchUtils.SupportedTypes`); BaseLib's `PostModInitPatch.LatePostInit` registers mod types via
  `SavedPropertiesTypeCache` (`CacheSavedProperties`) — no manual `InjectTypeIntoCache` needed.
- Stored cards use `ToSerializable` / `CardModel.FromSerializable` / `RunState.AddCard` exactly like `PaelsTooth`.
- `ModifyCardRewardCreationOptions` is called by every `CardFactory.CreateForReward` (once per card, options are
  mutated in place by `WithCardPools` -> our filter is idempotent); `ModifyMerchantCardPool` by
  `CardFactory.CreateForMerchant`, which throws when a type/rarity has no card (hence the safety valves).
- Relics and powers both receive combat hooks (`CombatState.IterateHookListeners`: creature powers, player relics,
  cards, potions...). `Hook.BeforeCombatStart` / `AfterCombatEnd` iterate the run's listeners (relics included).
- `CardModel.IsUpgradable` is `CurrentUpgradeLevel < MaxUpgradeLevel`, so `MaxUpgradeLevel = 0` blocks the Smith.
- `RestSiteOption` title/description keys: `rest_site_ui` `OPTION_<OptionId>.name/.description`; BaseLib
  `CustomRestSiteOption.CustomIconPath` overrides the icon.
- `CardSelectCmd.FromChooseACardScreen` accepts at most 3 cards (more -> `FromSimpleGrid`, as `PickSword` does).
- `CalculatedVar` only evaluates in combat.
- The localization analyzer reads every AdditionalFile whose path contains `localization` and ends in `.json`,
  keyed by file name (fragment trick tested: a card with keys only in a fragment builds, without them STS001).
- `RunState.Rng.Niche` is the run RNG used by game relics for one-off rolls (WingCharm, NeowsBones, WarHammer...).

Unverified (needs an in-game test; nothing here has been run inside the game yet):
- The whole UI flow of "마검 강화" (choose-a-card screens with token cards at the rest site, `BlockingPlayerChoiceContext`,
  result card) and SwordSwap's selection, especially in multiplayer.
- That a negative additive damage modifier behaves as a flat reduction for Durandal (applied before block, never
  below 0) — `DurandalBehavior` has a TODO.
- `CurrentSwordPower` display: per-sword title/description switching and the icon number (= level, may be hidden at 0).
- Save/load round trip of Mangeomchong (types are supported; actual save not tested).
- The reward filter fallback threshold (3) and the merchant fallback with the final card pool.
- Rest-site icon path `ui/rest_site/option_smith.png` (taken from `SmithRestSiteOption`) is reused as a placeholder.
- THE_ARCHITECT dialogue keys in `ancients.json` satisfy the analyzer; the real dialogue structure was copied from
  analyzer expectations, not tested in game.

---

## 9. Open TODOs (content)

| Sword | File | To do |
|---|---|---|
| 그람 | `GramBehavior.cs` | failure curse; 5단계 curse "니벨룽의 보물" (playable, gives gold, cannot be removed) in `OnLevelChanged` |
| 간장·막야 | `GanjiangBehavior.cs`, `MoyeBehavior.cs` | attack/block effects, pair bonus (`OnAnySwordSwitched` / counters), Max HP loss in `OnAcquired(firstTime)` |
| 쿠사나기 | `KusanagiBehavior.cs` | cards (정화), combat-end HP loss (`OnCombatEnd`, verify a safe HP-loss command), curse |
| 티르빙 | `TyrfingBehavior.cs` | ignore block (find the game's unblockable `ValueProp`), run curses (first 3 uses), inherited: first attack each turn |
| 다인슬레이프 | `DainsleifBehavior.cs` | `ModifyCardPlayResultPileTypeAndPosition` -> draw pile; cannot gain block while current |
| 뒤랑달 | `DurandalBehavior.cs` | verify reduction; cards |
| 스코프눙 | `SkofnungBehavior.cs` | wound power, burst at 12; inherited: 1 wound per 2 attacks |
| 오니마루 | `OnimaruBehavior.cs` | auto attack + kinds, command cards, 25% random kind in normal fights, elite/boss bonus |
| 클라이브 솔라시 | `ClaiomhSolaisBehavior.cs` | light power/counter, 발도 cards |
| 칼라드볼그 | `CaladbolgBehavior.cs` | hit up to 3 enemies, single-enemy penalty; inherited: 2 targets |
| all | — | 2 starter cards (Basic), failure curse, `CURRENT_SWORD_POWER.<SWORD>` text, token description lore (from the research doc) |
| events | `Events/` | act 1/2 boss acquisition events (`SwordAcquisition.OfferRandom(player, 3, rng, ctx)`), ? room/shop acquisition, forge story events, origin slideshow (spec §5 [임시]) |
