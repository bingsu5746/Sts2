using Godot;
using Mumyeong.MumyeongCode.Cards;
using Mumyeong.MumyeongCode.Cards.Genghis;
using Mumyeong.MumyeongCode.Cards.Guanyu;
using Mumyeong.MumyeongCode.Cards.Joan;
using Mumyeong.MumyeongCode.Cards.Leonidas;
using Mumyeong.MumyeongCode.Cards.Napoleon;
using Mumyeong.MumyeongCode.Cards.YiSunsin;
using Mumyeong.MumyeongCode.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace Mumyeong.MumyeongCode.Figures;

/// <summary>
/// Static facts about every figure (spec §3, §5.2). Text lives in the "events" table under
/// <c>MUMYEONG-FIGURE.&lt;KEY&gt;.*</c> (NAME, SHADOW, LAST_DEED, MEMORY_1..3, TASK).
/// </summary>
public static class FigureRegistry
{
    public const string Table = "events";
    public const string Prefix = "MUMYEONG-FIGURE";

    public static readonly IReadOnlyList<FigureId> All =
    [
        FigureId.Guanyu, FigureId.Joan, FigureId.Leonidas, FigureId.Genghis, FigureId.YiSunsin, FigureId.Napoleon,
    ];

    /// <summary>Upper-case key used in localization keys: GUANYU, JOAN, LEONIDAS, GENGHIS, YISUNSIN, NAPOLEON.</summary>
    public static string Key(FigureId figure) => figure.ToString().ToUpperInvariant();

    public static bool IsValid(int raw) => Enum.IsDefined(typeof(FigureId), raw);

    /// <summary>Card frame / accent colour of the figure.</summary>
    public static Color ColorOf(FigureId figure) => figure switch
    {
        FigureId.Guanyu => new Color("2f8f5b"),   // green dragon
        FigureId.Joan => new Color("d9c46a"),     // banner gold
        FigureId.Leonidas => new Color("b5462f"), // spartan crimson
        FigureId.Genghis => new Color("8a6a3c"),  // steppe leather
        FigureId.YiSunsin => new Color("3a5f9e"), // sea blue
        FigureId.Napoleon => new Color("5b4a9e"), // imperial violet
        _ => new Color("888888"),
    };

    /// <summary>The 2 starting cards the figure puts into the deck when the contract is made (spec §1).</summary>
    public static IReadOnlyList<CardModel> StarterCards(FigureId figure) => figure switch
    {
        FigureId.Guanyu => [ModelDb.Card<GuanyuCrescentBlade>(), ModelDb.Card<GuanyuLoneRider>()],
        FigureId.Joan => [ModelDb.Card<JoanRaiseBanner>(), ModelDb.Card<JoanMaidensSword>()],
        FigureId.Leonidas => [ModelDb.Card<LeonidasShieldWall>(), ModelDb.Card<LeonidasSpearThrust>()],
        FigureId.Genghis => [ModelDb.Card<GenghisMountedArcher>(), ModelDb.Card<GenghisCavalryCharge>()],
        FigureId.YiSunsin => [ModelDb.Card<YiCheonjaCannon>(), ModelDb.Card<YiPanokseon>()],
        FigureId.Napoleon => [ModelDb.Card<NapoleonCannon>(), ModelDb.Card<NapoleonOrders>()],
        _ => [],
    };

    /// <summary>The figure's own relic, given at 강림 (spec §5.2).</summary>
    public static RelicModel DescentRelic(FigureId figure) => figure switch
    {
        FigureId.Guanyu => ModelDb.Relic<GuanyuGreenDragonRelic>(),
        FigureId.Joan => ModelDb.Relic<JoanBannerRelic>(),
        FigureId.Leonidas => ModelDb.Relic<LeonidasShieldRelic>(),
        FigureId.Genghis => ModelDb.Relic<GenghisSealRelic>(),
        FigureId.YiSunsin => ModelDb.Relic<YiTurtleShipRelic>(),
        FigureId.Napoleon => ModelDb.Relic<NapoleonCrownRelic>(),
        _ => throw new ArgumentOutOfRangeException(nameof(figure)),
    };

    /// <summary>True when the card belongs to the given figure.</summary>
    public static bool IsCardOf(CardModel card, FigureId figure) => card is MumyeongCard { Figure: { } f } && f == figure;

    public static LocString Text(FigureId figure, string part) =>
        LocString.GetIfExists(Table, $"{Prefix}.{Key(figure)}.{part}") ?? new LocString(Table, $"{Prefix}.EMPTY");

    public static LocString Generic(string part) => new(Table, $"{Prefix}.{part}");

    public static string Name(FigureId figure) => Text(figure, "NAME").GetFormattedText();

    /// <summary>한 키워드 shown above the figure's shadow in the theater (game fiction, not a historical claim).</summary>
    public static string Shadow(FigureId figure) => Text(figure, "SHADOW").GetFormattedText();
}
