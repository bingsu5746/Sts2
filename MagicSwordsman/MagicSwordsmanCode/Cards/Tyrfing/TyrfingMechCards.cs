using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Tyrfing;

// 티르빙 메커니즘 카드 3장 (2026-10-17, 사용자 요청 "검의 메커니즘을 제대로 반영하는 카드"): a payoff against enemies
// with Block (방패 가르기: Tyrfing hits through it, this card also takes it away), a kill payoff that feeds the curse
// engine (목숨값: a kill exhausts a curse → 악행 저주 +3 / 난쟁이의 저주) and curses in hand as a resource (헤르보르의 요구).
// Common 1 · Uncommon 2.

/// <summary>
/// 방패 가르기 (Uncommon attack, cost 2): 10 damage (+2/L). Then the target loses ALL its Block and you gain half of it
/// (rounded down). With Tyrfing current the hit itself already ignores Block; stripping it helps every later
/// non-Tyrfing attack (and allies).
/// </summary>
public sealed class TyrfingShieldSplitter : TyrfingCard
{
    public TyrfingShieldSplitter() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(10);
        WithDamagePerLevel(2);
        WithTip(SwordMechanicTips.Effect(SwordId.Tyrfing));
        WithTip(MegaCrit.Sts2.Core.HoverTips.StaticHoverTip.Block);
    }

    protected override bool ShouldGlowGoldInternal =>
        IsMutable && Owner?.Creature.CombatState is { } cs && cs.HittableEnemies.Any(e => e.Block > 0);

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        if (cardPlay.Target is not { IsAlive: true } target || target.Block <= 0) return;
        var removed = target.Block;
        await CreatureCmd.LoseBlock(target, removed);
        if (removed / 2 > 0) await CreatureCmd.GainBlock(Owner.Creature, removed / 2, ValueProp.Move, cardPlay);
    }
}

/// <summary>
/// 목숨값 (Common attack): 9 damage (+1/L). Fatal: if this kills an enemy, exhaust a random curse card from your hand,
/// draw pile or discard pile (an exhausted 악행 curse gives Tyrfing attacks +3 this combat; 난쟁이의 저주 gives Strength).
/// Fatal check as 황금 자루 (game HandOfGreed). Lore (Hervarar saga, high confidence): Tyrfing must take a man's life
/// every time it is drawn.
/// </summary>
public sealed class TyrfingLifePrice : TyrfingCard
{
    public TyrfingLifePrice() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(9);
        WithDamagePerLevel(1);
        WithTip(SwordMechanicTips.Effect(SwordId.Tyrfing));
    }

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var fatal = cardPlay.Target != null && cardPlay.Target.Powers.All(p => p.ShouldOwnerDeathTriggerFatal());
        var attack = await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (!fatal || !attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled)) return;
        var curses = PileType.Hand.GetPile(Owner).Cards
            .Concat(PileType.Draw.GetPile(Owner).Cards)
            .Concat(PileType.Discard.GetPile(Owner).Cards)
            .Where(c => c.Type == CardType.Curse)
            .ToList();
        if (curses.Count == 0) return;
        var curse = Owner.RunState.Rng.CombatCardSelection.NextItem(curses);
        if (curse != null) await CardCmd.Exhaust(choiceContext, curse);
    }
}

/// <summary>
/// 헤르보르의 요구 (Uncommon skill): gain 1 energy for each curse card in your hand (at most 2; 3 from level 3), then
/// draw 1 card. Lore (Hervararkviða / "The Waking of Angantýr", high confidence): Hervor goes to her dead father's
/// barrow on Samsø and demands Tyrfing despite his warning that it will destroy her line.
/// </summary>
public sealed class TyrfingHervorsDemand : TyrfingCard
{
    public TyrfingHervorsDemand() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithCalculatedVar("MaxEnergy", 2, static (card, _) => card is MagicSwordCard { SwordLevel: >= 3 } ? 1 : 0);
        WithCards(1);
    }

    private int MaxEnergy => SwordLevel >= 3 ? 3 : 2;

    private int CursesInHand =>
        IsMutable && Owner?.PlayerCombatState != null
            ? PileType.Hand.GetPile(Owner).Cards.Count(c => c.Type == CardType.Curse)
            : 0;

    protected override bool ShouldGlowGoldInternal => CursesInHand > 0;

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var energy = Math.Min(CursesInHand, MaxEnergy);
        if (energy > 0) await PlayerCmd.GainEnergy(energy, Owner);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}
