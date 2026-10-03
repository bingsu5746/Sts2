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
/// 검 받침대 (Common) — 매 턴 처음 현재 검이 바뀔 때 방어도 3 (content doc §7; Anchor / BronzeScales level).
/// "No sword -> first sword" counts as a switch (content doc §0.3 전환). Uses the framework's per-turn switch count
/// (<see cref="SwordCombat.SwitchesThisTurn"/>, already incremented when listeners run).
/// </summary>
public sealed class SwordRack : MagicSwordsmanRelic, ISwordListener
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(3m, ValueProp.Unpowered)];

    public async Task AfterSwordSwitched(Player player, SwordId? from, SwordId to, PlayerChoiceContext choiceContext)
    {
        if (player != Owner) return;
        if (SwordCombat.SwitchesThisTurn(player) != 1) return;
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
    }
}
