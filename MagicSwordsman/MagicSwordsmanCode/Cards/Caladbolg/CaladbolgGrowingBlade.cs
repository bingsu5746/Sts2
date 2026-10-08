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
/// 공중에서 커지는 칼 (Uncommon power): this combat, whenever one of your attacks hits 2+ enemies, gain 2 Block
/// (+⌊L/2⌋). Source: 「내리치려 하면 공중에서 거대하게 커졌다」.
/// </summary>
public sealed class CaladbolgGrowingBlade : CaladbolgCard
{
    public CaladbolgGrowingBlade() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithCalculatedVar("BlockPerHit", 2, 1, static (card, _) => card is MagicSwordCard m ? m.SwordLevel / 2 : 0);
        WithTip(typeof(CaladbolgGrowingBladePower));
    }

    private int BlockPerHit => 2 + SwordLevel / 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<CaladbolgGrowingBladePower>(choiceContext, Owner.Creature, BlockPerHit, Owner.Creature,
            this);
    }
}
