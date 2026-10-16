using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

// 클라이브 솔라시 추가 카드: Common 3 · Uncommon 2 · Rare 1. 2026-10-15: 납도 reworked (Light -> energy/draw),
// 왕위 복귀 added; later (user request "Light gain/spend too samey") 피르 볼그 (Light from debuffs),
// 우스키아스 (Light while retained), 왕위 복귀 (threshold power) reworked and 스렝의 일격 (Light from wounds) added.
// Lore: 1차 마그 투이레드(피르 볼그와의 싸움), 핀디아스의 스승 우스키아스, '빛의 검'(클라이브 솔라시라는 이름의 뜻).

/// <summary>
/// 피르 볼그 (Common): 6 damage (+1/L). Gain 1 Light for each different debuff the target had (max 2).
/// Reworked 2026-10-15 (user request: diversify Light gain): it used to be "attack + gain 1 Light", the same flat gain
/// as 횃불. Now Light is wrung from an already weakened foe (pairs with 백광's Weak, Vulnerable, Spectral Scar...).
/// Lore (high confidence): the Tuatha Dé Danann defeated the Fir Bolg at the First Battle of Mag Tuired.
/// </summary>
public sealed class SolaisFirBolg : SolaisCard
{
    public const int MaxLight = 2;

    public SolaisFirBolg() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(6);
        WithDamagePerLevel(1);
        WithVar("MaxLight", MaxLight);
        WithTip(typeof(SolaisLightPower));
    }

    private static int DebuffKinds(Creature? target) => target == null
        ? 0
        : target.Powers.Count(p => p.IsVisible && p.TypeForCurrentAmount == PowerType.Debuff);

    protected override bool ShouldGlowGoldInternal => false;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // Counted before the hit: the target may die from it.
        var light = Math.Min(DebuffKinds(cardPlay.Target), MaxLight);
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        await SolaisLight.Gain(choiceContext, Owner, light, this);
    }
}

/// <summary>
/// 우스키아스 (Common): Retain. Gain 1 Light and draw 1 card. Whenever this is retained over the end of your turn
/// while Claíomh Solais' effect is active, gain 1 Light.
/// Reworked 2026-10-15 (user request: diversify Light gain): it used to be a plain "gain 2 Light". Now it is the
/// patient teacher — kept in hand, it charges a little every turn. Retention: game hook AfterFlush(retainedCards),
/// same as 숨긴 칼 (GanjiangHiddenBlade). Gated on Solais being active so it never dodges the fading cost.
/// Lore (high confidence): Uscias was the wise man of Findias, the city the sword came from.
/// </summary>
public sealed class SolaisUscias : SolaisCard
{
    public const int RetainLight = 1;

    public SolaisUscias() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithPower<SolaisLightPower>(1);
        WithCards(1);
        WithVar("RetainLight", RetainLight);
        WithKeywords(CardKeyword.Retain);
    }

    public override async Task AfterFlush(PlayerChoiceContext choiceContext, Player player,
        IReadOnlyCollection<CardModel> flushedCards, IReadOnlyCollection<CardModel> retainedCards)
    {
        await base.AfterFlush(choiceContext, player, flushedCards, retainedCards);
        if (!IsMutable || player != Owner || !retainedCards.Contains(this)) return;
        if (Owner.Creature.IsDead || !SolaisLight.TryGetActiveEffect(Owner, out _)) return;
        await SolaisLight.Gain(choiceContext, Owner, RetainLight, this);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await SolaisLight.Gain(choiceContext, Owner, DynamicVars["SolaisLightPower"].IntValue, this);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}

/// <summary>
/// 스렝의 일격 (Common skill, new 2026-10-15): cost 0. Draw 1 card. Until your next turn, whenever an enemy attack
/// deals unblocked damage to you, gain 1 Light (at most 3 times: <see cref="SolaisSrengPower"/>).
/// A Light source on the ENEMY's turn, from wounds (user request: diversify Light gain).
/// Lore (high confidence): at the First Battle of Mag Tuired the Fir Bolg champion Sreng cut off Nuada's arm — the
/// wound that later became the silver arm.
/// </summary>
public sealed class SolaisSrengsBlow : SolaisCard
{
    public SolaisSrengsBlow() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithCards(1);
        WithPower<SolaisSrengPower>(SolaisSrengPower.MaxTriggers);
        WithTip(typeof(SolaisLightPower));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        await PowerCmd.Apply<SolaisSrengPower>(choiceContext, Owner.Creature,
            DynamicVars["SolaisSrengPower"].BaseValue, Owner.Creature, this);
    }
}

