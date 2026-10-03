using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Extensions;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace MagicSwordsman.MagicSwordsmanCode.Curses;

/// <summary>
/// Base class for per-sword curse cards (forge failure curses, Gram's "니벨룽의 보물", Tyrfing's run curses...).
/// User decision: every sword has its own, different curse.
///
/// Curses are put in the game's TokenCardPool on purpose: the game draws random curses from CurseCardPool
/// (NeowsBones, SereTalon, CursedRun), and sword curses must never show up there or for other characters.
/// Default: cost -1, Curse type/rarity, Unplayable (like the game's Injury). For a playable curse
/// (e.g. 니벨룽의 보물 "쓰면 골드") pass playable: true and override <see cref="OnCursePlayed"/>.
/// To hook it up: return ModelDb.Card&lt;YourCurse&gt;() from YourSwordBehavior.FailureCurse.
/// </summary>
[Pool(typeof(TokenCardPool))]
public abstract class SwordCurseCard : ConstructedCardModel
{
    protected SwordCurseCard(bool playable = false, int cost = -1, TargetType target = TargetType.None)
        : base(cost, CardType.Curse, CardRarity.Curse, target)
    {
        if (!playable) WithKeywords(CardKeyword.Unplayable);
        WithTips(Cards.MagicSwordsmanKeywords.TipsFor); // mod term tooltips (e.g. 주인 없는 칼 -> 명령)
    }

    /// <summary>The sword this curse belongs to (informational; used by content and UI).</summary>
    public abstract SwordId Sword { get; }

    public override int MaxUpgradeLevel => 0;

    public override string CustomPortraitPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".BigCardImagePath();
    public override string PortraitPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();
    public override string BetaPortraitPath => $"beta/{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();

    protected sealed override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        OnCursePlayed(choiceContext, cardPlay);

    /// <summary>Only for playable curses.</summary>
    protected virtual Task OnCursePlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
}
