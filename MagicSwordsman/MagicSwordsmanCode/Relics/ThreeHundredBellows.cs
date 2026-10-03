using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Relics;

/// <summary>
/// 삼백 개의 풀무 (Uncommon) — 전투에서 두 번째로 검을 소환할 때도 만검총 효과(방어도 3 + 카드 1장) (content doc §7).
/// Mangeomchong's [확정] first-summon bonus is untouched; this relic adds the same numbers on the 2nd summon.
/// Counts real summons only (SwordCombatState.Summoned: Onimaru emerging by itself is not a summon; Ganjiang and Moye
/// count as one summon each).
/// 원전: 「아이 300명이 풀무질을 해 3년 만에 완성했다.」
/// </summary>
public sealed class ThreeHundredBellows : MagicSwordsmanRelic, ISwordListener
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(3m, ValueProp.Unpowered),
        new CardsVar(1),
    ];

    public async Task AfterSwordSummoned(Player player, SwordId sword, PlayerChoiceContext choiceContext)
    {
        if (player != Owner) return;
        if (SwordCombat.Get(player) is not { } state || state.Summoned.Count != 2) return;
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}
