using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

/// <summary>
/// 쌍검난무 — twin sword (Ganjiang + Moye), Attack, Uncommon, cost 2, one enemy.
/// Deal 2 damage 4 times (+⌊L/2⌋ per hit). Gain 4 (+1/L) Block. 【짝】 (always, twin) draw 1.
/// Both effects apply: Ganjiang (current) on the damage, Moye's block bonus added by GanjiangMoyeCard.
/// </summary>
public sealed class TwinSwordDance : GanjiangMoyeCard
{
    public TwinSwordDance() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(2);
        WithVar("Hits", 4);
        WithBlock(4);
        WithBlockPerLevel(1);
        WithCards(1);
    }

    protected override SwordId Side => SwordId.Ganjiang;
    protected override bool IsTwin => true;

    protected override int LevelDamageBonus(int level) => level / 2;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        await CommonActions.CardAttack(this, cardPlay, DynamicVars["Hits"].IntValue)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await CommonActions.CardBlock(this, cardPlay);
        if (pair) await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}
