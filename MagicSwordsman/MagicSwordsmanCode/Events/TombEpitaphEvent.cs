using BaseLib.Abstracts;
using MagicSwordsman.MagicSwordsmanCode.RestSite;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Events;

/// <summary>
/// "검총의 비문" — low-chance ? room sword event (content doc §6.4, spec §5 [확정] "? 방에서 낮은 확률로 획득").
/// Scene from the research report (#35 독고구패: 「깊은 계곡에 자신의 검을 묻고 새긴 비문」 「검총에는 나무 패만 남아
/// 있다」), told generically — it does not claim any sword's legend.
///
/// Pool: BaseLib adds a CustomEventModel with no Acts to ModelDb.AllSharedEvents, which every act shuffles into its
/// event list (ActModel.GenerateRooms); RoomSet.EnsureNextEventIsValid skips events whose IsAllowed is false and never
/// repeats a visited event. So: once per run, ? rooms only, and only when IsAllowed: from act 2 on (index >= 1), a
/// player owns 만검총 and at least one sword is still unowned. Chance: one entry among ~30 events of an act (~3% per
/// event room) — the game has no event weights, so the content doc's "약 6%" is approximated, see report.
///
/// Options:
///   1. PULL  — reveals one random unowned sword (rolled at start) -> TAKE (release a sword if full; 간장·막야 shows
///              the Max HP cost) / LEAVE_IT.
///   2. FORCE — lose 8 HP, choose 1 of 2 random unowned swords.
///   3. READ  — lose 5 HP, safe +1 on an owned sword (locked if none can be upgraded).
///   4. COVER — 30 gold.
/// </summary>
public sealed class TombEpitaphEvent : CustomEventModel
{
    private const string ForceHpVar = "ForceHpLoss";
    private const string ReadHpVar = "ReadHpLoss";
    private const string CoverGoldVar = "CoverGold";

    private SwordId? _pulled;
    private List<SwordId> _forced = new();

    // autoAdd (default true): BaseLib puts it into the shared ? room event pool.
    public TombEpitaphEvent()
    {
    }

    // TODO(art): own portrait. Reuses the game's "Grave of the Forgotten" event portrait as a placeholder.
    public override string? CustomInitialPortraitPath => ImageHelper.GetImagePath("events/grave_of_the_forgotten.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(ForceHpVar, 8m),
        new DynamicVar(ReadHpVar, 5m),
        new GoldVar(CoverGoldVar, 30),
        new DynamicVar("MaxHpCost", SwordEventHelper.MaxHpCost),
    ];

    public override bool IsAllowed(IRunState runState)
    {
        if (runState.CurrentActIndex < 1) return false; // "1막 이후"
        return runState.Players.Any(p =>
            SwordEventHelper.Tomb(p) is { } tomb && SwordEventHelper.Unowned(tomb).Count > 0);
    }

    private string PageKey(string page) => $"{Id.Entry}.pages.{page}";

    protected override Task BeforeEventStarted(bool isPreFinished)
    {
        _pulled = null;
        _forced = new List<SwordId>();
        if (Owner != null && SwordEventHelper.Tomb(Owner) is { } tomb)
        {
            _pulled = SwordEventHelper.Roll(tomb, 1, Rng).Cast<SwordId?>().FirstOrDefault();
            _forced = SwordEventHelper.Roll(tomb, 2, Rng);
        }

        return Task.CompletedTask;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var tomb = Owner != null ? SwordEventHelper.Tomb(Owner) : null;
        var options = new List<EventOption>();
        var initial = PageKey("INITIAL");

        var canPull = tomb != null && _pulled != null;
        options.Add(new EventOption(this, canPull ? Pull : (Func<Task>?)null, $"{initial}.options.PULL"));

        var canForce = tomb != null && _forced.Count > 0;
        options.Add(new EventOption(this, canForce ? Force : (Func<Task>?)null, $"{initial}.options.FORCE")
            .ThatDoesDamage(DynamicVars[ForceHpVar].BaseValue));

        var canRead = tomb != null && SwordForge.UpgradeableSwords(tomb).Count > 0;
        options.Add(new EventOption(this, canRead ? Read : (Func<Task>?)null, $"{initial}.options.READ")
            .ThatDoesDamage(DynamicVars[ReadHpVar].BaseValue));

        options.Add(new EventOption(this, Cover, $"{initial}.options.COVER"));
        return options;
    }

    private void ShowStart() => SetEventState(InitialDescription, GenerateInitialOptionsWrapper());

    // ------------------------------------------------------------------ 1. 뽑는다

    private Task Pull()
    {
        ShowReveal();
        return Task.CompletedTask;
    }

