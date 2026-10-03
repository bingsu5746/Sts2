using MagicSwordsman.MagicSwordsmanCode.RestSite;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace MagicSwordsman.MagicSwordsmanCode.Relics;

/// <summary>
/// 흑칠 칼집 (Common) — 다음 박살 1번을 실패로 바꾼다. 쓰면 빈 칼집이 된다 (content doc §7). One-use insurance; also
/// protects Gram from being reset to level 0. Used-up pattern: game relic LizardTail ([SavedProperty] bool +
/// IsUsedUp + RelicStatus.Disabled).
/// 원전: 「거의 밀폐된 흑칠 나무 칼집 … 2,500년 동안 녹이 거의 슬지 않았다」(월왕 구천검)
/// </summary>
public sealed class BlackLacquerSheath : MagicSwordsmanRelic, ISwordForgeModifier
{
    private bool _wasUsed;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override bool IsUsedUp => _wasUsed;

    [SavedProperty]
    public bool WasUsed
    {
        get => _wasUsed;
        set
        {
            AssertMutable();
            _wasUsed = value;
            if (IsUsedUp) Status = RelicStatus.Disabled;
        }
    }

    public Task<bool> TryPreventShatter(Player player, SwordId sword, ForgeMode mode)
    {
        if (player != Owner || WasUsed) return Task.FromResult(false);
        Flash();
        WasUsed = true;
        MainFile.Logger.Info($"[BlackLacquerSheath] shatter of {sword} turned into a failure");
        return Task.FromResult(true);
    }
}
