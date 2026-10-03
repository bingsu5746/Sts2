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

/// <summary>아쓰타 봉안 (Rare): remove one of your debuffs completely. 8 Block (+2/L). Exhaust.</summary>
public sealed class KusanagiAtsuta : KusanagiCard
{
    public KusanagiAtsuta() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithBlock(8);
        WithBlockPerLevel(2);
        WithKeywords(CardKeyword.Exhaust);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await KusanagiPurify.RemoveOneDebuff(Owner.Creature);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }
}
