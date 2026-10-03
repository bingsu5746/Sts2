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
/// 울스터의 영웅 (Rare power): cost 2 (1 from level 4). This combat, Caladbolg's effect hits EVERY enemy instead of
/// up to 3, and the single-enemy damage reduction is halved. Source: 「울스터의 영웅 페르구스 막 로이히」.
/// </summary>
public sealed class CaladbolgUlsterHero : CaladbolgCard
{
    public CaladbolgUlsterHero() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithTip(typeof(CaladbolgUlsterHeroPower));
    }

    protected override int? CostAtLevel(int level) => level >= 4 ? 1 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<CaladbolgUlsterHeroPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }
}
