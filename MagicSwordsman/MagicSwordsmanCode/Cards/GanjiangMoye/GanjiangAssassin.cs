using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

/// <summary>
/// 자객 — Ganjiang, Attack, Common, cost 1, one enemy. Deal 3 damage twice (+⌊L/2⌋ per hit). 【짝】 one more hit.
/// Lore: the assassin took revenge in his place.
/// </summary>
public sealed class GanjiangAssassin : GanjiangMoyeCard
{
    public GanjiangAssassin() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(3);
        WithVar("Hits", 2);
        WithVar("PairHits", 1);
    }

    protected override SwordId Side => SwordId.Ganjiang;

    protected override int LevelDamageBonus(int level) => level / 2;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        var hits = DynamicVars["Hits"].IntValue + (pair ? DynamicVars["PairHits"].IntValue : 0);
        await CommonActions.CardAttack(this, cardPlay, hits)
            .WithHitFx("vfx/vfx_dramatic_stab")
            .Execute(choiceContext);
    }
}
