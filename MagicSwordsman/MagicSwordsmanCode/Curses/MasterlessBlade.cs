using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

namespace MagicSwordsman.MagicSwordsmanCode.Curses;

/// <summary>
/// 주인 없는 칼 — Onimaru's forge-failure curse (content doc §3.1). Unplayable.
/// While it is in your hand, Onimaru does not make its END-OF-TURN auto attack (【명령】 and the turn-start attack of
/// 스스로 움직이는 칼 still work). The check lives in OnimaruAttack.AutoAttack.
/// Source line (inverted, content doc note): 「주인의 손을 거치지 않고 스스로 움직여」 — "a blade that will not obey".
/// </summary>
public sealed class MasterlessBlade : SwordCurseCard
{
    public override SwordId Sword => SwordId.Onimaru;

    /// <summary>True if <paramref name="player"/> holds a 주인 없는 칼 in hand (combat only).</summary>
    public static bool IsInHand(Player player) =>
        player.PlayerCombatState != null && PileType.Hand.GetPile(player).Cards.Any(c => c is MasterlessBlade);
}
