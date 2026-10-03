using MegaCrit.Sts2.Core.Entities.Players;

namespace MagicSwordsman.MagicSwordsmanCode.Relics;

/// <summary>
/// Base of the two "마검 소유 상한 +1" relics (content doc §7: 브란스톡, 검총의 나무 패; spec §2 [확정] "유물·이벤트로
/// 1~2자루 확장 가능" -> max 3 + 1 + 1 = 5).
/// The slot is stored in Mangeomchong.ExtraSlots (saved there), so it is added once in <see cref="AfterObtained"/>
/// (the game calls it only when the relic is obtained, not on load) and taken back in <see cref="AfterRemoved"/>.
/// Swords already owned are never released when a slot is taken back (the next acquisition simply needs room).
/// </summary>
public abstract class SwordSlotRelic : MagicSwordsmanRelic
{
    public const int SlotBonus = 1;

    protected static Mangeomchong? TombOf(Player player) => player.GetRelic<Mangeomchong>();

    public override async Task AfterObtained()
    {
        await base.AfterObtained();
        if (TombOf(Owner) is not { } tomb)
        {
            MainFile.Logger.Warn($"[{GetType().Name}] obtained without Mangeomchong; no sword slot added.");
            return;
        }

        tomb.AddExtraSlots(SlotBonus);
        tomb.Flash();
        MainFile.Logger.Info($"[{GetType().Name}] sword slots now {tomb.SlotCapacity}");
    }

    public override async Task AfterRemoved()
    {
        await base.AfterRemoved();
        TombOf(Owner)?.AddExtraSlots(-SlotBonus);
    }
}
