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

/// <summary>여덟 갈래 (Uncommon): cost 2. Deal 2 damage to a random enemy 8 times (+⌊L/2⌋ per hit).</summary>
public sealed class KusanagiEightHeads : KusanagiCard
{
    public const int Hits = 8;

    public KusanagiEightHeads() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.RandomEnemy)
    {
        WithDamage(2);
        WithVar("Hits", Hits);
    }

    protected override decimal BonusDamage(Creature? target) => SwordLevel / 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay, Hits).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}
