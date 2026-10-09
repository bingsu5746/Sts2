using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Events;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MagicSwordsman.MagicSwordsmanCode.Visuals;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Union;

/// <summary>
/// 【조합】 card (user request 2026-10-09: "검 조합 카드"): a sword card that needs TWO swords in Mangeomchong.
///  - <see cref="MagicSwordCard.Sword"/> = <see cref="Primary"/>: playing the card switches to it first (as any sword
///    card), the card is stored / restored with it when that sword is lost / regained, and its play restrictions
///    (Skofnung turn 1, sealed Kusanagi...) apply.
///  - <see cref="Partner"/>: the card only appears in rewards, shops, transforms and random combat cards while BOTH
///    swords are owned (<see cref="UnionCatalog.IsAllowed"/>, called by Mangeomchong.IsCardAllowed). Playing the card
///    also summons the partner (and its pair: Ganjiang -> Moye too) if it is owned and not out yet.
///  - Numbers grow with the SUM of both swords' levels (<see cref="UnionLevel"/>, 0..10); each card states how.
///  - Losing only the partner keeps the card in the deck (it still works; it just cannot summon the partner).
///  - Presentation: a violet frame, a "조합: A + B" header line, a hover tip naming both swords and a card-specific
///    two-sword motion (<see cref="UnionMotion"/>).
/// Implement <see cref="OnUnionPlay"/> instead of OnCardPlay.
/// </summary>
public abstract class UnionCard : MagicSwordCard
{
    protected UnionCard(int cost, CardType type, CardRarity rarity, TargetType target)
        : base(cost, type, rarity, target)
    {
        WithTip(new TooltipSource(UnionCatalog.TipFor));
    }

    /// <summary>The sword this card switches to (its "own" sword).</summary>
    public abstract SwordId Primary { get; }

    /// <summary>The second sword the combination needs (Ganjiang stands for the Ganjiang/Moye pair).</summary>
    public abstract SwordId Partner { get; }

    /// <summary>Which two-sword choreography plays (Visuals/UnionMotion.cs).</summary>
    public abstract UnionMotionKind Motion { get; }

    /// <summary>The body clip played with the choreography (an existing AnimationPlayer clip name).</summary>
    public abstract string BodyClip { get; }

    /// <summary>
    /// Seconds from the start of the choreography to the moment the blades land. Attack cards pass it as their attack
    /// animation delay (the game deals the damage that long after the "Attack" trigger).
    /// </summary>
    public virtual float Impact => 0.3f;

    public sealed override SwordId? Sword => Primary;

    // ------------------------------------------------------------------ levels

    /// <summary>Level of Primary + level of Partner (0..10; 0 for canonical cards / outside a run).</summary>
    public int UnionLevel
    {
        get
        {
            if (!IsMutable || Owner is not { } owner) return 0;
            return SwordCombat.LevelOf(owner, Primary) + SwordCombat.LevelOf(owner, Partner);
        }
    }

    /// <summary>Per-hit damage added to THIS card's powered attacks: ⌊UnionLevel / divisor⌋ (0 = none).</summary>
    protected virtual int UnionDamageDivisor => 0;

    /// <summary>Block added to THIS card's block: ⌊UnionLevel / divisor⌋ (0 = none).</summary>
    protected virtual int UnionBlockDivisor => 0;

    /// <summary>Card-specific extra damage per hit (preview too: keep it pure).</summary>
    protected virtual decimal BonusDamage(Creature? target) => 0m;

    /// <summary>Text variable {name} = baseValue + ⌊UnionLevel / divisor⌋ (base value outside combat).</summary>
    protected void WithUnionVar(string name, int baseValue, int divisor) =>
        WithCalculatedVar(name, baseValue, 1, (card, _) => card is UnionCard u ? u.UnionLevel / divisor : 0);

