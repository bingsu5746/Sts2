using BaseLib.Abstracts;
using Mumyeong.MumyeongCode.Cinema;
using Mumyeong.MumyeongCode.Extensions;
using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Relics;
using Mumyeong.MumyeongCode.Stages;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace Mumyeong.MumyeongCode.Events;

/// <summary>
/// 황천 영사실 — the run-start event (spec §4). Started by <see cref="EventTriggers"/> when the starting Ancient
/// (Neow) PROCEED would open the map, and only while the 코어 has no figure yet.
///
/// Shown by our own full-screen <see cref="CinemaScreen"/> (2026-11-13), not the game's event layout; this class only
/// says what each page looks like (a <see cref="Shot"/>) before setting the page's text and options.
/// Pages (2026-10-24: little text per page, a background per page, a cinematic for the contract):
///   INITIAL (hall) -> SHADOWS (hall: the three epithets) -> SCENE (scene background) -> CUT 0..2 (one option per
///   shadow) -> DESCENT (figure background, the shadow rises into the screen and becomes the spirit) -> WHO (who they
///   were) -> DEED (their real last moment) -> CONTRACT (resonance, contract type) -> the spirit rushes into you ->
///   TEACH (figure background) -> 1-of-3 figure card pick -> CONTRACTED.
/// Winner: the most resonance; a 1:1:1 tie goes to the figure of the last cut (유언), which the WORDS prompt says.
///
/// The three figures and the scene come from the event Rng (run seed + event id, mixed with a fixed key), so the same
/// seed shows the same theater. Not shared in multiplayer: every player gets their own copy (like 엔시페르's
/// TombResonanceEvent); a player who has no 코어 or already has a figure only sees LEAVE (the trigger opens the room
/// for everyone while any player still needs it).
/// Save/reload: nothing here saves the run. The last save is the one the game made when Neow finished (its room is
/// saved as pre-finished), so a reload puts the player back in Neow's finished room with the contract undone, and its
/// PROCEED opens the theater again from INITIAL (verified in the decompiled EventRoom / RunManager; untested in game).
/// The scene and the figure picked in the last cut are kept on the 코어 (run counters) for the 강림 page.
/// </summary>
public sealed class TheaterEvent : CustomEventModel
{
    public const int ShadowCount = 3;
    public const string DoneKey = "event.theater";

    /// <summary>Run counters: index of the scene in <see cref="TheaterScenes.All"/>, and (int)figure + 1 of the last cut.</summary>
    public const string SceneKey = "theater.scene";
    public const string WordsKey = "theater.words";

    private List<FigureId> _figures = new();
    private string _scene = TheaterScenes.All[0];
    private readonly List<FigureId> _picks = new();

    public TheaterEvent() : base(autoAdd: false)
    {
    }

    public override bool IsAllowed(IRunState runState) => false;

    // Drawn by CinemaScreen (the game's layout is hidden); the portrait only shows if our screen cannot be built.
    public override string? CustomInitialPortraitPath => EventArt.Resolve(EventArt.Hall);

    private string PageKey(string page) => $"{Id.Entry}.pages.{page}";

    private SoulCore? Core => Owner?.GetRelic<SoulCore>();

    /// <summary>False for a player without a 코어 or with a contract already (multiplayer: the room opens for everyone).</summary>
    private bool NeedsContract => Core is { Figure: null } core && core.GetRunCounter(DoneKey) == 0;

    public override LocString InitialDescription =>
        IsMutable && Owner != null && !NeedsContract ? L10NLookup($"{PageKey("NO_CORE")}.description") : base.InitialDescription;

