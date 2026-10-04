using BaseLib.Abstracts;
using MagicSwordsman.MagicSwordsmanCode.Cards.Tokens;
using MagicSwordsman.MagicSwordsmanCode.RestSite;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace MagicSwordsman.MagicSwordsmanCode.Events;

/// <summary>
/// "만검총의 울림" (after the act 1 boss) / "깊어지는 만검총" (after the act 2 boss) — content doc §6.1 / §6.2,
/// spec §5 [확정] "1막 보스 후 확정 이벤트, 2막 보스 후 확정 이벤트: 무작위 후보 3자루 중 1 선택".
///
/// Started by <see cref="TombResonanceTrigger"/> right after the next act is entered (never from the ? room pool:
/// autoAdd false + IsAllowed false). One event class, two texts: the act index picks the INITIAL / DEEP page.
/// Options: 3 random unowned swords (button = name, one-line intro, cost); a 4th exit:
///   act 1 -> PASS ("부르지 않는다", 25 gold), act 2 -> FORGO (safe +1 on an owned sword).
/// 간장·막야 (first time) asks for confirmation (Max HP -6, paid by GanjiangBehavior.OnAcquired).
/// When Mangeomchong is full the player releases a sword first (SwordEventHelper.Acquire); cancelling returns here.
/// A per-act run counter on Mangeomchong marks the event as done so the trigger does not repeat it.
/// Not shared in multiplayer (IsShared false): every player gets their own copy; players without 만검총 only see PASS.
/// </summary>
public sealed class TombResonanceEvent : CustomEventModel
{
    private const string PassGoldVar = "PassGold";
    private const int CandidateCount = 3;

    private List<SwordId> _candidates = new();

    public TombResonanceEvent() : base(autoAdd: false)
    {
    }

    /// <summary>Run counter key (Mangeomchong) marking the event of this act as finished.</summary>
    public static string DoneKey(int actIndex) => $"event.tomb_resonance.act{actIndex}";

    /// <summary>Never rolled in ? rooms; only <see cref="TombResonanceTrigger"/> enters it.</summary>
    public override bool IsAllowed(IRunState runState) => false;

    // TODO(art): own portrait. Reuses the game's "Grave of the Forgotten" event portrait as a placeholder
    // (path built like EventModel.InitialPortraitPath: events/<id lower>.png).
    public override string? CustomInitialPortraitPath => ImageHelper.GetImagePath("events/grave_of_the_forgotten.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new GoldVar(PassGoldVar, 25),
        new DynamicVar("MaxHpCost", SwordEventHelper.MaxHpCost),
    ];

    private int ActIndex => Owner?.RunState.CurrentActIndex ?? 1;

    /// <summary>Run start (act index 0): pick 1 of 3 swords next to Gram, no PASS (사용자 결정 2026-10-04).</summary>
    private bool IsStart => ActIndex == 0;

    /// <summary>After the act 2 boss (entering the 3rd act, index 2) the "deep" text and exit are used.</summary>
    private bool IsDeep => ActIndex >= 2;

    private string StartPage => IsStart ? "START" : IsDeep ? "DEEP" : "INITIAL";

    private string PageKey(string page) => $"{Id.Entry}.pages.{page}";

    public override LocString InitialDescription =>
        IsMutable && Owner != null ? StartDescription() : base.InitialDescription;

    private LocString StartDescription()
    {
        var desc = L10NLookup($"{PageKey(StartPage)}.description");
        var tomb = SwordEventHelper.Tomb(Owner!);
        var full = tomb != null && _candidates.Count > 0 && !_candidates.Any(tomb.CanAcquire);
        desc.Add("FullNote", full ? SwordLore.Generic("FULL_NOTE") : SwordLore.Generic("EMPTY"));
        return desc;
    }

