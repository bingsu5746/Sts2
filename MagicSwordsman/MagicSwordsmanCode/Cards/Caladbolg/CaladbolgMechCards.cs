using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Caladbolg;

// 칼라드볼그 메커니즘 카드 3장 (mech2, 2026-10-17, user request "현재의 검들의 메커니즘을 제대로 반영하는 카드들"):
// Caladbolg's payoffs so far all read "how many enemies did this attack hit" (draw / Vigor / Block / damage). These add
// a payoff for killing several at once, a card whose COST depends on the number of enemies, and a power that turns the
// single-enemy weakness into defense.

/// <summary>
/// 가레크의 전투 — Caladbolg, Attack, Uncommon, cost 1, one enemy. Deal 7 (+1/L) damage. Fatal: for each enemy this
/// attack kills, gain 1 Energy and draw 1 card. With Caladbolg's effect the hit spreads to up to 3 enemies, so one
/// swing can refund up to 3 Energy. Fatal check per enemy before the attack (game card Feed pattern: minions etc. do
/// not count).
/// Lore (Táin Bó Cúailnge, medium-high confidence): the last battle of the cattle raid, at Gáirech and Irgairech, where
/// Fergus fought with his sword until he was turned aside and struck the hilltops instead.
/// </summary>
public sealed class CaladbolgGairech : CaladbolgCard
{
    public CaladbolgGairech() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(7);
        WithDamagePerLevel(1);
        WithEnergy(1);
        WithCards(1);
        WithTip(StaticHoverTip.Fatal);
        WithTip(SwordMechTips.ThreeHills);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var fatalTargets = (Owner.Creature.CombatState?.HittableEnemies.ToList() ?? new List<Creature>())
            .Where(c => c.Powers.All((PowerModel p) => p.ShouldOwnerDeathTriggerFatal()))
            .ToHashSet();
        var attack = await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_giant_horizontal_slash")
            .Execute(choiceContext);
        var kills = attack.Results.SelectMany(r => r)
            .Where(r => r.WasTargetKilled && fatalTargets.Contains(r.Receiver))
            .Select(r => r.Receiver)
            .Distinct()
            .Count();
        if (kills <= 0 || Owner.Creature.IsDead) return;
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue * kills, Owner);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue * kills, Owner);
    }
}

/// <summary>
/// 대군 가르기 — Caladbolg, Attack, Common, cost 3, one enemy. Deal 8 (+1/L) damage. Costs 1 less for each enemy
/// besides the first (minimum 1): 3 enemies = cost 1 and, with Caladbolg's effect, 8 to each of them; a lone enemy =
/// cost 3 and the single-enemy reduction. The number of enemies as a resource.
/// </summary>
public sealed class CaladbolgCleaveHost : CaladbolgCard
{
    public CaladbolgCleaveHost() : base(3, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(8);
        WithDamagePerLevel(1);
        WithTip(SwordMechTips.ThreeHills);
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        var changed = base.TryModifyEnergyCostInCombat(card, originalCost, out modifiedCost);
        if (!ReferenceEquals(card, this)) return changed;
        var others = EnemyCount - 1;
        if (others <= 0) return changed;
        var reduced = Math.Max(1m, modifiedCost - others);
        if (reduced >= modifiedCost) return changed;
        modifiedCost = reduced;
        return true;
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_giant_horizontal_slash").Execute(choiceContext);
    }
}

/// <summary>
/// 코르막의 만류 — Caladbolg, Power, Uncommon, cost 1, self. This combat, whenever you play an Attack while only one
/// enemy is left, gain 3 (+⌊L/2⌋) Block (<see cref="CaladbolgRestraintPower"/>, whatever the current sword is).
/// The single-enemy fight is Caladbolg's weak spot (its attacks deal less); this turns it into defense.
/// Lore (Táin Bó Cúailnge, medium-high confidence): in the last battle Cormac Cond Longas, Conchobar's son, held Fergus
/// back from killing his father; Fergus turned his sword on the three hills instead.
/// </summary>
public sealed class CaladbolgCormac : CaladbolgCard
{
    public CaladbolgCormac() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithCalculatedVar("RestraintBlock", 3, 1, static (card, _) => card is MagicSwordCard m ? m.SwordLevel / 2 : 0);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<CaladbolgRestraintPower>()));
        WithTip(SwordMechTips.ThreeHills);
    }

    private int RestraintBlock => DynamicVars["RestraintBlockBase"].IntValue + SwordLevel / 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<CaladbolgRestraintPower>(choiceContext, Owner.Creature, RestraintBlock, Owner.Creature,
            this);
    }
}
