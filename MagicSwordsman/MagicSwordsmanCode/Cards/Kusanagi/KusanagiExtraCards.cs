using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Kusanagi;

// 쿠사나기 추가 카드 4장: Common 2 · Uncommon 1 · Rare 1.

/// <summary>
/// 맞불 (Common): 4 damage (+1/L) to ALL enemies. Choose a status/curse card in hand and exhaust it; if you did,
/// deal the damage to ALL enemies again. Lore: Yamato Takeru lit a counter-fire with the flint from Yamatohime's bag.
/// </summary>
public sealed class KusanagiCounterfire : KusanagiCard
{
    public KusanagiCounterfire() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
        WithDamage(4);
        WithDamagePerLevel(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        if (await KusanagiPurify.ExhaustOneFromHand(choiceContext, Owner, this))
            await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}

/// <summary>
/// 빗 속의 구시나다 (Common): 7 Block (+1/L). Gain 1 Artifact. Exhaust.
/// Lore: Susanoo turned Kushinadahime into a comb and kept her in his hair while he fought Orochi.
/// </summary>
public sealed class KusanagiKushinadaComb : KusanagiCard
{
    public KusanagiKushinadaComb() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(7);
        WithBlockPerLevel(1);
        WithPower<ArtifactPower>(1);
        WithKeywords(CardKeyword.Exhaust);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<ArtifactPower>(choiceContext, Owner.Creature, DynamicVars["ArtifactPower"].BaseValue,
            Owner.Creature, this);
    }
}

/// <summary>
/// 헌상 (Uncommon power): this combat, whenever a status/curse card is exhausted, gain 4 (+⌊L/2⌋) Block
/// (<see cref="KusanagiOfferingPower"/>). Lore: Susanoo offered the sword from Orochi's tail to Amaterasu.
/// </summary>
public sealed class KusanagiOffering : KusanagiCard
{
    public KusanagiOffering() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithCalculatedVar("PurgeBlock", 4, 1, static (card, _) => card is MagicSwordCard m ? m.SwordLevel / 2 : 0);
        WithTip(typeof(KusanagiOfferingPower));
    }

    private int PurgeBlock => DynamicVars["PurgeBlockBase"].IntValue + SwordLevel / 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<KusanagiOfferingPower>(choiceContext, Owner.Creature, PurgeBlock, Owner.Creature, this);
    }
}

/// <summary>
/// 이 빠진 도쓰카 (Rare): cost 1 (0 from level 3). This turn the damage/Block bonus Kusanagi inherits applies at full
/// strength instead of half (<see cref="KusanagiChippedTotsukaPower"/>). Draw 2 cards.
/// Lore: Susanoo's own Totsuka sword chipped on the sword hidden in Orochi's tail.
/// </summary>
public sealed class KusanagiChippedTotsuka : KusanagiCard
{
    public KusanagiChippedTotsuka() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithCards(2);
        WithTip(typeof(KusanagiChippedTotsukaPower));
    }

    protected override int? CostAtLevel(int level) => level >= 3 ? 0 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<KusanagiChippedTotsukaPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}
