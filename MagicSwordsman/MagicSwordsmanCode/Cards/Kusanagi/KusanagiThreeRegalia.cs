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

/// <summary>
/// 삼종신기 (Rare power): cost 2 (1 from level 3). Switching from Kusanagi to another sword keeps the inherited effect
/// until the end of that turn; whenever you switch into Kusanagi from another sword, draw 1 card.
/// </summary>
public sealed class KusanagiThreeRegalia : KusanagiCard
{
    public KusanagiThreeRegalia() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<KusanagiRegaliaPower>(1);
    }

    protected override int? CostAtLevel(int level) => level >= 3 ? 1 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<KusanagiRegaliaPower>(choiceContext, Owner.Creature,
            DynamicVars["KusanagiRegaliaPower"].BaseValue, Owner.Creature, this);
    }
}
