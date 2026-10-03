using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Tyrfing;

/// <summary>
/// 광전사 아른그림 (Uncommon): deal 3 damage 3 times (+⌊L/2⌋ per hit). If you lost HP this turn, 1 more time
/// (same check as the game's Spite).
/// </summary>
public sealed class TyrfingBerserkerArngrim : TyrfingCard
{
    public const int BaseHits = 3;

    public TyrfingBerserkerArngrim() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(3);
        WithVar("Hits", BaseHits);
    }

    protected override decimal BonusDamage(Creature? target) => SwordLevel / 2;

    protected override bool ShouldGlowGoldInternal => IsMutable && Owner != null && LostHpThisTurn(Owner.Creature);

    private static bool LostHpThisTurn(Creature creature) =>
        CombatManager.Instance.History.Entries.OfType<DamageReceivedEntry>().Any(e =>
            e.HappenedThisTurn(creature.CombatState) && e.Receiver == creature && e.Result.UnblockedDamage > 0);

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hits = BaseHits + (LostHpThisTurn(Owner.Creature) ? 1 : 0);
        await CommonActions.CardAttack(this, cardPlay, hits).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}
