using BaseLib.Abstracts;
using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Relics;
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
/// Pages (2026-10-24: little text per page, a background per page, a cinematic for the contract):
///   INITIAL (hall) -> SHADOWS (hall: the three epithets) -> SCENE (scene background) -> CUT 0..2 (one option per
///   shadow) -> DESCENT (figure background, the shadow rises into the screen and becomes the spirit) -> WHO (who they
///   were) -> DEED (their real last moment) -> CONTRACT (resonance, contract type) -> the spirit rushes into you ->
///   TEACH (figure background) -> 1-of-3 figure card pick -> CONTRACTED.
/// Winner: the most resonance; a 1:1:1 tie goes to the figure of the last cut (유언), which the WORDS prompt says.
///
/// The three figures and the scene come from the event Rng (run seed + event id, mixed with a fixed key), so the same
/// seed shows the same theater. Not shared in multiplayer: every player gets their own copy (like 엔시페르's
/// TombResonanceEvent). Per-instance state: a save/reload while the event is open restarts it from INITIAL.
/// </summary>
public sealed class TheaterEvent : CustomEventModel
{
    public const int ShadowCount = 3;
    public const string DoneKey = "event.theater";

    private List<FigureId> _figures = new();
    private string _scene = TheaterScenes.All[0];
    private readonly List<FigureId> _picks = new();

    public TheaterEvent() : base(autoAdd: false)
    {
    }

    public override bool IsAllowed(IRunState runState) => false;

    public override string? CustomInitialPortraitPath => EventArt.Resolve(EventArt.Hall);

    private string PageKey(string page) => $"{Id.Entry}.pages.{page}";

