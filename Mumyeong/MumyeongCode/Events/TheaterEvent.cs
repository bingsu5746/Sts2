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
/// Pages: START (blackout + the three shadows) -> CUT 0..2 (one option per shadow, the line of that figure for this
/// scene and cut) -> DESCENT (the figure with the most resonance steps into the screen) -> REVEAL (my three cuts vs
/// the figure's real last moment) -> contract: 코어 data, 2 starter cards, 1-of-3 figure card pick.
///
/// The three figures and the scene come from the event Rng (run seed + event id, mixed with a fixed key), so the same
/// seed shows the same theater. Not shared in multiplayer: every player gets their own copy (like 엔시페르's
/// TombResonanceEvent). Per-instance state: a save/reload while the event is open restarts it from START.
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

    // TODO(art): own theater portrait. Placeholder: the game's "Grave of the Forgotten" event art.
    public override string? CustomInitialPortraitPath => ImageHelper.GetImagePath("events/grave_of_the_forgotten.png");

    private string PageKey(string page) => $"{Id.Entry}.pages.{page}";

    private SoulCore? Core => Owner?.GetRelic<SoulCore>();

    public override LocString InitialDescription =>
        IsMutable && Owner != null ? StartDescription() : base.InitialDescription;

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

    // ------------------------------------------------------------------ START

    private LocString StartDescription()
    {
        var desc = L10NLookup($"{PageKey("INITIAL")}.description");
        desc.Add("Shadows", string.Join("\n", _figures.Select(f => $"— 「{FigureRegistry.Shadow(f)}」")));
        return desc;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, () => { ShowCut(0); return Task.CompletedTask; }, $"{PageKey("INITIAL")}.options.ROLL"),
    ];

    // ------------------------------------------------------------------ CUTS

    private void ShowCut(int cut)
    {
        var desc = L10NLookup($"{PageKey("CUT")}.description");
        desc.Add("Reaction", _picks.Count == 0 ? "" : Reaction(_picks[^1]));
        desc.Add("Scene", cut == 0 ? TheaterScenes.Description(_scene).GetFormattedText() + "\n\n" : "");
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

    // ------------------------------------------------------------------ DESCENT / REVEAL

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
        var desc = L10NLookup($"{PageKey("DESCENT")}.description");
        desc.Add("Reaction", Reaction(_picks[^1]));
        desc.Add("Shadow", FigureRegistry.Shadow(winner));
        desc.Add("Figure", FigureRegistry.Name(winner));
        SetEventState(desc,
        [
            new EventOption(this, () => { ShowReveal(winner); return Task.CompletedTask; },
                $"{PageKey("DESCENT")}.options.WATCH"),
        ]);
    }

    private void ShowReveal(FigureId winner)
    {
        var resonance = _picks.Count(p => p == winner);
        var contract = resonance >= TheaterScenes.Cuts.Count ? ContractType.Resonance : ContractType.Grudge;

        var desc = L10NLookup($"{PageKey("REVEAL")}.description");
        desc.Add("Figure", FigureRegistry.Name(winner));
        desc.Add("Mine", string.Join("\n", _picks.Select((p, i) =>
            $"{i + 1}. {TheaterScenes.Line(_scene, i, p).GetFormattedText()}")));
        desc.Add("Deed", FigureRegistry.Text(winner, "LAST_DEED").GetFormattedText());
        desc.Add("Resonance", resonance);
        desc.Add("Contract", Stages.StageRules.ContractName(contract));
        desc.Add("ContractText", FigureRegistry.Generic($"CONTRACT_TEXT.{contract.ToString().ToUpperInvariant()}").GetFormattedText());

        var tips = FigureRegistry.StarterCards(winner).Select(c => HoverTipFactory.FromCard(c)).ToList();
        var key = $"{PageKey("REVEAL")}.options.CONTRACT";
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
            core.MakeContract(figure, contract, resonance);
            var results = new List<CardPileAddResult>();
            foreach (var canonical in FigureRegistry.StarterCards(figure))
            {
                var card = Owner.RunState.CreateCard(canonical, Owner);
                results.Add(await CardPileCmd.Add(card, PileType.Deck));
            }
            if (results.Count > 0) CardCmd.PreviewCardPileAdd(results);
            await FigureCardPick.Offer(Owner, figure, new BlockingPlayerChoiceContext());
        }

        core.SetRunCounter(DoneKey, 1);
        var end = L10NLookup($"{PageKey("CONTRACTED")}.description");
        end.Add("Figure", FigureRegistry.Name(figure));
        end.Add("Contract", Stages.StageRules.ContractName(core.ContractKind));
        SetEventFinished(end);
    }
}
