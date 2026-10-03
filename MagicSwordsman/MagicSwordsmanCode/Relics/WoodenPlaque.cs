using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace MagicSwordsman.MagicSwordsmanCode.Relics;

/// <summary>
/// 검총의 나무 패 (Shop) — 마검 소유 상한 +1, 얻을 때 HP 5 회복 (content doc §7).
/// 원전: 「검총에는 나무 패만 남아 있다.」
/// </summary>
public sealed class WoodenPlaque : SwordSlotRelic
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new HealVar(5m)];

    public override async Task AfterObtained()
    {
        await base.AfterObtained();
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
    }
}
