using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Extensions;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Tokens;

/// <summary>
/// Non-deck "choice" cards used only to show options in the game's card-selection screens
/// (SwordSwap target, rest-site "마검 강화" sword pick and mode pick). They live in the game's TokenCardPool
/// so they never appear in rewards, shops or random generation, and are hidden from the card library.
/// Their title is also used as the sword's display name (<see cref="SwordRegistry.DisplayName"/>).
/// </summary>
[Pool(typeof(TokenCardPool))]
public abstract class ChoiceTokenCard() : ConstructedCardModel(-1, CardType.Skill, CardRarity.Token, TargetType.None,
    showInCardLibrary: false)
{
    public override int MaxUpgradeLevel => 0;

    public override string CustomPortraitPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".BigCardImagePath();
    public override string PortraitPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();
    public override string BetaPortraitPath => $"beta/{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();

    /// <summary>Creates mutable copies owned by <paramref name="player"/> for a selection screen.</summary>
    public static List<CardModel> CreateForSelection(Player player, IEnumerable<CardModel> canonicalTokens)
    {
        var result = new List<CardModel>();
        foreach (var canonical in canonicalTokens)
        {
            var scope = CombatManager.Instance.IsInProgress ? player.Creature.CombatState : null;
            result.Add(scope != null ? scope.CreateCard(canonical, player) : player.RunState.CreateCard(canonical, player));
        }

        return result;
    }

    /// <summary>Unregisters selection copies again (they were never added to any pile).</summary>
    public static void DisposeSelection(Player player, IEnumerable<CardModel> created)
    {
        foreach (var card in created)
        {
            var scope = CombatManager.Instance.IsInProgress ? player.Creature.CombatState : null;
            if (scope != null) scope.RemoveCard(card);
            else if (player.RunState.ContainsCard(card)) player.RunState.RemoveCard(card);
        }
    }
}

/// <summary>A token that represents one sword.</summary>
public abstract class SwordTokenCard : ChoiceTokenCard
{
    protected SwordTokenCard()
    {
        // what level 5 gives ("검의 완성", Swords/SwordMastery.cs) + the legend card itself
        WithTips(card => SwordMastery.TokenTips(card, ((SwordTokenCard)card).Sword));
    }

    public abstract SwordId Sword { get; }

    /// <summary>Canonical token for a sword.</summary>
    public static CardModel CanonicalFor(SwordId sword) => sword switch
    {
        SwordId.Gram => ModelDb.Card<GramToken>(),
        SwordId.Ganjiang => ModelDb.Card<GanjiangToken>(),
        SwordId.Moye => ModelDb.Card<MoyeToken>(),
        SwordId.Kusanagi => ModelDb.Card<KusanagiToken>(),
        SwordId.Tyrfing => ModelDb.Card<TyrfingToken>(),
        SwordId.Dainsleif => ModelDb.Card<DainsleifToken>(),
        SwordId.Durandal => ModelDb.Card<DurandalToken>(),
        SwordId.Skofnung => ModelDb.Card<SkofnungToken>(),
        SwordId.Onimaru => ModelDb.Card<OnimaruToken>(),
        SwordId.ClaiomhSolais => ModelDb.Card<ClaiomhSolaisToken>(),
        SwordId.Caladbolg => ModelDb.Card<CaladbolgToken>(),
        _ => throw new ArgumentOutOfRangeException(nameof(sword), sword, "No token card for this sword"),
    };
}

public sealed class GramToken : SwordTokenCard { public override SwordId Sword => SwordId.Gram; }
public sealed class GanjiangToken : SwordTokenCard { public override SwordId Sword => SwordId.Ganjiang; }
public sealed class MoyeToken : SwordTokenCard { public override SwordId Sword => SwordId.Moye; }
public sealed class KusanagiToken : SwordTokenCard { public override SwordId Sword => SwordId.Kusanagi; }
public sealed class TyrfingToken : SwordTokenCard { public override SwordId Sword => SwordId.Tyrfing; }
public sealed class DainsleifToken : SwordTokenCard { public override SwordId Sword => SwordId.Dainsleif; }
public sealed class DurandalToken : SwordTokenCard { public override SwordId Sword => SwordId.Durandal; }
public sealed class SkofnungToken : SwordTokenCard { public override SwordId Sword => SwordId.Skofnung; }
public sealed class OnimaruToken : SwordTokenCard { public override SwordId Sword => SwordId.Onimaru; }
public sealed class ClaiomhSolaisToken : SwordTokenCard { public override SwordId Sword => SwordId.ClaiomhSolais; }
public sealed class CaladbolgToken : SwordTokenCard { public override SwordId Sword => SwordId.Caladbolg; }