/// <summary>
/// 납도 (Uncommon skill): cost 0. Spend 1 Light: gain 1 energy and draw 1 card (2 cards from level 3). Unplayable
/// without Light.
/// Reworked 2026-10-15 (user request #38): it used to be another "consume all Light, X + K per Light" 【발도】 card
/// (followed by 1 Light). Now Light is fuel for tempo instead of damage: putting the blade back into its sheath
/// banks the light as energy for the next stroke. Source: 「죽음의 칼집」 (the sword's sheath in Cath Maige Tuired).
/// </summary>
public sealed class SolaisResheathe : SolaisCard
{
    public const int LightCost = 1;

    public SolaisResheathe() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("LightCost", LightCost);
        WithEnergy(1);
        WithCalculatedVar("Draw", 1, static (card, _) => card is MagicSwordCard { SwordLevel: >= 3 } ? 1 : 0);
        WithTip(typeof(SolaisLightPower));
    }

    protected override bool IsPlayableExtra => !IsMutable || Owner?.PlayerCombatState == null || Light >= LightCost;

    protected override bool ShouldGlowGoldInternal => Light >= LightCost;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!await SolaisLightSpend.TrySpend(choiceContext, Owner, LightCost, this)) return;
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        await CardPileCmd.Draw(choiceContext, SwordLevel >= 3 ? 2 : 1, Owner);
    }
}

/// <summary>
/// 왕위 복귀 (Uncommon power): while you have 3 or more Light, your attacks deal 3 more damage per hit (4 from level
/// 5). Light is a living threshold: nothing is spent, but the bonus is gone the moment Light drops below 3
/// (<see cref="SolaisKingshipPower"/>). 【발도】 still gets it (its Light is removed after the hit).
/// Reworked 2026-10-15 (user request: "spend 1 / spend 2 / spend 3" cards only differed by numbers): it used to
/// spend 3 Light for 2 Strength.
/// Lore (Cath Maige Tuired, high confidence): a blemished king could not rule; Nuada lost the throne with his arm and
/// regained it with the silver arm — the king reigns only while he is whole.
/// </summary>
public sealed class SolaisKingshipRestored : SolaisCard
{
    public SolaisKingshipRestored() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("LightNeeded", SolaisKingshipPower.LightNeeded);
        WithCalculatedVar("KingDamage", 3, static (card, _) => card is MagicSwordCard { SwordLevel: >= 5 } ? 1 : 0);
        WithTip(typeof(SolaisLightPower));
        WithTip(typeof(SolaisKingshipPower));
    }

    protected override bool ShouldGlowGoldInternal => Light >= SolaisKingshipPower.LightNeeded;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SolaisKingshipPower>(choiceContext, Owner.Creature, SwordLevel >= 5 ? 4 : 3,
            Owner.Creature, this);
    }
}

/// <summary>
/// 빛의 검 (Rare power): when you end your turn with Claíomh Solais current, deal 2 (3 from level 3) damage per Light
/// to ALL enemies (Light is not consumed). The name Claíomh Solais means "sword of light".
/// </summary>
public sealed class SolaisSwordOfLight : SolaisCard
{
    public SolaisSwordOfLight() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<SolaisSwordOfLightPower>(1);
        WithCalculatedVar("PerLight", SolaisSwordOfLightPower.DamagePerLight,
            static (card, _) => card is MagicSwordCard { SwordLevel: >= SolaisSwordOfLightPower.HighFromLevel } ? 1 : 0);
        WithTip(typeof(SolaisLightPower));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SolaisSwordOfLightPower>(choiceContext, Owner.Creature,
            DynamicVars["SolaisSwordOfLightPower"].BaseValue, Owner.Creature, this);
    }
}