    protected override Task BeforeEventStarted(bool isPreFinished)
    {
        _candidates = new List<SwordId>();
        if (Owner != null && SwordEventHelper.Tomb(Owner) is { } tomb)
        {
            // The event Rng is seeded only by run seed + event id, so mix in the act: act 1 and act 2 roll differently.
            var rng = new Rng(Rng.Seed, $"tomb_resonance_act{ActIndex}");
            _candidates = SwordEventHelper.Roll(tomb, CandidateCount, rng);
        }

        return Task.CompletedTask;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var options = new List<EventOption>();
        var tomb = Owner != null ? SwordEventHelper.Tomb(Owner) : null;

        if (tomb != null)
        {
            foreach (var sword in _candidates)
            {
                var s = sword;
                var option = new EventOption(this, () => ChooseSword(s), SwordLore.Name(s),
                    SwordEventHelper.OptionDescription(s), $"{PageKey(StartPage)}.options.SWORD_{SwordLore.SwordKey(s)}",
                    [HoverTipFactory.FromCard(SwordTokenCard.CanonicalFor(s))]);
                options.Add(option);
            }
        }

        if (IsStart && tomb != null && options.Count > 0)
        {
            // Run start: the player must take one of the three swords.
        }
        else if (IsDeep && tomb != null)
        {
            var canForge = SwordForge.UpgradeableSwords(tomb).Count > 0;
            options.Add(new EventOption(this, canForge ? Forgo : (Func<Task>?)null, $"{PageKey(StartPage)}.options.FORGO"));
            // Every sword already at level 5: FORGO is locked, keep a way out.
            if (!canForge) options.Add(new EventOption(this, Pass, $"{PageKey(StartPage)}.options.PASS"));
        }
        else
        {
            options.Add(new EventOption(this, Pass, $"{PageKey(StartPage)}.options.PASS"));
        }

        return options;
    }

    private void ShowStart() => SetEventState(InitialDescription, GenerateInitialOptionsWrapper());

    private Task ChooseSword(SwordId sword)
    {
        var tomb = SwordEventHelper.Tomb(Owner!);
        if (tomb != null && SwordEventHelper.CostsMaxHp(tomb, sword))
        {
            SetEventState(L10NLookup($"{PageKey("CONFIRM_PAIR")}.description"),
            [
                new EventOption(this, () => AcquireAndFinish(sword), $"{PageKey("CONFIRM_PAIR")}.options.OFFER")
                    .ThatDecreasesMaxHp(SwordEventHelper.MaxHpCost),
                new EventOption(this, BackToStart, $"{PageKey("CONFIRM_PAIR")}.options.BACK"),
            ]);
            return Task.CompletedTask;
        }

        return AcquireAndFinish(sword);
    }

    private Task BackToStart()
    {
        ShowStart();
        return Task.CompletedTask;
    }

    private async Task AcquireAndFinish(SwordId sword)
    {
        var tomb = SwordEventHelper.Tomb(Owner!);
        if (tomb == null)
        {
            ShowStart();
            return;
        }

        var firstTime = !tomb.EverOwned(sword);
        if (!await SwordEventHelper.Acquire(Owner!, sword, new BlockingPlayerChoiceContext()))
        {
            ShowStart(); // release cancelled: back to the three swords
            return;
        }

        MarkDone();
        SetEventFinished(IsStart ? SwordEventHelper.StartAcquiredText(sword) : SwordEventHelper.AcquiredText(sword, firstTime));
    }

    private async Task Pass()
    {
        await PlayerCmd.GainGold(DynamicVars[PassGoldVar].BaseValue, Owner!);
        MarkDone();
        SetEventFinished(L10NLookup($"{PageKey("PASS")}.description"));
    }

    /// <summary>Act 2 exit: "검을 받지 않는다 — 대신 보유 검 하나를 안전 강화(+1)" (content doc §6.2).</summary>
    private async Task Forgo()
    {
        var tomb = SwordEventHelper.Tomb(Owner!);
        var swords = tomb != null ? SwordForge.UpgradeableSwords(tomb) : [];
        var picked = await SwordAcquisition.PickSword(Owner!, swords, new BlockingPlayerChoiceContext(), canSkip: true,
            new LocString("card_selection", "MAGICSWORDSMAN-CHOOSE_SWORD_TO_FORGE"));
        if (picked is not { } sword)
        {
            ShowStart();
            return;
        }

        var outcome = await SwordForge.Apply(Owner!, sword, ForgeMode.Safe, Rng);
        MarkDone();
        var text = L10NLookup($"{PageKey("FORGO")}.description");
        text.Add("Sword", SwordLore.Name(sword));
        text.Add("NewLevel", outcome.NewLevel);
        text.Add("Stage", SwordLore.StageName(sword, outcome.NewLevel));
        SetEventFinished(text);
    }

    private void MarkDone()
    {
        if (Owner != null) SwordEventHelper.Tomb(Owner)?.SetRunCounter(DoneKey(ActIndex), 1);
    }
}
