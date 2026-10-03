using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

/// <summary>간장 베기 — Ganjiang, Attack, Basic (starter), cost 1, one enemy. Deal 6 (+1/L) damage. 【짝】 +3 damage.</summary>
public sealed class GanjiangSlash : GanjiangMoyeCard
{
    public GanjiangSlash() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithDamage(6);
        WithDamagePerLevel(1);
        WithVar("PairDamage", 3);
    }

    protected override SwordId Side => SwordId.Ganjiang;

    protected override int ExtraDamage(Creature? target) => PairBonusApplies ? DynamicVars["PairDamage"].IntValue : 0;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}
