using MagicSwordsman.MagicSwordsmanCode.RestSite;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace MagicSwordsman.MagicSwordsmanCode.Relics;

/// <summary>
/// 레긴의 모루 (Uncommon) — 도박 강화 성공 확률 +10%p; taken from the failure chance, shatter unchanged
/// (content doc §7): +2 = 75/22/3, +3 = 50/42/8.
/// 원전: 「오딘이 직접 부러뜨린 뒤 레긴이 두 조각을 다시 벼렸다. 새 그람은 모루를 둘로 쪼갰고」
/// </summary>
public sealed class ReginsAnvil : MagicSwordsmanRelic, ISwordForgeModifier
{
    private const string BonusVar = "Bonus";

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar(BonusVar, 10m)];

    public ForgeOdds ModifyForgeOdds(Player player, SwordId sword, ForgeMode mode, ForgeOdds odds)
    {
        if (player != Owner || mode == ForgeMode.Safe) return odds;
        var shift = Math.Min(DynamicVars[BonusVar].IntValue, odds.Failure);
        return odds with { Success = odds.Success + shift, Failure = odds.Failure - shift };
    }
}
