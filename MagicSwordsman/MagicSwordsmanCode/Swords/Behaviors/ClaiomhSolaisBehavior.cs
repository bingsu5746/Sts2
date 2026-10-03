using MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 클라이브 솔라시 (Claíomh Solais). Spec §7 [확정] / content doc §1.1, §2.9:
///  - Current effect: at the END of the owner's turn, if Claíomh Solais is current, charge 1 Light (always 1,
///    content doc 0.4 #5 — the level raises the 발도 damage per Light "K" instead). Using other swords during the
///    turn is free.
///  - Cost: at the end of the turn, if it is NOT current, all Light is lost.
///  - Kusanagi inheriting Claíomh Solais counts as "Claíomh Solais is current" for the end-of-turn check
///    (content doc 0.4 #6): it charges ctx.Scale(1) = 1 and does not lose Light.
///  - Light is a visible counter power (<see cref="SolaisLightPower"/>) on the player; 【발도】 cards
///    (<see cref="SolaisDrawCutCard"/>) consume all of it for block-ignoring strikes.
/// The end-of-turn rule runs in <see cref="OnPlayerTurnEnd"/>, an owned-sword lifecycle hook (called by Mangeomchong
/// from the game's BeforeSideTurnEnd for every owned sword), so it also sees Kusanagi's inheritance.
/// </summary>
public sealed class ClaiomhSolaisBehavior : SwordBehavior
{
    public override SwordId Id => SwordId.ClaiomhSolais;

    public override IEnumerable<CardModel> StarterCards =>
        [ModelDb.Card<SolaisSheathOfDeath>(), ModelDb.Card<SolaisNuadasTorch>()];

    /// <summary>누아다의 잃은 팔 (content doc §3.1).</summary>
    public override CardModel? FailureCurse => ModelDb.Card<NuadasLostArm>();

    public override async Task OnPlayerTurnEnd(SwordContext ctx, PlayerChoiceContext choiceContext)
    {
        var player = ctx.Player;
        if (ctx.Combat == null || ctx.Creature.IsDead) return;

        if (SolaisLight.TryGetActiveEffect(player, out var effectCtx))
        {
            // [확정] charge 1 (inherited: half of 1 -> min 1 = 1)
            var amount = effectCtx.Scale(SolaisLight.ChargePerTurn);

            // 네 가지 보물: +1 Light per stack; from level 3 also 3 Block per stack.
            var treasures = ctx.Creature.GetPower<SolaisFourTreasuresPower>();
            if (treasures != null) amount += treasures.Amount;

            await SolaisLight.Gain(choiceContext, player, amount, null);

            if (treasures != null) await treasures.AfterLightCharged(SwordCombat.LevelOf(player, Id));
        }
        else
        {
            // [확정] cost: not current at the end of the turn -> all Light fades.
            await SolaisLight.LoseAllAsCost(choiceContext, player);
        }
    }
}

/// <summary>
/// Light (빛) rules shared by the behavior, the 【발도】 cards and the Solais powers.
/// Light = amount of <see cref="SolaisLightPower"/> on the player creature (no upper limit, content doc §2.9).
/// </summary>
public static class SolaisLight
{
    /// <summary>[확정] Light charged at the end of a turn while Claíomh Solais is current.</summary>
    public const int ChargePerTurn = 1;

    /// <summary>
    /// 발도 damage per Light "K" at a Solais level (content doc §1.1): 4, 5, 5, 6, 6, 7 = 4 + ⌈L/2⌉.
    /// </summary>
    public static int DamagePerLight(int level) => 4 + LevelBonus(level);

    /// <summary>⌈L/2⌉ — the part of K that comes from the level (used by the cards' text variables).</summary>
    public static int LevelBonus(int level) => (Math.Clamp(level, 0, SwordRegistry.MaxLevel) + 1) / 2;

    public static int Get(Player player) => player.Creature.GetPowerAmount<SolaisLightPower>();

    /// <summary>
    /// Whether Claíomh Solais' current-sword effect is active for this player right now: Solais is current, or the
    /// current sword (Kusanagi) inherits it. <paramref name="ctx"/> is the context the effect runs with
    /// (IsInherited = true for Kusanagi).
    /// </summary>
    public static bool TryGetActiveEffect(Player player, out SwordContext ctx)
    {
        ctx = null!;
        if (SwordCombat.CurrentSword(player) is not { } current) return false;
        foreach (var (behavior, effectCtx) in SwordCombat.CurrentEffects(player, current, preview: false))
        {
            if (behavior.Id != SwordId.ClaiomhSolais) continue;
            ctx = effectCtx;
            return true;
        }

        return false;
    }

    public static async Task Gain(PlayerChoiceContext choiceContext, Player player, int amount,
        CardModel? source)
    {
        if (amount <= 0) return;
        await PowerCmd.Apply<SolaisLightPower>(choiceContext, player.Creature, amount, player.Creature, source);
    }

    /// <summary>【발도】: removes all Light and returns how much was consumed.</summary>
    public static async Task<int> ConsumeAll(Player player)
    {
        var power = player.Creature.GetPower<SolaisLightPower>();
        if (power == null) return 0;
        var amount = power.Amount;
        await PowerCmd.Remove(power);
        return amount;
    }

    /// <summary>
    /// The cost: all Light fades. 은팔의 누아다 (<see cref="SolaisSilverArmPower"/>) turns the lost Light into Block
    /// (it does not cancel the cost, content doc §2.9).
    /// </summary>
    public static async Task LoseAllAsCost(PlayerChoiceContext choiceContext, Player player)
    {
        var lost = await ConsumeAll(player);
        if (lost <= 0) return;
        var silverArm = player.Creature.GetPower<SolaisSilverArmPower>();
        if (silverArm != null) await silverArm.OnLightLostAsCost(lost);
    }
}
