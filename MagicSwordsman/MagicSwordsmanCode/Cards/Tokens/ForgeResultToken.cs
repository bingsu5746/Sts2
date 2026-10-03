using MagicSwordsman.MagicSwordsmanCode.RestSite;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Localization;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Tokens;

/// <summary>
/// Shown after a "마검 강화" attempt so the player sees the result (click to continue).
/// Title and text depend on <see cref="Outcome"/>:
///   cards.json keys  MAGICSWORDSMAN-FORGE_RESULT_TOKEN.&lt;KIND&gt;.title  and  .&lt;KIND&gt;.text
///   (KIND = ForgeOutcomeKind upper-case) with {SwordName} {OldLevel} {NewLevel}.
/// </summary>
public sealed class ForgeResultToken : ChoiceTokenCard
{
    public ForgeOutcome Outcome { get; set; }

    private string KindKey => Outcome.Kind.ToString().ToUpperInvariant();

    public override string Title =>
        IsMutable && LocString.GetIfExists("cards", $"{Id.Entry}.{KindKey}.title") is { } t
            ? t.GetFormattedText()
            : base.Title;

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        var text = IsMutable ? LocString.GetIfExists("cards", $"{Id.Entry}.{KindKey}.text") : null;
        if (text != null)
        {
            text.Add("SwordName", SwordRegistry.DisplayName(Outcome.Sword));
            text.Add("OldLevel", Outcome.OldLevel);
            text.Add("NewLevel", Outcome.NewLevel);
            description.Add("Result", text);
        }
        else
        {
            description.Add("Result", "");
        }
    }
}
