using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// "현재 검" — the visible marker of the player's current sword, and the dispatcher that forwards combat hooks
/// to the current sword's <see cref="SwordBehavior"/> (and, for Kusanagi, to the inherited sword at half strength).
///
/// The power holds no sword state itself: it reads <see cref="SwordCombat"/> every time, so it can never get
/// out of sync. Applied/removed only by <see cref="SwordCombat.EnsureCurrentSwordPower"/>.
///
/// Localization: base keys (title/description/smartDescription) are the fallback. Per-sword text uses
///   MAGICSWORDSMAN-CURRENT_SWORD_POWER.&lt;SWORD&gt;.title / .description / .smartDescription
///   (SWORD = SwordId upper-case, e.g. GRAM, CLAIOMHSOLAIS) with variables {Level}, {HalfLevel}, {SwordName},
///   {InheritedName}, {Amount}. In combat the game shows smartDescription (vars come from DynamicVars).
/// </summary>
public sealed class CurrentSwordPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    private const string LevelVar = "Level";
    private const string HalfLevelVar = "HalfLevel";
    private const string SwordNameVar = "SwordName";
    private const string InheritedNameVar = "InheritedName";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(LevelVar, 0m),
        new DynamicVar(HalfLevelVar, 0m),
        new StringVar(SwordNameVar, "-"),
        new StringVar(InheritedNameVar, "-"),
    ];

    private Player? OwnerPlayer => IsMutable ? Owner?.Player : null;

    private SwordId? CurrentSword => OwnerPlayer is { } p ? SwordCombat.CurrentSword(p) : null;

    /// <summary>The number on the icon is the current sword's upgrade level.</summary>
    public override int DisplayAmount =>
        OwnerPlayer is { } p && CurrentSword is { } s ? SwordCombat.LevelOf(p, s) : 0;

    public override LocString Title
    {
        get
        {
            if (CurrentSword is { } s && LocString.Exists("powers", SwordKey(s, "title")))
                return new LocString("powers", SwordKey(s, "title"));
            return base.Title;
        }
    }

    public override LocString Description
    {
        get
        {
            LocString loc;
            if (CurrentSword is { } s && LocString.Exists("powers", SwordKey(s, "description")))
                loc = new LocString("powers", SwordKey(s, "description"));
            else
                loc = base.Description;
            AddSwordVars(loc);
            return loc;
        }
    }

    /// <summary>Per-sword smart description when it exists. Also refreshes the description variables.</summary>
    protected override string SmartDescriptionLocKey
    {
        get
        {
            UpdateVars();
            if (CurrentSword is { } s && LocString.Exists("powers", SwordKey(s, "smartDescription")))
                return SwordKey(s, "smartDescription");
            return base.SmartDescriptionLocKey;
        }
    }

    private void UpdateVars()
    {
        if (!IsMutable) return;
        var player = OwnerPlayer;
        var sword = CurrentSword;
        var level = player != null && sword != null ? SwordCombat.LevelOf(player, sword.Value) : 0;
        DynamicVars[LevelVar].BaseValue = level;
        DynamicVars[HalfLevelVar].BaseValue = level <= 0 ? 0 : Math.Max(1, level / 2);
        if (DynamicVars[SwordNameVar] is StringVar sn)
            sn.StringValue = sword != null ? SwordRegistry.DisplayName(sword.Value) : "-";
        var inherited = player != null && sword != null
            ? SwordRegistry.Get(sword.Value).GetInheritedSword(SwordCombat.ContextFor(player, sword.Value))
            : null;
        if (DynamicVars[InheritedNameVar] is StringVar inh)
            inh.StringValue = inherited != null ? SwordRegistry.DisplayName(inherited.Value) : "-";
    }

    private string SwordKey(SwordId sword, string field) => $"{Id.Entry}.{sword.ToString().ToUpperInvariant()}.{field}";

    private void AddSwordVars(LocString loc)
    {
        var player = OwnerPlayer;
        var sword = CurrentSword;
        var level = player != null && sword != null ? SwordCombat.LevelOf(player, sword.Value) : 0;
        loc.Add("Level", level);
        loc.Add("HalfLevel", level <= 0 ? 0 : Math.Max(1, level / 2));
        loc.Add("SwordName", sword != null ? SwordRegistry.DisplayName(sword.Value) : "-");
        var inherited = player != null && sword != null
            ? SwordRegistry.Get(sword.Value).GetInheritedSword(SwordCombat.ContextFor(player, sword.Value))
            : null;
        loc.Add("InheritedName", inherited != null ? SwordRegistry.DisplayName(inherited.Value) : "-");
    }

    /// <summary>Called after a switch so the icon number / tooltip update.</summary>
    public void Refresh()
    {
        UpdateVars();
        InvokeDisplayAmountChanged();
    }

    /// <summary>
    /// The current sword's behavior, then (Kusanagi) the inherited one flagged IsInherited.
    /// For number hooks with a card source, nothing is returned while that card is a not-yet-current sword card in
    /// hand (Mangeomchong previews the sword the card would switch to instead — see SwordCombat.EffectiveSwordFor).
    /// </summary>
    private IEnumerable<(SwordBehavior Behavior, SwordContext Ctx)> Active(CardModel? cardSource = null)
    {
        var player = OwnerPlayer;
        if (player == null) return [];
        var (sword, preview) = SwordCombat.EffectiveSwordFor(player, cardSource);
        if (sword is not { } current || preview) return [];
        return SwordCombat.CurrentEffects(player, current, preview: false);
    }

    // ------------------------------------------------------------------ forwarded hooks

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        decimal sum = 0m;
        foreach (var (b, ctx) in Active(cardSource)) sum += b.ModifyDamageAdditive(ctx, target, amount, props, dealer, cardSource);
        return sum;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        decimal mult = 1m;
        foreach (var (b, ctx) in Active(cardSource))
            mult *= b.ModifyDamageMultiplicative(ctx, target, amount, props, dealer, cardSource);
        return mult;
    }

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource,
        CardPlay? cardPlay)
    {
        decimal sum = 0m;
        foreach (var (b, ctx) in Active(cardSource)) sum += b.ModifyBlockAdditive(ctx, target, block, props, cardSource, cardPlay);
        return sum;
    }

    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        decimal mult = 1m;
        foreach (var (b, ctx) in Active(cardSource))
            mult *= b.ModifyBlockMultiplicative(ctx, target, block, props, cardSource, cardPlay);
        return mult;
    }

    public override int ModifyAttackHitCount(AttackCommand attack, int hitCount)
    {
        foreach (var (b, ctx) in Active()) hitCount = b.ModifyAttackHitCount(ctx, attack, hitCount);
        return hitCount;
    }

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(CardModel card,
        bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
    {
        var result = (pileType, position);
        foreach (var (b, ctx) in Active())
            result = b.ModifyCardPlayResultPileTypeAndPosition(ctx, card, isAutoPlay, resources, result.pileType,
                result.position);
        return result;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        foreach (var (b, ctx) in Active().ToList()) await b.BeforeCardPlayed(ctx, cardPlay);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        foreach (var (b, ctx) in Active().ToList()) await b.AfterCardPlayed(ctx, choiceContext, cardPlay);
    }

    public override async Task BeforeAttack(AttackCommand command)
    {
        foreach (var (b, ctx) in Active().ToList()) await b.BeforeAttack(ctx, command);
    }

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        foreach (var (b, ctx) in Active().ToList()) await b.AfterAttack(ctx, choiceContext, command);
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        foreach (var (b, ctx) in Active().ToList())
            await b.AfterDamageGiven(ctx, choiceContext, dealer, result, props, target, cardSource);
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        foreach (var (b, ctx) in Active().ToList())
            await b.AfterDamageReceived(ctx, choiceContext, target, result, props, dealer, cardSource);
    }
}