    protected override Task BeforeEventStarted(bool isPreFinished)
    {
        _picks.Clear();
        var rng = new Rng(Rng.Seed, "mumyeong_theater");
        var pool = FigureRegistry.All.ToList();
        _figures = new List<FigureId>();
        while (_figures.Count < ShadowCount && pool.Count > 0)
        {
            var f = rng.NextItem(pool);
            _figures.Add(f);
            pool.Remove(f);
        }
        _scene = rng.NextItem(TheaterScenes.All.ToList()) ?? TheaterScenes.All[0];
        CinemaScreen.Direct(this, Owner,
            NeedsContract ? new Shot(EventArt.Hall, CinemaFx.Blackout, TextDelay: 2.6) : new Shot(EventArt.Hall));
        return Task.CompletedTask;
    }

    private EventOption Next(string page, string option, Action show) =>
        new(this, () => { show(); return Task.CompletedTask; }, $"{PageKey(page)}.options.{option}");

    // ------------------------------------------------------------------ INITIAL / SHADOWS / SCENE

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        NeedsContract
            ? [Next("INITIAL", "ROLL", ShowShadows)]
            : [new EventOption(this, Leave, $"{PageKey("NO_CORE")}.options.LEAVE")];

    private Task Leave()
    {
        SetEventFinished(L10NLookup($"{PageKey("NO_CORE")}.description"));
        return Task.CompletedTask;
    }

    private void ShowShadows()
    {
        CinemaScreen.Direct(this, Owner, new Shot(EventArt.Hall, CinemaFx.Shadows, _figures, TextDelay: 2.2));
        var desc = L10NLookup($"{PageKey("SHADOWS")}.description");
        desc.Add("Shadows", string.Join("\n", _figures.Select(f => $"[gold]「{FigureRegistry.Shadow(f)}」[/gold]")));
        SetEventState(desc, [Next("SHADOWS", "START", ShowScene)]);
    }

    private void ShowScene()
    {
        CinemaScreen.Direct(this, Owner, new Shot(EventArt.Scene(_scene), CinemaFx.Cut, _figures, Picks: [], TextDelay: 1.0));
        var desc = L10NLookup($"{PageKey("SCENE")}.description");
        desc.Add("Scene", TheaterScenes.Description(_scene).GetFormattedText());
        SetEventState(desc, [Next("SCENE", "FREEZE", () => ShowCut(0))]);
    }

    // ------------------------------------------------------------------ CUTS

    private void ShowCut(int cut)
    {
        CinemaScreen.Direct(this, Owner, new Shot(EventArt.Scene(_scene), CinemaFx.Cut, _figures, Picks: _picks.ToList()));
        var desc = L10NLookup($"{PageKey("CUT")}.description");
        desc.Add("Reaction", _picks.Count == 0 ? "" : Reaction(_picks[^1]));
        desc.Add("Prompt", TheaterScenes.Generic($"PROMPT.{TheaterScenes.Cuts[cut]}").GetFormattedText());

        var options = new List<EventOption>();
        foreach (var figure in _figures)
        {
            var f = figure;
            var body = TheaterScenes.Generic("REACTS");
            AddShadow(body, f);
            options.Add(new EventOption(this, () => Pick(cut, f), TheaterScenes.Line(_scene, cut, f), body,
                $"{PageKey("CUT")}.options.LINE", []));
        }

        SetEventState(desc, options);
    }

    private static string Reaction(FigureId figure)
    {
        var line = TheaterScenes.Generic("REACTION");
        AddShadow(line, figure);
        return line.GetFormattedText() + "\n\n";
    }

    /// <summary>{Shadow} and its Korean subject particle {Ga}.</summary>
    private static void AddShadow(LocString text, FigureId figure)
    {
        var shadow = FigureRegistry.Shadow(figure);
        text.Add("Shadow", shadow);
        text.Add("Ga", Korean.Ga(shadow));
    }

