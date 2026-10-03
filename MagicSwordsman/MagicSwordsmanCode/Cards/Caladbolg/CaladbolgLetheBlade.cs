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
/// 레테의 검 (Uncommon): cost 0. This turn, your next attack (2 attacks from level 3) hits up to 3 enemies whatever
/// the current sword is, with no single-enemy penalty. Draw 1 card. Source: 「'요정 언덕에서 온 레테의 검'」.
/// </summary>
public sealed class CaladbolgLetheBlade : CaladbolgCard
{
    public CaladbolgLetheBlade() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithCalculatedVar("Attacks", 1, 1, static (card, _) => card is MagicSwordCard m && m.SwordLevel >= 3 ? 1 : 0);
        WithCards(1);
        WithTip(typeof(CaladbolgLethePower));
    }

    private int Attacks => SwordLevel >= 3 ? 2 : 1;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<CaladbolgLethePower>(choiceContext, Owner.Creature, Attacks, Owner.Creature, this);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}
