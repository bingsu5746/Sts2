using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 오니마루. Spec §7 [확정]: from the start of combat it comes out ON ITS OWN (SwordDefinition.EmergesAtCombatStart;
/// Mangeomchong calls SwordCombat.Emerge — not a summon, no Mangeomchong bonus, not current) and auto-attacks every
/// turn. The attack kind is chosen by dedicated Power cards (last one wins): 거합 Iai (default, single big hit) /
/// 난무 Frenzy (random multi-hit) / 베기 Cleave (all enemies) / 퇴마 Exorcism (damage + Weak) / 호위 Guard (block).
/// Attack cards are "commands": Onimaru immediately makes an extra auto attack of the current kind.
/// Cost: normal fights — each turn 25% chance the kind is random; elites/bosses — follows the chosen kind + bonus damage.
/// Kusanagi inherits "half of the upgrade bonus" (the auto attack itself always runs).
/// </summary>
public sealed class OnimaruBehavior : SwordBehavior
{
    public override SwordId Id => SwordId.Onimaru;

    // TODO(content: Onimaru): starter cards (2), failure curse.
    public override IEnumerable<CardModel> StarterCards => [];
    public override CardModel? FailureCurse => null;

    public override Task OnPlayerTurnStart(SwordContext ctx, PlayerChoiceContext choiceContext)
    {
        // TODO(content: Onimaru): auto attack every turn while ctx.IsPresent (it is present from turn 1).
        //   Store the chosen kind in ctx.Combat counters (e.g. ctx.SetCounter("kind", (int)OnimaruKind.Iai)).
        //   Random rolls: use ctx.Player.RunState.Rng.CombatTargets or .Niche (game Rng, never System.Random).
        //   Elite/boss detection: check the current room / encounter type in the game source.
        return Task.CompletedTask;
    }
}