    /// <summary>Play-time value of a <see cref="WithUnionVar"/> number.</summary>
    protected int UnionValue(int baseValue, int divisor) => baseValue + UnionLevel / divisor;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        var bonus = base.ModifyDamageAdditive(target, amount, props, dealer, cardSource);
        if (!ReferenceEquals(cardSource, this) || !props.IsPoweredAttack()) return bonus;
        if (UnionDamageDivisor > 0) bonus += UnionLevel / UnionDamageDivisor;
        return bonus + BonusDamage(target);
    }

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource,
        CardPlay? cardPlay)
    {
        var bonus = base.ModifyBlockAdditive(target, block, props, cardSource, cardPlay);
        if (ReferenceEquals(cardSource, this) && UnionBlockDivisor > 0) bonus += UnionLevel / UnionBlockDivisor;
        return bonus;
    }

    // ------------------------------------------------------------------ Dainsleif as the primary sword

    /// <summary>
    /// Same rule as DainsleifCard: the game picks the result pile BEFORE OnPlay, so a card that is about to switch to
    /// Dainsleif applies Dainsleif's return rule to itself (when Dainsleif is already current the behavior does it).
    /// </summary>
    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(CardModel card,
        bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
    {
        if (Primary != SwordId.Dainsleif || !ReferenceEquals(card, this) || Owner == null)
            return (pileType, position);
        var (effective, preview) = SwordCombat.EffectiveSwordFor(Owner, this);
        if (!preview || effective != SwordId.Dainsleif) return (pileType, position);
        return DainsleifReturn.Decide(Owner, this, resources, pileType, position, inherited: false);
    }

    // ------------------------------------------------------------------ play

    protected sealed override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await SummonPartner(choiceContext);
        await OnUnionPlay(choiceContext, cardPlay);
    }

    /// <summary>The card's own effect (after the switch to Primary and the partner's summon).</summary>
    protected abstract Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay);

    /// <summary>The partner (and its pair) comes out of Mangeomchong too, if owned and not out / sealed.</summary>
    private async Task SummonPartner(PlayerChoiceContext choiceContext)
    {
        var owner = Owner;
        var state = SwordCombat.Get(owner);
        var tomb = owner.GetRelic<Mangeomchong>();
        if (state == null || tomb == null) return;
        foreach (var s in SwordRegistry.WithPartners(Partner))
        {
            if (!tomb.Owns(s) || state.Present.Contains(s) || state.ReturnedToVault.Contains(s)) continue;
            await SwordCombat.Summon(owner, s, choiceContext);
        }
    }

    /// <summary>
    /// Starts the two-sword choreography + body clip now (skills, and attacks that do not use an attack command).
    /// Attack-command cards do NOT call this: MotionDirector starts it on the game's "Attack" trigger instead.
    /// </summary>
    protected void PlayMotion(Creature? target) => UnionMotion.Play(Owner, this, target);

    /// <summary>
    /// This card's attack command with the choreography's timing: the damage lands <see cref="Impact"/> seconds after
    /// the "Attack" trigger (which starts the choreography, see MotionDirector).
    /// </summary>
    protected AttackCommand UnionAttack(CardPlay cardPlay, int hits = 1) =>
        CommonActions.CardAttack(this, cardPlay, hits).WithAttackerAnim("Attack", Impact);

    /// <summary>Like <see cref="UnionAttack"/> with explicit damage / props (e.g. unblockable).</summary>
    protected AttackCommand UnionAttack(CardPlay cardPlay, decimal damage, ValueProp props, int hits = 1) =>
        CommonActions.CardAttack(this, cardPlay, cardPlay.Target, damage, props, hits).WithAttackerAnim("Attack", Impact);

    // ------------------------------------------------------------------ presentation

    /// <summary>Union cards share one violet frame so they stand out from single-sword cards.</summary>
    public override Godot.Material? CreateCustomFrameMaterial => ShaderUtils.GenerateHsv(0.76f, 0.75f, 0.95f);

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("UnionLevel", UnionLevel);
        description.Add("SwordA", UnionCatalog.NameOf(Primary));
        description.Add("SwordB", UnionCatalog.NameOf(Partner));
    }
}

/// <summary>
/// Lookup + rules for 【조합】 cards: the reward / shop / transform filter, the hover tips, and which cards a sword
/// acquisition unlocks (for the "new union" popup and the sword hover tips).
/// </summary>
public static class UnionCatalog
{
    private static List<UnionCard>? _all;

    /// <summary>Every canonical union card (read lazily: ModelDb must be initialised).</summary>
    public static IReadOnlyList<UnionCard> All
    {
        get
        {
            if (_all != null) return _all;
            try
            {
                _all = ModelDb.AllCards.OfType<UnionCard>().OrderBy(c => c.Rarity).ThenBy(c => c.Id.Entry).ToList();
            }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"[Union] could not list union cards: {e.Message}");
                return [];
            }