    private void ShowReveal()
    {
        if (_pulled is not { } sword || SwordEventHelper.Tomb(Owner!) is not { } tomb)
        {
            ShowStart();
            return;
        }

        var desc = L10NLookup($"{PageKey("REVEAL")}.description");
        desc.Add("Sword", SwordLore.Name(sword));
        desc.Add("Tagline", SwordLore.Line(sword, "TAGLINE"));

        var costsHp = SwordEventHelper.CostsMaxHp(tomb, sword);
        var take = new EventOption(this, () => TakeRevealed(sword),
            $"{PageKey("REVEAL")}.options.{(costsHp ? "TAKE_PAIR" : "TAKE")}");
        if (costsHp) take = take.ThatDecreasesMaxHp(SwordEventHelper.MaxHpCost);

        SetEventState(desc,
        [
            take,
            new EventOption(this, LeaveIt, $"{PageKey("REVEAL")}.options.LEAVE_IT"),
        ]);
    }

    private async Task TakeRevealed(SwordId sword)
    {
        if (!await TryAcquireAndFinish(sword)) ShowReveal(); // release cancelled
    }

    private Task LeaveIt()
    {
        SetEventFinished(L10NLookup($"{PageKey("LEAVE_IT")}.description"));
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ 2. 힘으로 뽑는다

    private async Task Force()
    {
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner!.Creature, DynamicVars[ForceHpVar].BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered, (Creature?)null, (CardModel?)null);
        if (Owner.Creature.IsDead) return;

        var picked = await SwordAcquisition.PickSword(Owner, _forced, new BlockingPlayerChoiceContext(), canSkip: true,
            new LocString("card_selection", "MAGICSWORDSMAN-CHOOSE_SWORD_TO_ACQUIRE"));
        if (picked is not { } sword)
        {
            SetEventFinished(L10NLookup($"{PageKey("FORCE_NOTHING")}.description"));
            return;
        }

        var tomb = SwordEventHelper.Tomb(Owner);
        if (tomb != null && SwordEventHelper.CostsMaxHp(tomb, sword))
        {
            SetEventState(L10NLookup($"{PageKey("CONFIRM_PAIR")}.description"),
            [
                new EventOption(this, () => ForceTake(sword), $"{PageKey("CONFIRM_PAIR")}.options.OFFER")
                    .ThatDecreasesMaxHp(SwordEventHelper.MaxHpCost),
                new EventOption(this, ForceGiveUp, $"{PageKey("CONFIRM_PAIR")}.options.REFUSE"),
            ]);
            return;
        }

        await ForceTake(sword);
    }

    private async Task ForceTake(SwordId sword)
    {
        if (!await TryAcquireAndFinish(sword))
            SetEventFinished(L10NLookup($"{PageKey("FORCE_NOTHING")}.description"));
    }

    private Task ForceGiveUp()
    {
        SetEventFinished(L10NLookup($"{PageKey("FORCE_NOTHING")}.description"));
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ 3. 비문을 읽는다

    private async Task Read()
    {
        var tomb = SwordEventHelper.Tomb(Owner!);
        var swords = tomb != null ? SwordForge.UpgradeableSwords(tomb) : [];
        // Pick first (cancel -> back, nothing paid), then pay the HP.
        var picked = await SwordAcquisition.PickSword(Owner!, swords, new BlockingPlayerChoiceContext(), canSkip: true,
            new LocString("card_selection", "MAGICSWORDSMAN-CHOOSE_SWORD_TO_FORGE"));
        if (picked is not { } sword)
        {
            ShowStart();
            return;
        }

        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner!.Creature, DynamicVars[ReadHpVar].BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered, (Creature?)null, (CardModel?)null);
        if (Owner.Creature.IsDead) return;

        var outcome = await SwordForge.Apply(Owner, sword, ForgeMode.Safe, Rng);
        var text = L10NLookup($"{PageKey("READ")}.description");
        text.Add("Sword", SwordLore.Name(sword));
        text.Add("NewLevel", outcome.NewLevel);
        text.Add("Stage", SwordLore.StageName(sword, outcome.NewLevel));
        SetEventFinished(text);
    }

    // ------------------------------------------------------------------ 4. 덮어 둔다

    private async Task Cover()
    {
        await PlayerCmd.GainGold(DynamicVars[CoverGoldVar].BaseValue, Owner!);
        SetEventFinished(L10NLookup($"{PageKey("COVER")}.description"));
    }

    // ------------------------------------------------------------------ shared

    private async Task<bool> TryAcquireAndFinish(SwordId sword)
    {
        var tomb = SwordEventHelper.Tomb(Owner!);
        if (tomb == null) return false;
        var firstTime = !tomb.EverOwned(sword);
        if (!await SwordEventHelper.Acquire(Owner!, sword, new BlockingPlayerChoiceContext())) return false;
        SetEventFinished(SwordEventHelper.AcquiredText(sword, firstTime));
        return true;
    }
}
