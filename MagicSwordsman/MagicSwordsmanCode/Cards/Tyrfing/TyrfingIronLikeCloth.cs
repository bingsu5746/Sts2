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
/// 쇠와 돌을 천처럼 (Common): 7 damage (+1/L), + the target's Block (at most 10).
/// 2026-10-17 (mechanic pass): was a flat "+4 if the target has Block". Tyrfing's hits ignore Block, so the more Block
/// the target hides behind, the harder this cuts — a payoff that scales against high-Block enemies.
/// </summary>
public sealed class TyrfingIronLikeCloth : TyrfingCard
{
    public const int BlockedBonus = 10;

    public TyrfingIronLikeCloth() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(7);
        WithDamagePerLevel(1);
        WithVar("Bonus", BlockedBonus);
        WithTip(SwordMechanicTips.Effect(Swords.SwordId.Tyrfing));
    }

    protected override decimal BonusDamage(Creature? target) => target is { Block: > 0 } t ? Math.Min(t.Block, BlockedBonus) : 0m;

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}
