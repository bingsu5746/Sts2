using BaseLib.Extensions;
using Cheongunmun.CheongunmunCode.Resources;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Cheongunmun.CheongunmunCode.Relics;

public sealed class Danjeon : CheongunmunRelic
{
    public const int StartingMax = 1;

    [SavedProperty]
    public int MaxNaegong { get; set; } = StartingMax;

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override bool ShowCounter => true;
    public override int DisplayAmount => MaxNaegong;

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != Owner || Owner.PlayerCombatState is not { TurnNumber: 1 } playerCombatState)
            return Task.CompletedTask;

        playerCombatState.GetResource<Naegong>().Fill();
        Flash();
        return Task.CompletedTask;
    }
}
