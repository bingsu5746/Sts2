using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Character;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Extensions;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards;

/// <summary>
/// Base class for every 마검사 card (common cards AND sword cards).
///
/// - <see cref="Sword"/>: null = common card (공용 카드). Otherwise the card belongs to that sword:
///   it only appears in rewards/shops while the sword is owned, playing it summons/switches to the sword,
///   it cannot be upgraded individually (MaxUpgradeLevel 0, spec §4) and scales with the sword's level.
/// - Implement <see cref="OnCardPlay"/> (NOT OnPlay): the framework first runs
///   <see cref="SwordCombat.OnSwordCardPlayed"/> (summon + switch), then your effect, so the new current
///   sword's effect already applies to this card.
/// - Level scaling: <see cref="WithDamagePerLevel"/> / <see cref="WithBlockPerLevel"/> add N per sword level to
///   this card's own attacks / block through the game's damage/block hooks (so the in-combat preview turns green
///   like Strength). For any other number use <see cref="WithLevelVar"/> + <see cref="LevelVar"/>.
/// </summary>
[Pool(typeof(MagicSwordsmanCardPool))]
public abstract class MagicSwordCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true,
    bool autoAdd = true)
    : ConstructedCardModel(cost, type, rarity, target, showInCardLibrary, autoAdd)
{
    private int _damagePerLevel;
    private int _blockPerLevel;

    // Image size: normal art 1000x760 (500x380 ok), full art 606x852; small variants 250x190 / 250x350.
    public override string CustomPortraitPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".BigCardImagePath();
    public override string PortraitPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();
    public override string BetaPortraitPath => $"beta/{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();

    /// <summary>The sword this card belongs to; null for common cards. Override in sword cards.</summary>
    public virtual SwordId? Sword => null;

    public bool IsSwordCard => Sword.HasValue;

    /// <summary>Spec §4: sword cards are never upgraded individually (the sword is upgraded instead).</summary>
    public override int MaxUpgradeLevel => IsSwordCard ? 0 : base.MaxUpgradeLevel;

    /// <summary>The owner's saved level (0..5) of this card's sword. 0 for common/canonical cards.</summary>
    public int SwordLevel
    {
        get
        {
            if (Sword is not { } sword || !IsMutable) return 0;
            var owner = Owner;
            return owner == null ? 0 : SwordCombat.LevelOf(owner, sword);
        }
    }

    /// <summary>base + perLevel * SwordLevel — use for numbers read at play time.</summary>
    protected int Scaled(int baseValue, int perLevel) => baseValue + perLevel * SwordLevel;

    // ------------------------------------------------------------------ builder helpers (call in constructor)

    /// <summary>This card's powered attack damage gets +N per sword level (applied via ModifyDamageAdditive).</summary>
    protected MagicSwordCard WithDamagePerLevel(int perLevel)
    {
        _damagePerLevel = perLevel;
        return this;
    }

    /// <summary>This card's block gets +N per sword level (applied via ModifyBlockAdditive).</summary>
    protected MagicSwordCard WithBlockPerLevel(int perLevel)
    {
        _blockPerLevel = perLevel;
        return this;
    }

    /// <summary>
    /// A level-scaled number for card text: shows base + perLevel*level in combat (BaseLib calculated var named
    /// <paramref name="name"/>, use {name} in the description). Read the value at play time with <see cref="LevelVar"/>.
    /// Outside combat the text shows the base value (CalculatedVar only evaluates in combat).
    /// </summary>
    protected MagicSwordCard WithLevelVar(string name, int baseValue, int perLevel)
    {
        WithCalculatedVar(name, baseValue, perLevel, static (card, _) => card is MagicSwordCard m ? m.SwordLevel : 0);
        return this;
    }

    /// <summary>Current value of a <see cref="WithLevelVar"/> number: base + perLevel * SwordLevel.</summary>
    protected int LevelVar(string name) =>
        DynamicVars[name + "Base"].IntValue + DynamicVars[name + "Extra"].IntValue * SwordLevel;

    public int DamagePerLevel => _damagePerLevel;
    public int BlockPerLevel => _blockPerLevel;

    // ------------------------------------------------------------------ play

    protected sealed override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await SwordCombat.OnSwordCardPlayed(this, choiceContext);
        await OnCardPlay(choiceContext, cardPlay);
    }

    /// <summary>The card's own effect. Runs after the sword switch.</summary>
    protected virtual Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;

    /// <summary>Sword play restrictions (e.g. Skofnung on turn 1) + <see cref="IsPlayableExtra"/>.</summary>
    protected override bool IsPlayable => SwordCombat.CanPlaySwordCard(this) && IsPlayableExtra;

    /// <summary>Extra card-specific playability condition.</summary>
    protected virtual bool IsPlayableExtra => true;

    // ------------------------------------------------------------------ level scaling via hooks
    // Cards in combat piles receive combat hooks (CardModel.ShouldReceiveCombatHooks), so the bonus applies to
    // the real damage AND to the hand preview.

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        if (_damagePerLevel == 0 || !ReferenceEquals(cardSource, this) || !props.IsPoweredAttack()) return 0m;
        return _damagePerLevel * SwordLevel;
    }

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (_blockPerLevel == 0 || !ReferenceEquals(cardSource, this)) return 0m;
        return _blockPerLevel * SwordLevel;
    }

    // ------------------------------------------------------------------ text

    /// <summary>Adds {SwordLevel}, {SwordName}, {IsSwordCard} to every card description.</summary>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("SwordLevel", SwordLevel);
        description.Add("SwordName", Sword is { } s ? SwordRegistry.DisplayName(s) : "");
        description.Add("IsSwordCard", IsSwordCard);
    }
}
