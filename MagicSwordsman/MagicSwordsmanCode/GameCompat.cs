using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode;

/// <summary>
/// Game API calls whose signature differs between game v0.111 (default build) and v0.113 beta
/// (build with -p:Sts2Api=113, which defines STS2_V113). Keep version switches here where possible.
/// </summary>
internal static class GameCompat
{
    /// <summary>Max HP loss that does not come from a card (events, sword costs).</summary>
    public static Task LoseMaxHpNotFromCard(PlayerChoiceContext choiceContext, Creature creature, decimal amount) =>
#if STS2_V113
        CreatureCmd.LoseMaxHp(choiceContext, creature, amount, cardSource: null);
#else
        CreatureCmd.LoseMaxHp(choiceContext, creature, amount, isFromCard: false);
#endif

    /// <summary>The next relic of the player's relic reward queue (rarity rolled by the game).</summary>
    public static RelicModel PullNextRewardRelic(Player player) =>
#if STS2_V113
        RelicFactory.PullNextRelicForRewardFromFront(player);
#else
        RelicFactory.PullNextRelicFromFront(player);
#endif
}
