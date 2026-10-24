using System.Runtime.CompilerServices;
using Mumyeong.MumyeongCode.Cards;
using Mumyeong.MumyeongCode.Dialogue;
using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Powers;
using Mumyeong.MumyeongCode.Stages;
using Mumyeong.MumyeongCode.Visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Mumyeong.MumyeongCode.Relics;

/// <summary>
/// 코어 — 무명's starter relic (spec §2). The single owner of all run data:
///   FigurePlus1  int     contracted figure as (int)FigureId + 1 (0 = no contract yet)
///   Contract     int     ContractType
///   Mastery      int     figure cards played this run (spec §5)
///   Stage        int     SoulStage
///   Resonance    int     resonance 0..3 the theater ended with
///   RunCounters  string  "key=value;..." (event done flags, descent task, ...)
/// Run hooks: reward / shop / transform / in-combat generation filter (shared cards + the figure's cards), rest-site
/// option "필름을 돌린다" while a stage-up is ready. Combat hooks: 한 계약 effects and the descent task tracking.
/// Pattern: MagicSwordsman Mangeomchong (same saved types, same filter code, same safety valves).
/// </summary>
public sealed class SoulCore : MumyeongRelic
{
    private const string StatusVar = "Status";

    private int _figurePlus1;
    private int _contract;
    private int _mastery;
    private int _stage;
    private int _resonance;
    private string _runCounters = "";

    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new StringVar(StatusVar)];

    public override bool ShowCounter => IsMutable && Figure != null;

    /// <summary>Counter on the icon: mastery (figure cards played).</summary>
    public override int DisplayAmount => IsMutable ? _mastery : 0;

    // =====================================================================================
    // Saved properties
    // =====================================================================================

    [SavedProperty]
    public int FigurePlus1
    {
        get => _figurePlus1;
        set { AssertMutable(); _figurePlus1 = value; UpdateStatus(); }
    }

    [SavedProperty]
    public int Contract
    {
        get => _contract;
        set { AssertMutable(); _contract = value; UpdateStatus(); }
    }

    [SavedProperty]
    public int Mastery
    {
        get => _mastery;
        set { AssertMutable(); _mastery = value; UpdateStatus(); }
    }

    [SavedProperty]
    public int Stage
    {
        get => _stage;
        set { AssertMutable(); _stage = value; UpdateStatus(); }
    }

    [SavedProperty]
    public int Resonance
    {
        get => _resonance;
        set { AssertMutable(); _resonance = value; UpdateStatus(); }
    }

    [SavedProperty]
    public string RunCounters
    {
        get => _runCounters;
        set { AssertMutable(); _runCounters = value ?? ""; UpdateStatus(); }
    }

    // =====================================================================================
    // Run data API
    // =====================================================================================

    public FigureId? Figure =>
        _figurePlus1 > 0 && FigureRegistry.IsValid(_figurePlus1 - 1) ? (FigureId)(_figurePlus1 - 1) : null;

    public ContractType ContractKind => Enum.IsDefined(typeof(ContractType), _contract) ? (ContractType)_contract : ContractType.None;

    public SoulStage CurrentStage => Enum.IsDefined(typeof(SoulStage), _stage) ? (SoulStage)_stage : SoulStage.Possession;

    /// <summary>Called by the theater event when the contract is made (outside combat).</summary>
    public void MakeContract(FigureId figure, ContractType contract, int resonance)
    {
        FigurePlus1 = (int)figure + 1;
        Contract = (int)contract;
        Resonance = resonance;
        Stage = (int)SoulStage.Possession;
        if (Owner != null) SpiritVisuals.RefreshSoon(Owner);
    }

    public void SetStage(SoulStage stage)
    {
        Stage = (int)stage;
        if (Owner != null) SpiritVisuals.RefreshSoon(Owner); // the spirit grows clearer with each stage
    }

    /// <summary>MumyeongCard.OnPlay: a card of the contracted figure was played.</summary>
    public void OnFigureCardPlayed(FigureId figure)
    {
        if (Figure != figure) return;
        Mastery = _mastery + 1;
        InvokeDisplayAmountChanged();
        if (Owner != null) SpiritVisuals.Pulse(Owner);
    }

    /// <summary>Per-run integer counter (saved). Key must not contain ';' or '='.</summary>
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

    /// <summary>Shared cards always; figure cards only of the contracted figure (none before the contract).</summary>
    public bool IsCardAllowed(CardModel card) =>
        card is not MumyeongCard { Figure: { } f } || Figure == f;

    // =====================================================================================
    // Tooltip
    // =====================================================================================

    private void UpdateStatus()
    {
        if (!IsMutable) return;
        if (DynamicVars[StatusVar] is not StringVar sv) return;
        try
        {
            sv.StringValue = StageRules.StatusText(this);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SoulCore] status text: {e.Message}");
            sv.StringValue = "";
        }

        InvokeDisplayAmountChanged();
    }

    public override async Task AfterObtained()
    {
        await base.AfterObtained();
        UpdateStatus();
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        await base.AfterRoomEntered(room);
        UpdateStatus(); // the act index in the status line changes with the map
        SpiritTalk.OnRoomEntered(Owner, room); // shows the spirit (rest site / shop) and maybe a line by the fire
    }

    // =====================================================================================
    // Run hooks: rewards / shop / rest site
    // =====================================================================================

    private static readonly ConditionalWeakTable<Func<CardModel, bool>, object> OwnFilters = new();

    /// <summary>Below this many allowed non-basic cards the filter is skipped (CardFactory would throw).</summary>
    private const int MinFilteredRewardCards = 3;

    public override CardCreationOptions ModifyCardRewardCreationOptions(Player player, CardCreationOptions options)
    {
        if (player != Owner) return options;
        if (options.CustomCardPool != null) return options;
        if (options.CardPools.Count == 0) return options;
        var previous = options.CardPoolFilter;
        if (previous != null && OwnFilters.TryGetValue(previous, out _)) return options; // already filtered

        Func<CardModel, bool> filter = card => (previous == null || previous(card)) && IsCardAllowed(card);

        var allowed = options.CardPools
            .SelectMany(p => p.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
            .Where(filter)
            .Count(c => c.Rarity != CardRarity.Basic && c.Rarity != CardRarity.Ancient);
        if (allowed < MinFilteredRewardCards)
        {
            MainFile.Logger.Warn($"[SoulCore] only {allowed} allowed reward cards; figure filter skipped.");
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

        // Safety: CardFactory.CreateForMerchant throws if no card of a requested type remains.
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
        if (StageRules.NextReadyStage(this) == null) return false;
        options.Add(new FilmReelRestSiteOption(player));
        return true;
    }

    // =====================================================================================
    // Combat hooks: 한 계약 (spec §4.3) and the descent task (spec §5.1)
    // =====================================================================================

    public const int GrudgeLimit = 5;
    public const int GrudgeHpLoss = 4;
    public const int GrudgeStrength = 1;

    private int _turnCardCount;
    private int _turnNumberSeen;
    private int _combatBlocked;
    private int _combatKills;

    public override Task BeforeCombatStart()
    {
        _turnCardCount = 0;
        _turnNumberSeen = 0;
        _combatBlocked = 0;
        _combatKills = 0;
        return Task.CompletedTask;
    }

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != Owner || Owner.PlayerCombatState is not { TurnNumber: 1 }) return;
        SpiritTalk.OnCombatStart(Owner); // the spirit appears behind him, maybe an opening exchange
        if (ContractKind != ContractType.Grudge || Figure == null) return;
        Flash();
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, GrudgeStrength, Owner.Creature, null);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner) return;

        // Napoleon task: cards played this turn
        var turn = Owner.PlayerCombatState?.TurnNumber ?? 0;
        if (turn != _turnNumberSeen)
        {
            _turnNumberSeen = turn;
            _turnCardCount = 0;
        }
        _turnCardCount++;
        if (Figure == FigureId.Napoleon && _turnCardCount >= 8) CompleteDescentTask();

        // 한 계약: a non-figure card feeds the grudge; at GrudgeLimit the soul lashes out (HP loss) and it resets.
        if (ContractKind == ContractType.Grudge && Figure is { } figure && !FigureRegistry.IsCardOf(cardPlay.Card, figure) &&
            Owner.Creature.IsAlive)
        {
            await PowerCmd.Apply<GrudgePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, null);
            if (Owner.Creature.GetPower<GrudgePower>() is { } grudge && grudge.Amount >= GrudgeLimit)
            {
                Flash();
                await PowerCmd.Remove(grudge);
                await CreatureCmd.Damage(choiceContext, Owner.Creature, GrudgeHpLoss, DamageProps.cardHpLoss, (CardModel)null!);
            }
        }
    }

    public override Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (Figure == FigureId.Guanyu && dealer == Owner.Creature && target.IsEnemy && result.TotalDamage >= 30)
            CompleteDescentTask();
        return Task.CompletedTask;
    }

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner.Creature && result.UnblockedDamage > 0) SpiritTalk.OnOwnerDamaged(Owner);
        if (Figure == FigureId.Leonidas && target == Owner.Creature && result.BlockedDamage > 0)
        {
            _combatBlocked += result.BlockedDamage;
            if (_combatBlocked >= 50) CompleteDescentTask();
        }
        return Task.CompletedTask;
    }

    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (Figure == FigureId.Genghis && creature.IsEnemy && !wasRemovalPrevented)
        {
            _combatKills++;
            if (_combatKills >= 3) CompleteDescentTask();
        }
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner.Creature) && Figure == FigureId.Joan &&
            Owner.Creature.GetPowerAmount<JoanFaithPower>() >= 10)
            CompleteDescentTask();
        return Task.CompletedTask;
    }

    public override Task AfterCombatVictory(CombatRoom room)
    {
        SpiritTalk.OnVictory(Owner, room);
        if (Figure == FigureId.YiSunsin && (room.RoomType == RoomType.Elite || room.RoomType == RoomType.Boss) &&
            Owner.Creature.IsAlive && Owner.Creature.CurrentHp * 2 <= Owner.Creature.MaxHp)
            CompleteDescentTask();
        return Task.CompletedTask;
    }

    /// <summary>The descent task only counts once the soul reached 집중 (spec §5.1).</summary>
    private void CompleteDescentTask()
    {
        if (CurrentStage != SoulStage.Focus || GetRunCounter(StageRules.TaskDoneKey) > 0) return;
        SetRunCounter(StageRules.TaskDoneKey, 1);
        Flash();
        MainFile.Logger.Info($"[SoulCore] descent task of {Figure} completed");
    }
}
