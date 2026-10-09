using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

// 클라이브 솔라시 powers (content doc §2.9, §3.1). Light rules: Swords/Behaviors/ClaiomhSolaisBehavior.cs (SolaisLight).

/// <summary>
/// 빛 (Light) — Claíomh Solais' charge. Amount = Light. Charged at the end of the turn while Claíomh Solais is
/// current, lost when another sword is current at the end of the turn, consumed by 【발도】 cards.
/// It has no effect of its own; the behavior and the cards read the amount.
/// </summary>
public sealed class SolaisLightPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>
/// 네 가지 보물 (SolaisFourTreasures): when you end your turn with Claíomh Solais current, charge Amount more Light;
/// from Claíomh Solais level 3 also gain 3 Block per stack. Applied by ClaiomhSolaisBehavior.OnPlayerTurnEnd.
/// </summary>
public sealed class SolaisFourTreasuresPower : MagicSwordsmanPower
{
    public const int BlockFromLevel = 3;
    public const int BlockPerStack = 3;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<SolaisLightPower>()];

    /// <summary>Called by ClaiomhSolaisBehavior after the end-of-turn charge (which already included +Amount Light).</summary>
    public async Task AfterLightCharged(int solaisLevel)
    {
        Flash();
        if (solaisLevel < BlockFromLevel || Owner.IsDead) return;
        await CreatureCmd.GainBlock(Owner, BlockPerStack * Amount, ValueProp.Unpowered, null);
    }
}

/// <summary>
/// 은팔의 누아다 (SolaisSilverArm): when Light is lost as Claíomh Solais' cost, gain 4 Block per Light lost (5 from
/// level 3); whenever you 【발도】, draw 1 card. Amount = number of copies played (multiplies both).
/// </summary>
public sealed class SolaisSilverArmPower : MagicSwordsmanPower
{
    public const int BlockPerLight = 4;
    public const int BlockPerLightHigh = 5;
    public const int HighFromLevel = 3;
    public const int DrawPerDrawCut = 1; // balance 2026-10-09: was 2

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<SolaisLightPower>(), HoverTipFactory.Static(StaticHoverTip.Block)];

    private int BlockPerLightNow()
    {
        var player = Owner.Player;
        return player != null && SwordCombat.LevelOf(player, SwordId.ClaiomhSolais) >= HighFromLevel
            ? BlockPerLightHigh
            : BlockPerLight;
    }

    /// <summary>Called by SolaisLight.LoseAllAsCost after the Light was removed.</summary>
    public async Task OnLightLostAsCost(int lightLost)
    {
        if (lightLost <= 0 || Owner.IsDead) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, BlockPerLightNow() * lightLost * Amount, ValueProp.Unpowered, null);
    }

    /// <summary>Called by SolaisDrawCutCard after every 【발도】.</summary>
    public async Task OnDrawCut(PlayerChoiceContext choiceContext)
    {
        var player = Owner.Player;
        if (player == null || Owner.IsDead) return;
        Flash();
        await CardPileCmd.Draw(choiceContext, DrawPerDrawCut * Amount, player);
    }
}

/// <summary>
/// 마그 투이레드의 대가 (SolaisMagTuired): next turn you have Amount less energy. Pattern of the game's
/// EnergyNextTurnPower (AfterEnergyReset, then removed), with PlayerCmd.LoseEnergy.
/// </summary>
public sealed class SolaisMagTuiredPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.ForEnergy(this)];

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player) return;
        Flash();
        await PlayerCmd.LoseEnergy(Amount, player);
        await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 누아다의 잃은 팔 (curse NuadasLostArm): draw Amount fewer cards at the start of your next turn. Pattern of the
/// game's DrawCardsNextTurnPower (only counts when present at turn start, removed after the hand draw) with the
/// subtraction of MindRotPower.
/// </summary>
public sealed class NuadasLostArmPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner.Player || AmountOnTurnStart == 0) return count;
        return Math.Max(0m, count - Amount);
    }

    public override Task AfterModifyingHandDraw()
    {
        Flash();
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (participants.Contains(Owner) && AmountOnTurnStart != 0) await PowerCmd.Remove(this);
    }
}
