using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Gram;

/// <summary>
/// 모루 쪼개기 — Gram, Attack, Uncommon, cost 2, one enemy. Remove all of the target's Block, then deal 12 (+1/L)
/// damage and apply 1 Vulnerable (2 from Gram level 3). Removing block: CreatureCmd.LoseBlock (game card Expose).
/// Lore: the new Gram split the anvil in two.
/// </summary>
public sealed class GramAnvilSplitter : GramCard
{
    public GramAnvilSplitter() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(12);
        WithDamagePerLevel(1);
        WithCalculatedVar("Vuln", 1, 1, static (card, _) => card is MagicSwordCard { SwordLevel: >= 3 } ? 1 : 0);
        WithTip(typeof(VulnerablePower));
    }

    private int VulnerableAmount => SwordLevel >= 3 ? 2 : 1;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;
        if (target.Block > 0) await CreatureCmd.LoseBlock(target, target.Block);
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
        if (!target.IsDead)
            await PowerCmd.Apply<VulnerablePower>(choiceContext, target, VulnerableAmount, Owner.Creature, this);
    }
}