    private Task Pick(int cut, FigureId figure)
    {
        _picks.Add(figure);
        if (cut + 1 < TheaterScenes.Cuts.Count) ShowCut(cut + 1);
        else ShowDescent();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ DESCENT / WHO / DEED / CONTRACT

    /// <summary>
    /// 섞인 결 ([확정] design log §2, implemented 2026-11-13): in a 한 계약, the other figure you resonated with most
    /// (ties: the one picked later) leaves one of its starter cards in your deck. It is not your figure's card, so
    /// playing it builds 한 like any other.
    /// </summary>
    private FigureId? MixedGrain(FigureId winner)
    {
        var others = _picks.Select((f, i) => (f, i)).Where(x => x.f != winner).ToList();
        if (others.Count == 0) return null;
        return others.GroupBy(x => x.f).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Max(x => x.i)).First().Key;
    }

    /// <summary>Most resonance wins; a 1:1:1 tie goes to the figure of the last cut (spec §4.1).</summary>
    private FigureId Winner()
    {
        var best = _figures.Select(f => (f, n: _picks.Count(p => p == f))).OrderByDescending(x => x.n).ToList();
        if (best.Count > 1 && best[0].n == best[1].n) return _picks[^1];
        return best[0].f;
    }

    private void ShowDescent()
    {
        var winner = Winner();
        CinemaScreen.Direct(this, Owner, new Shot(EventArt.Figure(winner), CinemaFx.Rise, _figures, winner, TextDelay: 3.4));
        var desc = L10NLookup($"{PageKey("DESCENT")}.description");
        AddShadow(desc, winner);
        desc.Add("Figure", FigureRegistry.Name(winner));
        SetEventState(desc, [Next("DESCENT", "WHO", () => ShowWho(winner))]);
    }

    private void ShowWho(FigureId winner)
    {
        CinemaScreen.Direct(this, Owner, new Shot(EventArt.Figure(winner), CinemaFx.Hold, Figure: winner));
        var desc = L10NLookup($"{PageKey("WHO")}.description");
        desc.Add("Figure", FigureRegistry.Name(winner));
        desc.Add("Intro", FigureRegistry.Text(winner, "INTRO").GetFormattedText());
        SetEventState(desc, [Next("WHO", "DEED", () => ShowDeed(winner))]);
    }

    /// <summary>Their real last moment next to the last words you chose (spec §4.1-5).</summary>
    private void ShowDeed(FigureId winner)
    {
        CinemaScreen.Direct(this, Owner, new Shot(EventArt.Figure(winner), CinemaFx.Hold, Figure: winner));
        var desc = L10NLookup($"{PageKey("DEED")}.description");
        desc.Add("Figure", FigureRegistry.Name(winner));
        desc.Add("Deed", FigureRegistry.Text(winner, "LAST_DEED").GetFormattedText());
        desc.Add("Words", TheaterScenes.Line(_scene, TheaterScenes.Cuts.Count - 1, _picks[^1]).GetFormattedText());
        SetEventState(desc, [Next("DEED", "TERMS", () => ShowContract(winner))]);
    }

    private void ShowContract(FigureId winner)
    {
        CinemaScreen.Direct(this, Owner, new Shot(EventArt.Figure(winner), CinemaFx.Hold, Figure: winner));
        var resonance = _picks.Count(p => p == winner);
        var contract = resonance >= TheaterScenes.Cuts.Count ? ContractType.Resonance : ContractType.Grudge;

        var desc = L10NLookup($"{PageKey("CONTRACT")}.description");
        desc.Add("Resonance", resonance);
        desc.Add("Contract", StageRules.ContractName(contract));
        desc.Add("ContractText", ContractText(contract));
        var mixed = contract == ContractType.Grudge ? MixedGrain(winner) : null;
        desc.Add("Mixed", mixed is { } m ? MixedText(m) : "");

        var tips = FigureRegistry.StarterCards(winner).Select(c => HoverTipFactory.FromCard(c)).ToList();
        var key = $"{PageKey("CONTRACT")}.options.TAKE";
        SetEventState(desc,
        [
            new EventOption(this, () => MakeContract(winner, contract, resonance),
                L10NLookup($"{key}.title"), L10NLookup($"{key}.description"), key, tips),
        ]);
    }