    private SoulCore? Core => Owner?.GetRelic<SoulCore>();

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
        return Task.CompletedTask;
    }

    private EventOption Next(string page, string option, Action show) =>
        new(this, () => { show(); return Task.CompletedTask; }, $"{PageKey(page)}.options.{option}");

    // ------------------------------------------------------------------ INITIAL / SHADOWS / SCENE

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [Next("INITIAL", "ROLL", ShowShadows)];

    private void ShowShadows()
    {
        EventArt.Show(Owner, EventArt.Hall);
        var desc = L10NLookup($"{PageKey("SHADOWS")}.description");
        desc.Add("Shadows", string.Join("\n", _figures.Select(f => $"[gold]「{FigureRegistry.Shadow(f)}」[/gold]")));
        SetEventState(desc, [Next("SHADOWS", "START", ShowScene)]);
    }

    private void ShowScene()
    {
        EventArt.Show(Owner, EventArt.Scene(_scene));
        var desc = L10NLookup($"{PageKey("SCENE")}.description");
        desc.Add("Scene", TheaterScenes.Description(_scene).GetFormattedText());
        SetEventState(desc, [Next("SCENE", "FREEZE", () => ShowCut(0))]);
    }

    // ------------------------------------------------------------------ CUTS

    private void ShowCut(int cut)
    {
        var desc = L10NLookup($"{PageKey("CUT")}.description");
        desc.Add("Reaction", _picks.Count == 0 ? "" : Reaction(_picks[^1]));
        desc.Add("Prompt", TheaterScenes.Generic($"PROMPT.{TheaterScenes.Cuts[cut]}").GetFormattedText());

        var options = new List<EventOption>();
        foreach (var figure in _figures)
        {
            var f = figure;
            var body = TheaterScenes.Generic("REACTS");
            body.Add("Shadow", FigureRegistry.Shadow(f));
            options.Add(new EventOption(this, () => Pick(cut, f), TheaterScenes.Line(_scene, cut, f), body,
                $"{PageKey("CUT")}.options.LINE", []));
        }

        SetEventState(desc, options);
    }

    private static string Reaction(FigureId figure)
    {
        var line = TheaterScenes.Generic("REACTION");
        line.Add("Shadow", FigureRegistry.Shadow(figure));
        return line.GetFormattedText() + "\n\n";
    }

    private Task Pick(int cut, FigureId figure)
    {
        _picks.Add(figure);
        if (cut + 1 < TheaterScenes.Cuts.Count) ShowCut(cut + 1);
        else ShowDescent();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ DESCENT / WHO / DEED / CONTRACT

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
        EventArt.Show(Owner, EventArt.Figure(winner));
        EventArt.PlayRise(Owner, winner);
        var desc = L10NLookup($"{PageKey("DESCENT")}.description");
        desc.Add("Shadow", FigureRegistry.Shadow(winner));
        desc.Add("Figure", FigureRegistry.Name(winner));
        SetEventState(desc, [Next("DESCENT", "WHO", () => ShowWho(winner))]);
    }

    private void ShowWho(FigureId winner)
    {
        var desc = L10NLookup($"{PageKey("WHO")}.description");
        desc.Add("Figure", FigureRegistry.Name(winner));
        desc.Add("Intro", FigureRegistry.Text(winner, "INTRO").GetFormattedText());
        SetEventState(desc, [Next("WHO", "DEED", () => ShowDeed(winner))]);
    }

    private void ShowDeed(FigureId winner)
    {
        var desc = L10NLookup($"{PageKey("DEED")}.description");
        desc.Add("Figure", FigureRegistry.Name(winner));
        desc.Add("Deed", FigureRegistry.Text(winner, "LAST_DEED").GetFormattedText());
        SetEventState(desc, [Next("DEED", "TERMS", () => ShowContract(winner))]);
    }

    private void ShowContract(FigureId winner)
    {
        var resonance = _picks.Count(p => p == winner);
        var contract = resonance >= TheaterScenes.Cuts.Count ? ContractType.Resonance : ContractType.Grudge;

        var desc = L10NLookup($"{PageKey("CONTRACT")}.description");
        desc.Add("Resonance", resonance);
        desc.Add("Contract", Stages.StageRules.ContractName(contract));
        desc.Add("ContractText", FigureRegistry.Generic($"CONTRACT_TEXT.{contract.ToString().ToUpperInvariant()}").GetFormattedText());

        var tips = FigureRegistry.StarterCards(winner).Select(c => HoverTipFactory.FromCard(c)).ToList();
        var key = $"{PageKey("CONTRACT")}.options.TAKE";
        SetEventState(desc,
        [
            new EventOption(this, () => MakeContract(winner, contract, resonance),
                L10NLookup($"{key}.title"), L10NLookup($"{key}.description"), key, tips),
        ]);
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

        if (core.Figure == null)
        {
            await EventArt.PlayMerge(Owner, figure);
            core.MakeContract(figure, contract, resonance);
            var results = new List<CardPileAddResult>();
            foreach (var canonical in FigureRegistry.StarterCards(figure))
            {
                var card = Owner.RunState.CreateCard(canonical, Owner);
                results.Add(await CardPileCmd.Add(card, PileType.Deck));
            }
            if (results.Count > 0) CardCmd.PreviewCardPileAdd(results);
        }

        core.SetRunCounter(DoneKey, 1);
        EventArt.Show(Owner, EventArt.Figure(figure));
        var desc = L10NLookup($"{PageKey("TEACH")}.description");
        desc.Add("Figure", FigureRegistry.Name(figure));
        var key = $"{PageKey("TEACH")}.options.LEARN";
        SetEventState(desc, [new EventOption(this, () => Learn(figure), key)]);
    }

    private async Task Learn(FigureId figure)
    {
        if (Owner != null) await FigureCardPick.Offer(Owner, figure, new BlockingPlayerChoiceContext());
        var end = L10NLookup($"{PageKey("CONTRACTED")}.description");
        end.Add("Figure", FigureRegistry.Name(figure));
        end.Add("Contract", Stages.StageRules.ContractName(Core?.ContractKind ?? ContractType.None));
        SetEventFinished(end);
    }
}
