using MagicSwordsman.MagicSwordsmanCode.Cards;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>Helpers shared by the Tyrfing powers.</summary>
internal static class TyrfingPowerUtil
{
    /// <summary>A powered attack hit of <paramref name="owner"/> coming from a Tyrfing card.</summary>
    public static bool IsTyrfingAttack(Creature owner, Creature? dealer, ValueProp props, CardModel? cardSource) =>
        dealer == owner && props.IsPoweredAttack() && cardSource is MagicSwordCard { Sword: SwordId.Tyrfing };
}

/// <summary>
/// 언제나 이기는 칼 — this combat, Tyrfing card attacks deal +Amount damage. Gained when one of Tyrfing's
/// 악행 curses (스바프를라미의 최후 / 햐르마르의 죽음 / 앙간튀르의 죽음) is exhausted (content doc §3.2, +3 each).
/// </summary>
public sealed class TyrfingVictoryPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource) =>
        TyrfingPowerUtil.IsTyrfingAttack(Owner, dealer, props, cardSource) ? Amount : 0m;
}

/// <summary>목숨값 (TyrfingDwarvenRansom): the next Tyrfing attack this turn deals +Amount damage.</summary>
public sealed class TyrfingRansomPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource) =>
        TyrfingPowerUtil.IsTyrfingAttack(Owner, dealer, props, cardSource) ? Amount : 0m;

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (command.Attacker != Owner || command.ModelSource is not MagicSwordCard { Sword: SwordId.Tyrfing }) return;
        await PowerCmd.Remove(this);
    }

    // Same pattern as the game's ReboundPower: gone at the end of the owner's turn.
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 난쟁이의 저주 (TyrfingDwarvenCurse): this combat, whenever one of your curse cards is exhausted gain Amount
/// Strength; from Tyrfing level 3 also 4 Block per stack (FeelNoPainPower pattern).
/// </summary>
public sealed class TyrfingDwarvenCursePower : MagicSwordsmanPower
{
    public const int BlockPerStack = 4;
    public const int BlockFromLevel = 3;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<StrengthPower>(), HoverTipFactory.Static(StaticHoverTip.Block)];

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner?.Creature != Owner || card.Type != CardType.Curse) return;
        Flash();
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, Amount, Owner, null);
        var player = Owner.Player;
        if (player != null && SwordCombat.LevelOf(player, SwordId.Tyrfing) >= BlockFromLevel)
            await CreatureCmd.GainBlock(Owner, BlockPerStack * Amount, ValueProp.Unpowered, null);
    }
}
