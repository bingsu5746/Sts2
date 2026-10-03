using MagicSwordsman.MagicSwordsmanCode.Cards.Tokens;
using MagicSwordsman.MagicSwordsmanCode.RestSite;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Localization;

namespace MagicSwordsman.MagicSwordsmanCode.Events.Forge;

// Choice cards of the per-sword forge story (see ForgeStory). They derive from the framework's token bases, so they
// live in TokenCardPool, are hidden from the library and never reach rewards. Text: cards.json (fragment g6) + the
// per-sword lines of SwordLore (events.json). Outside a selection (canonical copies) the sword-dependent arguments
// are empty strings so the text never fails to format.

/// <summary>Intro: "{Intro}\n\n{Level}단계 — {Stage}" with the sword's name as title.</summary>
public sealed class ForgeStoryIntroToken : ChoiceTokenCard
{
    public SwordId Sword { get; set; }
    public int Level { get; set; }

    public override string Title => IsMutable ? SwordLore.NameText(Sword) : base.Title;

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        if (!IsMutable)
        {
            description.Add("Intro", "");
            description.Add("Stage", "");
            description.Add("Level", 0m);
            return;
        }

        description.Add("Intro", SwordLore.Line(Sword, "FORGE_INTRO"));
        description.Add("Stage", SwordLore.StageName(Sword, Level));
        description.Add("Level", Level);
    }
}

/// <summary>Mode card with the sword's phrase: {Story} + the framework's numbers {Gain} {Success} {Failure} {Shatter}.</summary>
public abstract class ForgeStoryModeToken : ForgeModeToken
{
    public SwordId Sword { get; set; }

    /// <summary>Which SwordLore part is the phrase (FORGE_SAFE / FORGE_GAMBLE) or a generic line (FORGE_GAMBLE3).</summary>
    protected abstract LocString StoryLine();

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        if (!IsMutable)
        {
            description.Add("Story", "");
            description.Add("ShatterRule", "");
            return;
        }

        description.Add("Story", StoryLine());
        // Spec §4 [확정]: Gram does not disappear on a shatter, it goes back to level 0.
        description.Add("ShatterRule", SwordLore.Generic(
            SwordRegistry.GetDefinition(SwordRegistry.GroupLeader(Sword)).CanBeLost ? "SHATTER_RULE" : "SHATTER_RULE_RESET"));
    }
}

public sealed class ForgeStorySafeToken : ForgeStoryModeToken
{
    public override ForgeMode Mode => ForgeMode.Safe;
    protected override LocString StoryLine() => SwordLore.Line(Sword, "FORGE_SAFE");
}

public sealed class ForgeStoryGamble2Token : ForgeStoryModeToken
{
    public override ForgeMode Mode => ForgeMode.GamblePlus2;
    protected override LocString StoryLine() => SwordLore.Line(Sword, "FORGE_GAMBLE");
}

/// <summary>Content doc §5.1: the +3 gamble uses the same line for every sword ("모든 것을 건다.").</summary>
public sealed class ForgeStoryGamble3Token : ForgeStoryModeToken
{
    public override ForgeMode Mode => ForgeMode.GamblePlus3;
    protected override LocString StoryLine() => SwordLore.Generic("FORGE_GAMBLE3");
}

/// <summary>
/// Result: title per outcome kind (cards.json MAGICSWORDSMAN-FORGE_STORY_RESULT_TOKEN.&lt;KIND&gt;.title), description
/// "{Story}{Extra}\n\n{Result}" where Story is the sword's success / failure / shatter line, Extra the Gram level-5
/// Nibelung line (content doc §5.2) and Result the outcome line (&lt;KIND&gt;.text: {SwordName} {OldLevel} {NewLevel}
/// {Stage} {CurseName}).
/// </summary>
public sealed class ForgeStoryResultToken : ChoiceTokenCard
{
    public ForgeStoryResult Result { get; set; }

    private ForgeOutcome Outcome => Result.Outcome;

    private string KindKey => Outcome.Kind.ToString().ToUpperInvariant();

    public override string Title =>
        IsMutable && LocString.GetIfExists("cards", $"{Id.Entry}.{KindKey}.title") is { } t
            ? t.GetFormattedText()
            : base.Title;

    private string StoryPart => Outcome.Kind switch
    {
        ForgeOutcomeKind.Success => "FORGE_SUCCESS",
        ForgeOutcomeKind.Shatter or ForgeOutcomeKind.ShatterReset => "FORGE_SHATTER",
        _ => "FORGE_FAILURE",
    };

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        var resultText = IsMutable ? LocString.GetIfExists("cards", $"{Id.Entry}.{KindKey}.text") : null;
        if (resultText == null)
        {
            description.Add("Story", "");
            description.Add("Extra", "");
            description.Add("Result", "");
            return;
        }

        var sword = Outcome.Sword;
        resultText.Add("SwordName", SwordLore.Name(sword));
        resultText.Add("OldLevel", Outcome.OldLevel);
        resultText.Add("NewLevel", Outcome.NewLevel);
        resultText.Add("Stage", SwordLore.StageName(sword, Outcome.NewLevel));
        resultText.Add("CurseName", SwordRegistry.Get(sword).FailureCurse?.Title ?? "");

        description.Add("Story", SwordLore.Line(sword, StoryPart));
        description.Add("Extra", Result.GotNibelung ? SwordLore.Line(SwordId.Gram, "NIBELUNG") : SwordLore.Generic("EMPTY"));
        description.Add("Result", resultText);
    }
}
