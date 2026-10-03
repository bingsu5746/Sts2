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

/// <summary>회그니의 칼부림 (Common): deal 4 damage twice (+⌊L/2⌋ per hit).</summary>
public sealed class DainsleifHognisFlurry : DainsleifCard
{
    public const int Hits = 2;

    public DainsleifHognisFlurry() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(4);
        WithVar("Hits", Hits);
    }

    protected override decimal BonusDamage(Creature? target) => SwordLevel / 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay, Hits).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}
