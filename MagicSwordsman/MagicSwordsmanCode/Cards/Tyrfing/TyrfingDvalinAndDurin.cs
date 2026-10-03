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
/// 드발린과 두린 (Uncommon): 6 damage (+1/L) + 3 per curse in your hand/draw/discard pile (4 from level 3).
/// </summary>
public sealed class TyrfingDvalinAndDurin : TyrfingCard
{
    public TyrfingDvalinAndDurin() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(6);
        WithDamagePerLevel(1);
    }

    private int PerCurse => SwordLevel >= 3 ? 4 : 3;

    protected override decimal BonusDamage(Creature? target) => PerCurse * CursesInPiles();

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}
