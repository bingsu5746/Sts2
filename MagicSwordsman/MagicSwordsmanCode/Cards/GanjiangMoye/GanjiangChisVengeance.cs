using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

/// <summary>
/// 적의 복수 — Ganjiang, Attack, Rare, cost 2, one enemy. Deal 12 (+2/L) damage, +2 per 4 HP lost this combat
/// (max +16). 【짝】 apply 2 Vulnerable.
/// "HP lost this combat" = unblocked damage the owner received, from the combat history (pattern: game card
/// TearAsunder, DamageReceivedEntry; the history is cleared at every combat end).
/// Lore: Ganjiang's son Chi gave his own head and sword to the assassin.
/// </summary>
public sealed class GanjiangChisVengeance : GanjiangMoyeCard
{
    private const int HpPerStep = 4;
    private const int DamagePerStep = 2;
    private const int MaxBonus = 16;

    public GanjiangChisVengeance() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(12);
        WithDamagePerLevel(2);
        WithCalculatedVar("Vengeance", 0, 1, static (card, _) => card is GanjiangChisVengeance c ? c.VengeanceBonus : 0);
        WithVar("PairVuln", 2);
        WithTip(typeof(VulnerablePower));
    }

    protected override SwordId Side => SwordId.Ganjiang;

    /// <summary>Bonus damage from HP lost this combat: +2 per 4 HP, max +16.</summary>
    public int VengeanceBonus
    {
        get
        {
            if (!IsMutable || Owner?.PlayerCombatState == null) return 0;
            var creature = Owner.Creature;
            var lost = CombatManager.Instance.History.Entries.OfType<DamageReceivedEntry>()
                .Where(e => e.Receiver == creature)
                .Sum(e => e.Result.UnblockedDamage);
            return Math.Min(MaxBonus, lost / HpPerStep * DamagePerStep);
        }
    }

    protected override int ExtraDamage(Creature? target) => VengeanceBonus;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_heavy_blunt")
            .Execute(choiceContext);
        var target = cardPlay.Target;
        if (pair && target is { IsDead: false })
            await PowerCmd.Apply<VulnerablePower>(choiceContext, target, DynamicVars["PairVuln"].IntValue,
                Owner.Creature, this);
    }
}