    private static string MixedText(FigureId mixed)
    {
        var text = FigureRegistry.Generic("MIXED_GRAIN");
        text.Add("Figure", FigureRegistry.Name(mixed));
        text.Add("Card", FigureRegistry.StarterCards(mixed)[0].Title);
        return "\n\n" + text.GetFormattedText();
    }

    /// <summary>What the contract does, with the numbers from the code (spec §4.2 / §4.3).</summary>
    private static string ContractText(ContractType contract)
    {
        var text = FigureRegistry.Generic($"CONTRACT_TEXT.{contract.ToString().ToUpperInvariant()}");
        text.Add("Sync", StageRules.SyncMastery);
        text.Add("SyncResonance", StageRules.SyncMasteryResonance);
        text.Add("Focus", StageRules.FocusMastery);
        text.Add("FocusResonance", StageRules.FocusMasteryResonance);
        text.Add("Strength", SoulCore.GrudgeStrength);
        text.Add("Limit", SoulCore.GrudgeLimit);
        text.Add("HpLoss", SoulCore.GrudgeHpLoss);
        return text.GetFormattedText();
    }

    // ------------------------------------------------------------------ CONTRACT

    private async Task MakeContract(FigureId figure, ContractType contract, int resonance)
    {
        var core = Core;
        if (core == null || Owner == null)
        {
            SetEventFinished(L10NLookup($"{PageKey("NO_CORE")}.description"));
            return;
        }

        // Already contracted (a second copy of the room in multiplayer): no second card pick.
        if (core.Figure != null || core.GetRunCounter(DoneKey) > 0)
        {
            await Leave();
            return;
        }

        // Game state first, then the (local, skippable) cinematic: nothing below depends on the animation's nodes.
        core.MakeContract(figure, contract, resonance);
        core.SetRunCounter(DoneKey, 1);
        core.SetRunCounter(SceneKey, Math.Max(0, TheaterScenes.All.ToList().IndexOf(_scene)));
        core.SetRunCounter(WordsKey, (int)_picks[^1] + 1);
        var results = new List<CardPileAddResult>();
        var starters = FigureRegistry.StarterCards(figure).ToList();
        if (contract == ContractType.Grudge && MixedGrain(figure) is { } mixed && FigureRegistry.StarterCards(mixed).Count > 0)
            starters.Add(FigureRegistry.StarterCards(mixed)[0]);
        foreach (var canonical in starters)
        {
            var card = Owner.RunState.CreateCard(canonical, Owner);
            results.Add(await CardPileCmd.Add(card, PileType.Deck));
        }
        await CinemaScreen.PlayMerge(this, Owner, figure);
        if (results.Count > 0) CardCmd.PreviewCardPileAdd(results);

        CinemaScreen.Direct(this, Owner, new Shot(EventArt.Figure(figure), TextDelay: 0.8));
        var desc = L10NLookup($"{PageKey("TEACH")}.description");
        var name = FigureRegistry.Name(figure);
        desc.Add("Figure", name);
        desc.Add("Ga", Korean.Ga(name));
        var key = $"{PageKey("TEACH")}.options.LEARN";
        SetEventState(desc, [new EventOption(this, () => Learn(figure), key)]);
    }

    private async Task Learn(FigureId figure)
    {
        if (Owner != null) await FigureCardPick.Offer(Owner, figure, new BlockingPlayerChoiceContext());
        var end = L10NLookup($"{PageKey("CONTRACTED")}.description");
        end.Add("Figure", FigureRegistry.Name(figure));
        end.Add("Contract", StageRules.ContractName(Core?.ContractKind ?? ContractType.None));
        CinemaScreen.Direct(this, Owner, new Shot(EventArt.Hall, CinemaFx.LightsOn, TextDelay: 1.2));
        SetEventFinished(end);
    }
}
