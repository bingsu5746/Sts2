using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 열두 광전사 (card TyrfingTwelveBerserkers): at the start of the owner's turn, lose <see cref="HpLoss"/> HP
/// (1 less from Tyrfing level <see cref="ReducedFromLevel"/>) and gain Amount <see cref="TyrfingVictoryPower"/>
/// (Tyrfing card attacks +damage this combat). Turn-start pattern: the game's DemonFormPower; HP loss: PoisonPower.
/// </summary>
public sealed class TyrfingBerserkersPower : MagicSwordsmanPower
{
    public const int HpLoss = 2;
    public const int ReducedFromLevel = 3;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<TyrfingVictoryPower>()];

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(Owner)) return;
        Flash();
        var player = Owner.Player;
        var loss = player != null && SwordCombat.LevelOf(player, SwordId.Tyrfing) >= ReducedFromLevel ? HpLoss - 1 : HpLoss;
        var choiceContext = new ThrowingPlayerChoiceContext();
        await CreatureCmd.Damage(choiceContext, Owner, loss, DamageProps.nonCardHpLoss, null, null);
        if (Owner.IsDead) return;
        await PowerCmd.Apply<TyrfingVictoryPower>(choiceContext, Owner, Amount, Owner, null);
    }
}
