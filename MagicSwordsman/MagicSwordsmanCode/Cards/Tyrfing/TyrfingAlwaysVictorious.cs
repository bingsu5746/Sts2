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
/// 언제나 승리 (Rare): cost 3. 24 damage (+3/L). Double damage if the target is at 30% HP or less.
/// </summary>
public sealed class TyrfingAlwaysVictorious : TyrfingCard
{
    public TyrfingAlwaysVictorious() : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(24);
        WithDamagePerLevel(3);
    }

    protected override decimal DamageMultiplier(Creature? target) =>
        target is { MaxHp: > 0 } t && t.CurrentHp * 10 <= t.MaxHp * 3 ? 2m : 1m;

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}
