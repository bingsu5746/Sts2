using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

/// <summary>
/// Base of every 클라이브 솔라시 card: Sword = ClaiomhSolais, Light helpers, a card-specific damage/block bonus that
/// also shows in the combat preview, and a level-threshold cost ("3단계부터 비용 0").
/// </summary>
public abstract class SolaisCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : MagicSwordCard(cost, type, rarity, target)
{
    public sealed override SwordId? Sword => SwordId.ClaiomhSolais;

    /// <summary>The owner's current Light (0 outside combat / for canonical cards).</summary>
    protected int Light => IsMutable && Owner?.PlayerCombatState != null ? SolaisLight.Get(Owner) : 0;

    /// <summary>Extra damage per hit of THIS card's powered attacks (evaluated for the preview too: keep it pure).</summary>
    protected virtual decimal BonusDamage(Creature? target) => 0m;

    /// <summary>Extra block of THIS card (preview too: keep it pure).</summary>
    protected virtual decimal BonusBlock() => 0m;

    /// <summary>Cost override for the current sword level; null = printed cost.</summary>
    protected virtual int? CostAtLevel(int level) => null;

    /// <summary>Text variable {name} = baseValue + perStep * ⌈L/2⌉ (the level part of 발도's "K"; base out of combat).</summary>
    protected SolaisCard WithLightDamageVar(string name, int baseValue, int perStep)
    {
        WithCalculatedVar(name, baseValue, perStep,
            static (card, _) => card is MagicSwordCard m ? SolaisLight.LevelBonus(m.SwordLevel) : 0);
        return this;
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        var bonus = base.ModifyDamageAdditive(target, amount, props, dealer, cardSource);
        if (ReferenceEquals(cardSource, this) && props.IsPoweredAttack()) bonus += BonusDamage(target);
        return bonus;
    }

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource,
        CardPlay? cardPlay)
    {
        var bonus = base.ModifyBlockAdditive(target, block, props, cardSource, cardPlay);
        if (ReferenceEquals(cardSource, this)) bonus += BonusBlock();
        return bonus;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) || CostAtLevel(SwordLevel) is not { } c || c >= originalCost) return false;
        modifiedCost = c;
        return true;
    }
}

/// <summary>
/// 【발도】 (draw-cut) card: consumes ALL Light; this card's damage ignores Block.
/// Damage = printed damage (+level) + Light × K × <see cref="LightMultiplier"/>, where K = 4,5,5,6,6,7 by Claíomh
/// Solais level (content doc §1.1). The Light part is added through this card's own damage hook, so the hand
/// preview shows the full number; the Light is removed right after the attack.
/// Ignoring Block: the attack is made with ValueProp.Move | ValueProp.Unblockable (BaseLib
/// AttackCommandExtensions.WithValueProp, used by CommonActions.CardAttack) — the game's own unblockable flag
/// (Creature.DamageBlockInternal returns 0 blocked damage for it). Still a powered attack (Strength etc. apply).
/// Implement <see cref="OnDrawCut"/> instead of OnCardPlay.
/// </summary>
public abstract class SolaisDrawCutCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : SolaisCard(cost, type, rarity, target)
{
    public const ValueProp DrawCutProps = ValueProp.Move | ValueProp.Unblockable;

    /// <summary>Light damage multiplier: 1 normally, 2 for 마그 투이레드, 0 for cards that convert Light otherwise.</summary>
    protected virtual int LightMultiplier => 1;

    protected int DamagePerLight => SolaisLight.DamagePerLight(SwordLevel) * LightMultiplier;

    protected override decimal BonusDamage(Creature? target) => Light * DamagePerLight;

    protected sealed override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await OnDrawCut(choiceContext, cardPlay);
        var consumed = await SolaisLight.ConsumeAll(Owner);
        await AfterLightConsumed(choiceContext, cardPlay, consumed);

        var silverArm = Owner.Creature.GetPower<SolaisSilverArmPower>();
        if (silverArm != null) await silverArm.OnDrawCut(choiceContext);
    }

    /// <summary>The card's attack (Light is still there, so the damage includes it). Use <see cref="DrawCutAttack"/>.</summary>
    protected abstract Task OnDrawCut(PlayerChoiceContext choiceContext, CardPlay cardPlay);

    /// <summary>After the Light was removed; <paramref name="consumed"/> = how much was consumed.</summary>
    protected virtual Task AfterLightConsumed(PlayerChoiceContext choiceContext, CardPlay cardPlay, int consumed) =>
        Task.CompletedTask;

    /// <summary>This card's attack with the block-ignoring props (AnyEnemy -> chosen target, AllEnemies -> all).</summary>
    protected AttackCommand DrawCutAttack(CardPlay cardPlay) =>
        CommonActions.CardAttack(this, cardPlay, cardPlay.Target, DynamicVars.Damage.BaseValue, DrawCutProps);
}
