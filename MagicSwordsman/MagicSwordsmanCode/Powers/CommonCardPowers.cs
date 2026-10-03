using MagicSwordsman.MagicSwordsmanCode.Cards;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

// Powers of the common (non-sword) card pool, content doc §4.3–4.4. Sword events come from the framework's
// ISwordListener (SwordCombat.Listeners includes the player's powers). A "switch" is every change of the current
// sword, including none -> sword (content doc §0.3), which is exactly when AfterSwordSwitched fires.

/// <summary>검무 (SwordDance): whenever your current sword changes, gain Amount Block.</summary>
public sealed class SwordDancePower : MagicSwordsmanPower, ISwordListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public async Task AfterSwordSwitched(Player player, SwordId? from, SwordId to, PlayerChoiceContext choiceContext)
    {
        if (player != Owner.Player || !Owner.IsAlive) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }
}

/// <summary>
/// 칼바람 (BladeGale): whenever your current sword changes, deal Amount damage to a random enemy
/// (pattern: game JuggernautPower — Rng.CombatTargets, ValueProp.Unpowered).
/// </summary>
public sealed class BladeGalePower : MagicSwordsmanPower, ISwordListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task AfterSwordSwitched(Player player, SwordId? from, SwordId to, PlayerChoiceContext choiceContext)
    {
        if (player != Owner.Player || !Owner.IsAlive) return;
        var enemies = CombatState.HittableEnemies;
        if (enemies.Count == 0) return;
        var target = player.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target == null) return;
        Flash();
        await CreatureCmd.Damage(choiceContext, target, Amount, ValueProp.Unpowered, Owner, null);
    }
}

/// <summary>
/// 한 검 깊이 (OneBladeFocus): if your current sword does not change for the rest of this turn, gain Amount Block at
/// the end of the turn. A switch removes the power; it is removed after it pays out (end-of-turn timing: game
/// PlatingPower, BeforeSideTurnEndEarly).
/// </summary>
public sealed class OneBladeFocusPower : MagicSwordsmanPower, ISwordListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public async Task AfterSwordSwitched(Player player, SwordId? from, SwordId to, PlayerChoiceContext choiceContext)
    {
        if (player != Owner.Player) return;
        await PowerCmd.Remove(this);
    }

    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;
        if (Owner.IsAlive)
        {
            Flash();
            await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
        }
        await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 검심 (SwordHeart): at the start of your turn, if you have a current sword, draw Amount more card(s)
/// (hand-draw hook: game MachineLearningPower.ModifyHandDraw). The current sword is kept between turns.
/// </summary>
public sealed class SwordHeartPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner.Player) return count;
        return SwordCombat.CurrentSword(player) == null ? count : count + Amount;
    }
}

/// <summary>
/// 마검 공명 (SwordResonance): whenever your current sword changes, draw 1 card, at most Amount times per turn
/// (per-turn counter in internal data, pattern: game OutbreakPower / InitInternalData).
/// </summary>
public sealed class SwordResonancePower : MagicSwordsmanPower, ISwordListener
{
    private sealed class Data
    {
        public int Turn;
        public int Triggers;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new Data();

    public async Task AfterSwordSwitched(Player player, SwordId? from, SwordId to, PlayerChoiceContext choiceContext)
    {
        if (player != Owner.Player || !Owner.IsAlive) return;
        var data = GetInternalData<Data>();
        var turn = player.PlayerCombatState?.TurnNumber ?? 0;
        if (data.Turn != turn)
        {
            data.Turn = turn;
            data.Triggers = 0;
        }
        if (data.Triggers >= Amount) return;
        data.Triggers++;
        Flash();
        await CardPileCmd.Draw(choiceContext, 1, player);
    }
}

/// <summary>
/// 검성 (SwordSaint): the first time your current sword changes each turn, gain Amount energy. "First this turn"
/// uses the framework counter <see cref="SwordCombat.SwitchesThisTurn"/> (already incremented when listeners run).
/// </summary>
public sealed class SwordSaintPower : MagicSwordsmanPower, ISwordListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task AfterSwordSwitched(Player player, SwordId? from, SwordId to, PlayerChoiceContext choiceContext)
    {
        if (player != Owner.Player || !Owner.IsAlive) return;
        if (SwordCombat.SwitchesThisTurn(player) != 1) return;
        Flash();
        await PlayerCmd.GainEnergy(Amount, player);
    }
}

/// <summary>
/// 검총의 네 자루 (FourSwordsOfTheTomb): when you summon your 2nd / 3rd / 4th sword this combat, gain 1 energy /
/// draw 2 cards / gain 2 Strength (each × Amount). The count is <see cref="SwordCombatState.Summoned"/> (emerging
/// Onimaru is not a summon, spec §3).
/// </summary>
public sealed class FourSwordsOfTheTombPower : MagicSwordsmanPower, ISwordListener
{
    public const int EnergyAt2 = 1;
    public const int CardsAt3 = 2;
    public const int StrengthAt4 = 2;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<StrengthPower>()];

    public async Task AfterSwordSummoned(Player player, SwordId sword, PlayerChoiceContext choiceContext)
    {
        if (player != Owner.Player || !Owner.IsAlive) return;
        var state = SwordCombat.Get(player);
        if (state == null) return;
        switch (state.Summoned.Count)
        {
            case 2:
                Flash();
                await PlayerCmd.GainEnergy(EnergyAt2 * Amount, player);
                break;
            case 3:
                Flash();
                await CardPileCmd.Draw(choiceContext, CardsAt3 * Amount, player);
                break;
            case 4:
                Flash();
                await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, StrengthAt4 * Amount, Owner, null);
                break;
        }
    }
}

/// <summary>
/// 검진 (SwordFormation): at the end of your turn, gain Amount Block for each sword out (present) other than the
/// current one (end-of-turn timing: game PlatingPower, BeforeSideTurnEndEarly).
/// </summary>
public sealed class SwordFormationPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || !Owner.IsAlive || Owner.Player is not { } player) return;
        var state = SwordCombat.Get(player);
        if (state == null) return;
        var swords = state.Present.Count(s => s != state.Current);
        if (swords <= 0) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, Amount * swords, ValueProp.Unpowered, null);
    }
}

/// <summary>
/// 검의 메아리 (SwordEcho): this turn, the next Amount sword card(s) you play are played one extra time
/// (pattern: game BurstPower — ModifyCardPlayCount + AfterModifyingCardPlayCount, removed at the end of the turn).
/// </summary>
public sealed class SwordEchoPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card.Owner?.Creature != Owner) return playCount;
        if (card is not MagicSwordCard { IsSwordCard: true }) return playCount;
        return playCount + 1;
    }

    public override async Task AfterModifyingCardPlayCount(CardModel card)
    {
        await PowerCmd.Decrement(this);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 무검의 경지 (Swordless): your attack cards that are not sword cards deal Amount more damage per hit
/// (filter: game StrengthPower — powered attacks of the owner; cardSource must be a non-sword attack card).
/// </summary>
public sealed class SwordlessPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        if (dealer == null || dealer != Owner || !props.IsPoweredAttack()) return 0m;
        if (cardSource == null || cardSource.Type != CardType.Attack) return 0m;
        if (cardSource is MagicSwordCard { IsSwordCard: true }) return 0m;
        return Amount;
    }
}