            return _all;
        }
    }

    /// <summary>Whether a sword (or its pair) is one of the card's two swords.</summary>
    public static bool Uses(UnionCard card, SwordId sword)
    {
        var group = SwordRegistry.WithPartners(sword);
        return group.Contains(card.Primary) || group.Contains(card.Partner);
    }

    /// <summary>Both swords owned (pairs: the leader stands for the pair).</summary>
    public static bool OwnsBoth(Mangeomchong tomb, UnionCard card) => tomb.Owns(card.Primary) && tomb.Owns(card.Partner);

    /// <summary>Mangeomchong filter: a union card is allowed only while both of its swords are owned.</summary>
    public static bool IsAllowed(Mangeomchong tomb, CardModel card) => card is not UnionCard u || OwnsBoth(tomb, u);

    /// <summary>Union cards that become available when <paramref name="acquired"/> joins the currently owned swords.</summary>
    public static List<UnionCard> UnlockedBy(Mangeomchong tomb, IEnumerable<SwordId> acquired)
    {
        var group = acquired.ToHashSet();
        return All.Where(c => (group.Contains(c.Primary) || group.Contains(c.Partner)) && OwnsBoth(tomb, c)).ToList();
    }

    /// <summary>Union cards that WOULD become available if <paramref name="sword"/> were acquired now.</summary>
    public static List<UnionCard> WouldUnlock(Mangeomchong tomb, SwordId sword)
    {
        var group = SwordRegistry.WithPartners(sword);
        return All.Where(c =>
        {
            if (group.Contains(c.Primary)) return tomb.Owns(c.Partner) && !group.Contains(c.Partner);
            if (group.Contains(c.Partner)) return tomb.Owns(c.Primary);
            return false;
        }).ToList();
    }

    /// <summary>Hover tips (the card itself) for a sword acquisition option. Never throws.</summary>
    public static IEnumerable<IHoverTip> TipsIfAcquired(Mangeomchong? tomb, SwordId sword)
    {
        if (tomb == null) return [];
        try
        {
            return WouldUnlock(tomb, sword).Select(c => HoverTipFactory.FromCard(c)).ToList();
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[Union] tips: {e.Message}");
            return [];
        }
    }

    /// <summary>
    /// One text block for a floating sword's hover tip: the union cards of this sword whose other sword is owned
    /// (or "none yet"). Empty when the sword has no union card at all. Never throws.
    /// </summary>
    public static string SwordTipLine(Player player, SwordId sword)
    {
        try
        {
            var tomb = player.GetRelic<Mangeomchong>();
            var mine = All.Where(c => Uses(c, sword)).ToList();
            if (tomb == null || mine.Count == 0) return "";
            var lines = mine.Select(c =>
            {
                var other = SwordRegistry.WithPartners(sword).Contains(c.Primary) ? c.Partner : c.Primary;
                var line = new LocString("card_keywords", OwnsBoth(tomb, c)
                    ? "MAGICSWORDSMAN-UNION.swordTipOpen"
                    : "MAGICSWORDSMAN-UNION.swordTipLocked");
                line.Add("Card", c.Title);
                line.Add("Sword", NameOf(other));
                return line.GetFormattedText();
            });
            return "\n" + new LocString("card_keywords", "MAGICSWORDSMAN-UNION.swordTipHeader").GetFormattedText() +
                   "\n" + string.Join("\n", lines);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[Union] sword tip: {e.Message}");
            return "";
        }
    }

    /// <summary>Display name of a sword for union texts (the pair leader -> "간장·막야").</summary>
    public static string NameOf(SwordId sword) => SwordLore.NameText(sword);

    /// <summary>The 【조합】 tip of a card: "조합: A + B" and the rule text.</summary>
    public static IHoverTip TipFor(CardModel card)
    {
        var title = new LocString("card_keywords", "MAGICSWORDSMAN-UNION.title");
        var desc = new LocString("card_keywords", "MAGICSWORDSMAN-UNION.description");
        if (card is UnionCard u)
        {
            desc.Add("SwordA", NameOf(u.Primary));
            desc.Add("SwordB", NameOf(u.Partner));
        }
        else
        {
            desc.Add("SwordA", "");
            desc.Add("SwordB", "");
        }

        string text;
        try { text = desc.GetFormattedText(); }
        catch (Exception) { text = ""; }
        return new HoverTip(title, text);
    }
}
