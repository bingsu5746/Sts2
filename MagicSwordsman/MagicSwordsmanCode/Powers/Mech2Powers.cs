using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

// Powers of the mech2 cards (2026-10-17): Gram / Kusanagi / Onimaru / Caladbolg cards that use each sword's core
// mechanic in a new way. Block from powers is Unpowered (like the game's PlatingPower / FeelNoPainPower).

/// <summary>
/// 용의 피 (GramDragonBlood): until the end of this turn, whenever the owner plays an Attack card, gain Amount Block.
/// Amount = Gram level + 2 when the card was played (stacks if played twice).
/// </summary>
public sealed class GramDragonBloodPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Amount <= 0 || Owner.IsDead || cardPlay.Card.Owner?.Creature != Owner) return;
        if (cardPlay.Card.Type != CardType.Attack) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 미소기 (KusanagiMisogi): this combat, whenever purification reduces or removes one of the owner's debuffs
/// (KusanagiPurify.ReduceOneDebuff / RemoveOneDebuff), draw 1 card and gain Amount Block.
/// No loop: every trigger needs a debuff that actually went down.
/// </summary>
public sealed class KusanagiMisogiPower : MagicSwordsmanPower
{
    public const int Draw = 1;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public async Task OnDebuffPurified(PlayerChoiceContext choiceContext)
    {
        if (Amount <= 0 || Owner.IsDead || Owner.Player is not { } player) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
        await CardPileCmd.Draw(choiceContext, Draw, player);
    }
}

/// <summary>
/// 녹을 닦다 (OnimaruPolishRust): this turn, Onimaru's end-of-turn auto attack never rolls a random stance and deals
/// Amount more damage per hit (호위: Amount more Block). Read by OnimaruAttack.AutoAttack; gone at the end of the turn
/// (the auto attack runs in BeforeSideTurnEnd, before this is removed in AfterSideTurnEnd).
/// </summary>
public sealed class OnimaruPolishedPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 바뀌는 자세 (OnimaruShiftingStance): this combat, whenever Onimaru's stance changes to a DIFFERENT stance
/// (OnimaruAttack.SetKind), Onimaru acts Amount more time(s) with the new stance right away. Stance cards are Power
/// cards (played once per combat) and random turn-end stances do not change the stance, so this cannot loop.
/// </summary>
public sealed class OnimaruShiftingStancePower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task OnStanceChanged(Player player, OnimaruKind kind, PlayerChoiceContext choiceContext)
    {
        if (Amount <= 0 || Owner.IsDead || player != Owner.Player) return;
        Flash();
        for (var i = 0; i < Amount; i++)
            await OnimaruAttack.Perform(player, kind, null, choiceContext, null, assumeCurrent: false);
    }
}

/// <summary>
/// 코르막의 만류 (CaladbolgCormac): this combat, whenever the owner plays an Attack card while only one enemy is left,
/// gain Amount Block — Caladbolg's single-enemy weakness turned into defense. Works whatever the current sword is.
/// </summary>
public sealed class CaladbolgRestraintPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Amount <= 0 || Owner.IsDead || cardPlay.Card.Owner?.Creature != Owner) return;
        if (cardPlay.Card.Type != CardType.Attack) return;
        if (CaladbolgSpread.HittableEnemyCount(Owner) != 1) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }
}
