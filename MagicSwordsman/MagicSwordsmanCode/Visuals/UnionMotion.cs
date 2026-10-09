using Godot;
using MagicSwordsman.MagicSwordsmanCode.Cards.Union;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>The two-sword choreographies of the 【조합】 cards (one per card, see SwordVisuals.Union.cs).</summary>
public enum UnionMotionKind
{
    /// <summary>Kusanagi coils around the target like the serpent while Gram rises and slams down.</summary>
    Coil,
    /// <summary>Gram slams, then Tyrfing dashes through: one after the other.</summary>
    Sequence,
    /// <summary>The two blades cut through the target from opposite corners, crossing in an X.</summary>
    Cross,
    /// <summary>One blade in front, the ghost blade behind: both pierce at once from opposite sides.</summary>
    Pincer,
    /// <summary>Both blades circle the target like spirits, then cut one after the other.</summary>
    Orbit,
    /// <summary>One blade draws a warding circle around Ensifer while the other blinks to the target twice.</summary>
    Ward,
    /// <summary>Both rise high above the target (one grown huge) and come down together like falling mountains.</summary>
    Drop,
    /// <summary>A beam of light shoots from above while the other blade grows and sweeps a rainbow arc.</summary>
    BeamArc,
    /// <summary>Both blades circle Ensifer, glowing (no target).</summary>
    Halo,
    /// <summary>Two blades form an anvil above the head and the third hammers it three times (no target).</summary>
    Forge,
    /// <summary>The blades plant crossed in front of Ensifer as a shield, then fade half out of sight (no target).</summary>
    Guard,
    /// <summary>One blade spins a ring of cuts around Ensifer, the other flares and dashes at the target.</summary>
    FireRing,
}

/// <summary>
/// Presentation only: plays a 【조합】 card's own motion — Ensifer's body clip (<see cref="UnionCard.BodyClip"/>, an
/// existing AnimationPlayer clip) and the two swords' choreography (<see cref="SwordVisuals.PlayUnion"/>).
///  - Cards with an attack command: started by MotionDirector when the game fires "Attack" (the damage lands
///    <see cref="UnionCard.Impact"/> seconds later, the cards pass that as their attack animation delay).
///  - Other cards call <see cref="Play"/> from their effect (after both swords were summoned).
/// Every entry point swallows its errors.
/// </summary>
public static class UnionMotion
{
    /// <summary>Last union card choreographed per player and when (one choreography per card play).</summary>
    private static readonly Dictionary<ulong, (CardModel Card, ulong At)> Last = new();

    private const ulong RepeatWindowMs = 1200;

    /// <summary>A new card play began (MotionDirector.OnCardPlayed): its first "Attack" starts a fresh choreography.</summary>
    public static void OnCardPlayed(Player player) => Last.Remove(player.NetId);

    public static void Play(Player player, UnionCard card, Creature? target)
    {
        try
        {
            Last[player.NetId] = (card, Time.GetTicksMsec());
            Sfx.Union();
            SwordVisuals.PlayMotion(player, card.BodyClip);
            SwordVisuals.PlayUnion(player, card.Motion, card.Primary, card.Partner, target, card.Impact);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[UnionMotion] {card.Id.Entry}: {e.Message}");
        }
    }

    /// <summary>
    /// Called by MotionDirector when the game fires "Attack" for <paramref name="lastCard"/>. Returns true when the
    /// card is a union card: the choreography is started (first trigger of this play only) and
    /// <paramref name="clip"/> is the body clip to play instead of the generic attack (null = leave it).
    /// </summary>
    public static bool TryHandleAttackTrigger(Player player, CardModel? lastCard, Creature? target, out string? clip)
    {
        clip = null;
        try
        {
            if (lastCard is not UnionCard card) return false;
            var now = Time.GetTicksMsec();
            if (Last.TryGetValue(player.NetId, out var last) && ReferenceEquals(last.Card, card) &&
                now - last.At < RepeatWindowMs)
                return true; // later attack of the same play: the choreography is already running
            Last[player.NetId] = (card, now);
            clip = card.BodyClip;
            SwordVisuals.PlayUnion(player, card.Motion, card.Primary, card.Partner, target, card.Impact);
            return true;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[UnionMotion] attack trigger: {e.Message}");
            return false;
        }
    }
}
