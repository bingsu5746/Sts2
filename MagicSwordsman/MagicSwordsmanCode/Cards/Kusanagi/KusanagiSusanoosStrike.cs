using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Kusanagi;

/// <summary>
/// 스사노오의 일격 (Uncommon): 6 damage (+1/L), +3 for each status/curse card exhausted this combat.
/// </summary>
public sealed class KusanagiSusanoosStrike : KusanagiCard
{
    public const int PerExhausted = 3;

    public KusanagiSusanoosStrike() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(6);
        WithDamagePerLevel(1);
        WithVar("PerCard", PerExhausted);
    }

    protected override decimal BonusDamage(Creature? target) =>
        IsMutable && Owner?.PlayerCombatState != null ? PerExhausted * KusanagiPurify.ExhaustedThisCombat(Owner) : 0;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}
