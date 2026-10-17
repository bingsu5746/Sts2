using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Caladbolg;

/// <summary>
/// 스치는 칼끝 / 단단한 틈새 (Common): cost 0, 3 damage (+1/L). Gain 1 Vigor for each enemy this hit.
/// Reworked 2026-10-16 (near-clone pass): it was a plain 0-cost attack, one of four "deal X damage" Caladbolg cards.
/// Now a wide graze primes the next swing (game VigorPower: the next attack deals that much more).
/// Source: 「'단단한 틈새'」.
/// </summary>
public sealed class CaladbolgHardCleft : CaladbolgCard
{
    public const int VigorPerEnemy = 1;

    public CaladbolgHardCleft() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithTip(SwordMechTips.ThreeHills);
        WithDamage(3);
        WithDamagePerLevel(1);
        WithVar("VigorPerEnemy", VigorPerEnemy);
        WithTip(typeof(VigorPower));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var attack = await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        var hit = CaladbolgSpread.DistinctTargetsHit(attack);
        if (hit > 0 && !Owner.Creature.IsDead)
            await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, VigorPerEnemy * hit, Owner.Creature, this);
    }
}
