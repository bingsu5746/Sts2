using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using Mumyeong.MumyeongCode.Character;
using Mumyeong.MumyeongCode.Extensions;
using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

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
    // Beta: cards without their own painting show their figure's emblem (images/card_portraits/figure_<key>.png).
    private string PortraitFile => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png";
    private string EmblemFile => $"figure_{(Figure is { } f ? FigureRegistry.Key(f).ToLowerInvariant() : "shared")}.png";
    public override string CustomPortraitPath => PortraitFile.BigCardImagePath(EmblemFile);
    public override string PortraitPath => PortraitFile.CardImagePath(EmblemFile);
    public override string BetaPortraitPath => $"beta/{PortraitFile}".CardImagePath(EmblemFile);

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

    // ------------------------------------------------------------------ helpers (BaseLib CommonActions + game commands)

    public const string Slash = "vfx/vfx_attack_slash";

    /// <summary>Attack with this card's Damage / CalculatedDamage var (single target or all enemies per TargetType).</summary>
    protected Task<AttackCommand> Hit(PlayerChoiceContext ctx, CardPlay cardPlay, int hits = 1) =>
        CommonActions.CardAttack(this, cardPlay, hits).WithHitFx(Slash).Execute(ctx);

    protected Task<decimal> GainBlock(CardPlay cardPlay) => CommonActions.CardBlock(this, cardPlay);

    protected Task<IEnumerable<CardModel>> DrawCards(PlayerChoiceContext ctx) => CommonActions.Draw(this, ctx);

    protected Task GainEnergy() => PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);

    protected decimal PowerAmount<T>() where T : PowerModel => DynamicVars[typeof(T).Name].BaseValue;

    /// <summary>Applies T to the owner with this card's var for T.</summary>
    protected Task Self<T>(PlayerChoiceContext ctx) where T : PowerModel =>
        PowerCmd.Apply<T>(ctx, Owner.Creature, PowerAmount<T>(), Owner.Creature, this);

    protected Task Self<T>(PlayerChoiceContext ctx, decimal amount) where T : PowerModel =>
        PowerCmd.Apply<T>(ctx, Owner.Creature, amount, Owner.Creature, this);

    /// <summary>Applies T to the card's target with this card's var for T.</summary>
    protected async Task OnTarget<T>(PlayerChoiceContext ctx, CardPlay cardPlay) where T : PowerModel
    {
        if (cardPlay.Target is { IsAlive: true } target)
            await PowerCmd.Apply<T>(ctx, target, PowerAmount<T>(), Owner.Creature, this);
    }

    /// <summary>Applies T to every hittable enemy with this card's var for T.</summary>
    protected async Task OnAllEnemies<T>(PlayerChoiceContext ctx) where T : PowerModel
    {
        var enemies = Enemies(this);
        if (enemies.Count > 0) await PowerCmd.Apply<T>(ctx, enemies, PowerAmount<T>(), Owner.Creature, this);
    }

    protected Task LoseHp(PlayerChoiceContext ctx, decimal amount) =>
        CreatureCmd.Damage(ctx, Owner.Creature, amount, DamageProps.cardHpLoss, this);

    public static List<Creature> Enemies(CardModel card) =>
        card.Owner?.Creature.CombatState?.HittableEnemies.ToList() ?? [];

    /// <summary>For calculated vars: 0 outside combat.</summary>
    public static decimal EnemyCount(CardModel card) => Enemies(card).Count;

    public static decimal OwnerBlock(CardModel card) => card.Owner?.Creature.Block ?? 0;

    public static decimal OwnerPower<T>(CardModel card) where T : PowerModel =>
        card.Owner?.Creature.GetPowerAmount<T>() ?? 0;
}

/// <summary>Shorthand base for a figure's card: <c>public sealed class X() : FigureCard(FigureId.Joan, 1, ...)</c>.</summary>
public abstract class FigureCard(FigureId figure, int cost, CardType type, CardRarity rarity, TargetType target)
    : MumyeongCard(cost, type, rarity, target)
{
    public override FigureId? Figure => figure;
}
