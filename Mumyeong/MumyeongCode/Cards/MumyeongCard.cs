using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using Mumyeong.MumyeongCode.Character;
using Mumyeong.MumyeongCode.Extensions;
using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Mumyeong.MumyeongCode.Cards;

/// <summary>
/// Base of every 무명 card.
/// - <see cref="Figure"/> null = shared card (공용), otherwise the card belongs to that figure: it is only offered in
///   rewards/shops while that figure is the contracted one (<see cref="SoulCore.IsCardAllowed"/>).
/// - Implement <see cref="OnCardPlay"/>, never OnPlay (sealed): playing a card of the contracted figure first counts one
///   mastery point on the 코어 (spec §2, §5), then runs the card's effect.
/// - Figure cards get the figure's colour on the card frame; shared cards use the colourless frame.
/// </summary>
[Pool(typeof(MumyeongCardPool))]
public abstract class MumyeongCard(int cost, CardType type, CardRarity rarity, TargetType target) :
    ConstructedCardModel(cost, type, rarity, target)
{
    // Image size: normal art 1000x760 (500x380 ok), full art 606x852; small variants 250x190 / 250x350.
    public override string CustomPortraitPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".BigCardImagePath();
    public override string PortraitPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();
    public override string BetaPortraitPath => $"beta/{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();

    public virtual FigureId? Figure => null;

    public override Godot.Material? CreateCustomFrameMaterial
    {
        get
        {
            if (Figure is not { } figure)
                return ModelDb.CardPool<MegaCrit.Sts2.Core.Models.CardPools.ColorlessCardPool>().FrameMaterial;
            var c = FigureRegistry.ColorOf(figure);
            return ShaderUtils.GenerateHsv(c.H <= 0.001f ? 1f : c.H, Math.Max(c.S, 0.15f) / 0.75f, Math.Max(c.V, 0.4f));
        }
    }

    protected sealed override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Figure is { } figure && Owner?.GetRelic<SoulCore>() is { } core) core.OnFigureCardPlayed(figure);
        await OnCardPlay(choiceContext, cardPlay);
    }

    protected abstract Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay);
}

/// <summary>Shorthand base for a figure's card: <c>public sealed class X() : FigureCard(FigureId.Joan, 1, ...)</c>.</summary>
public abstract class FigureCard(FigureId figure, int cost, CardType type, CardRarity rarity, TargetType target)
    : MumyeongCard(cost, type, rarity, target)
{
    public override FigureId? Figure => figure;
}
