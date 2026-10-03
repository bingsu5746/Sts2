using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Dainsleif;

/// <summary>
/// 되풀이되는 전투 (Uncommon): 6 damage (+1/L). Each time you play it, its damage increases by 3 (4 from level 3)
/// for the rest of this combat (Rampage-like; per card instance).
/// </summary>
public sealed class DainsleifRecurrence : DainsleifCard
{
    private int _bonus;

    public DainsleifRecurrence() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(6);
        WithDamagePerLevel(1);
    }

    private int Growth => SwordLevel >= 3 ? 4 : 3;

    protected override decimal BonusDamage(Creature? target) => _bonus;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        AssertMutable();
        _bonus += Growth;
    }
}
