using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

/// <summary>
/// 암수 한 쌍 — twin sword (Ganjiang + Moye), Attack, Rare, cost 1, one enemy. Deal 6 (+1/L) damage, gain 6 (+1/L)
/// Block. For the rest of this turn, the 【짝】 of every later Ganjiang/Moye card always triggers.
/// Lore: a male and female pair of swords named after the smith couple Ganjiang and Moye.
/// </summary>
public sealed class TwinMatedPair : GanjiangMoyeCard
{
    public TwinMatedPair() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(6);
        WithDamagePerLevel(1);
        WithBlock(6);
        WithBlockPerLevel(1);
    }

    protected override SwordId Side => SwordId.Ganjiang;
    protected override bool IsTwin => true;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await CommonActions.CardBlock(this, cardPlay);
        PairRules.MakeAlwaysThisTurn(Owner);
    }
}
