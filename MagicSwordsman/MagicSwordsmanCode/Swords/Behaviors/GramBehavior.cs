using MagicSwordsman.MagicSwordsmanCode.Cards;
using MagicSwordsman.MagicSwordsmanCode.Cards.Gram;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 그람 (starting sword).
/// Spec §7: current-sword effect "공격 피해 +강화 단계" [확정]; Kusanagi inherits "+강화 단계의 절반".
/// Cost [확정]: on reaching level 5, curse "니벨룽의 보물" (content doc §3.2 / §0.4 #18: only the FIRST time
/// level 5 is reached in a run, one card per run).
/// Forge failure curse: 오딘이 꺾은 칼날 (content doc §3.1).
/// Shatter at the rest site returns Gram to level 0 instead of losing it (SwordDefinition.CanBeLost = false).
/// Card cost thresholds (레긴의 재단조, 발뭉·노퉁) are applied here via <see cref="GramCard.CostAtLevel"/>.
/// </summary>
public sealed class GramBehavior : SwordBehavior
{
    /// <summary>Run counter (Mangeomchong) set once the Nibelung curse has been given.</summary>
    public const string NibelungCounter = "gram.nibelung";

    public override SwordId Id => SwordId.Gram;

    // Gram's cards come from the starting deck (부서진 칼날), so no starter cards on acquisition.
    public override IEnumerable<CardModel> StarterCards => [];

    public override CardModel? FailureCurse => ModelDb.Card<OdinsBrokenBlade>();

    /// <summary>
    /// Current effect: the owner's powered attacks deal +Level damage (same filter as the game's StrengthPower).
    /// ctx.Scale halves it (rounded down, min 1) when Kusanagi inherits it.
    /// While 발뭉·노퉁 is active and Gram itself is current, the power's bonus replaces this one (content doc §2.1).
    /// </summary>
    public override decimal ModifyDamageAdditive(SwordContext ctx, Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (dealer != ctx.Creature) return 0m;
        if (!props.IsPoweredAttack()) return 0m;
        if (!ctx.IsInherited && ctx.Creature.HasPower<BalmungNothungPower>()) return 0m;
        return ctx.Scale(ctx.Level);
    }

    public override bool TryModifyEnergyCost(SwordContext ctx, MagicSwordCard card, decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card is not GramCard gramCard || gramCard.CostAtLevel(ctx.Level) is not { } cost) return false;
        if (cost >= originalCost) return false;
        modifiedCost = cost;
        return true;
    }

    public override async Task OnLevelChanged(Player player, int oldLevel, int newLevel)
    {
        if (oldLevel >= SwordRegistry.MaxLevel || newLevel < SwordRegistry.MaxLevel) return;
        var relic = player.GetRelic<Mangeomchong>();
        if (relic == null) return;
        if (relic.GetRunCounter(NibelungCounter) > 0) return; // §0.4 #18: once per run

        relic.SetRunCounter(NibelungCounter, 1);
        // TODO(content: forge event): show the extra line of content doc §5.2 ("…그리고 니벨룽의 보물이 함께
        // 따라왔다.") on the forge result screen; the rest-site result UI is framework-owned.
        await relic.AddCurse(ModelDb.Card<NibelungTreasure>());
        MainFile.Logger.Info("[Gram] reached level 5 for the first time: added 니벨룽의 보물");
    }
}
