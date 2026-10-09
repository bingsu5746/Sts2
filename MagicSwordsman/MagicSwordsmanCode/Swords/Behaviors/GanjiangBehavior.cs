using MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 간장 (male blade of the pair). Spec §7 + content doc §1.1 / §2.2:
///  - Current effect: the owner's powered attacks deal +2/+2/+3/+3/+4/+5 damage per hit (level 0..5).
///    Kusanagi inherits half (ctx.Scale: 1/1/1/1/2/2, content doc §1.3).
///  - Pair: acquired/lost together with Moye (2 slots); Moye shares Ganjiang's level (SwordDefinition.LevelOwner).
///    The 【짝】 pair bonus is card logic (Cards/GanjiangMoye/PairRules); twin-sword cards use Sword = Ganjiang.
///  - Cost [확정]: lose 6 Max HP when the pair is acquired — only the FIRST time in a run (content doc §0.4 #10).
///    Also when the pair is the random first sword of the run (OnAcquiredAsStartingSword, 2026-10-09).
///  - Starter card: 간장 베기 (Moye grants 막야 막기). Forge-failure curse: 용광로의 제물 (shared with Moye).
/// </summary>
public sealed class GanjiangBehavior : SwordBehavior
{
    public const int MaxHpCost = 6;

    private static readonly int[] DamageByLevel = [2, 2, 3, 3, 4, 5];

    public override SwordId Id => SwordId.Ganjiang;

    public override IEnumerable<CardModel> StarterCards => [ModelDb.Card<GanjiangSlash>()];

    public override CardModel? FailureCurse => ModelDb.Card<FurnaceOffering>();

    /// <summary>Attack damage bonus of the current-sword effect at a level (0..5).</summary>
    public static int DamageBonus(int level) => DamageByLevel[Math.Clamp(level, 0, DamageByLevel.Length - 1)];

    public override decimal ModifyDamageAdditive(SwordContext ctx, Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (dealer != ctx.Creature) return 0m;
        if (!props.IsPoweredAttack()) return 0m;
        return ctx.Scale(DamageBonus(ctx.LevelFor(cardSource)));
    }

    /// <summary>
    /// The pair rolled as the run's first sword: the same Max HP cost, applied directly because the run is still being
    /// created (no room yet, so no damage command). Current HP is clamped to the new maximum.
    /// </summary>
    public override void OnAcquiredAsStartingSword(Player player)
    {
        var creature = player.Creature;
        creature.SetMaxHpInternal(Math.Max(1, creature.MaxHp - MaxHpCost));
        MainFile.Logger.Info($"[Ganjiang] starting sword: lost {MaxHpCost} Max HP");
    }

    public override async Task OnAcquired(Player player, bool firstTime)
    {
        if (!firstTime) return;
        // Out-of-combat Max HP loss: same call as the game's relics/events (LeafyPoultice, UnrestSite).
        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), player.Creature, MaxHpCost, isFromCard: false);
        MainFile.Logger.Info($"[Ganjiang] first acquisition: lost {MaxHpCost} Max HP");
    }
}
