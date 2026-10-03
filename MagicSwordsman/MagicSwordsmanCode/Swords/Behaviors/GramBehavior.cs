using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 그람 (starting sword) — WORKED EXAMPLE of a SwordBehavior.
/// Spec §7: current-sword effect "공격 피해 +강화 단계" [확정]; Kusanagi inherits "+강화 단계의 절반".
/// Cost [확정]: on reaching level 5, curse "니벨룽의 보물" (gives gold when played, never leaves the deck).
/// Shatter at the rest site returns Gram to level 0 instead of losing it (SwordDefinition.CanBeLost = false).
/// </summary>
public sealed class GramBehavior : SwordBehavior
{
    public override SwordId Id => SwordId.Gram;

    // Gram's cards come from the starting deck (부서진 칼날), so no starter cards on acquisition.
    public override IEnumerable<CardModel> StarterCards => [];

    // TODO(content: Gram): return ModelDb.Card<GramFailureCurse>() — a Gram-specific curse for forge failures
    // (user: "저주는 카드마다 다르게"). Until then forge failures lower the level instead.
    public override CardModel? FailureCurse => null;

    /// <summary>
    /// Current effect: the owner's powered attacks deal +Level damage (same filter as the game's StrengthPower).
    /// ctx.Scale halves it (rounded down, min 1) when Kusanagi inherits it.
    /// </summary>
    public override decimal ModifyDamageAdditive(SwordContext ctx, Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (dealer != ctx.Creature) return 0m;
        if (!props.IsPoweredAttack()) return 0m;
        return ctx.Scale(ctx.Level);
    }

    public override Task OnLevelChanged(Player player, int oldLevel, int newLevel)
    {
        if (oldLevel < 5 && newLevel >= 5)
        {
            // TODO(content: Gram): add the curse "니벨룽의 보물" (NibelungTreasure, Curses/) here:
            //   await player.GetRelic<Relics.Mangeomchong>()!.AddCurse(ModelDb.Card<NibelungTreasure>());
            // Use a run counter (e.g. "gram.nibelung") if it must only ever be added once per run.
            MainFile.Logger.Info("[Gram] reached level 5 — Nibelung curse not implemented yet");
        }

        return Task.CompletedTask;
    }
}
