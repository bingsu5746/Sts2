using MegaCrit.Sts2.Core.Entities.Relics;

namespace MagicSwordsman.MagicSwordsmanCode.Relics;

/// <summary>
/// 브란스톡 (Rare) — 마검 소유 상한 +1 (content doc §7).
/// 원전: 「오딘이 볼숭 왕의 연회장 나무 브란스톡에 꽂아 두었고, 시그문드만 뽑을 수 있었다.」
/// </summary>
public sealed class Branstock : SwordSlotRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;
}
