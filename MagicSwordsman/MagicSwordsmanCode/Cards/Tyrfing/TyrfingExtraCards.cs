using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Tyrfing;

// 티르빙 추가 카드 4장 (Common 2 · Uncommon 1 · Rare 1): HP cost, curses in your piles, Tyrfing attack bonus.

/// <summary>
/// 삼쇠섬 결투 (Common): 10 damage (+1/L), then lose 2 HP (enables 광전사 아른그림's extra hit).
/// Lore: on Samsø, Angantyr with Tyrfing and Hjalmar dealt each other their death wounds.
/// </summary>
public sealed class TyrfingSamsoDuel : TyrfingCard
{
    public const int HpLoss = 2;

    public TyrfingSamsoDuel() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(10);
        WithDamagePerLevel(1);
        WithVar("HpLoss", HpLoss);
    }

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        await CreatureCmd.Damage(choiceContext, Owner.Creature, HpLoss, DamageProps.cardHpLoss, this);
    }
}

/// <summary>
/// 앙간튀르의 경고 (Common): 11 Block (+1/L). Shuffle an Injury (curse) into your draw pile (FightThrough pattern),
/// feeding 드발린과 두린 / 녹슬지 않는 날 / 난쟁이의 저주.
/// Lore: woken in his barrow, Angantyr warned Hervor that Tyrfing would destroy her whole line; she took it anyway.
/// </summary>
public sealed class TyrfingAngantyrsWarning : TyrfingCard
{
    public TyrfingAngantyrsWarning() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(11);
        WithBlockPerLevel(1);
        WithTip(typeof(Injury));
    }

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        var injury = CombatState!.CreateCard<Injury>(Owner);
        CardCmd.PreviewCardPileAdd(
            await CardPileCmd.AddGeneratedCardToCombat(injury, PileType.Draw, Owner, CardPilePosition.Random));
    }
}

/// <summary>
/// 무덤의 불길 (Uncommon): cost 0. Lose 3 HP (2 from level 3). Exhaust every Curse in your hand, then draw 1 card
/// per card exhausted. Exhausting 악행 curses also triggers their +3 (TyrfingVictoryPower) and 난쟁이의 저주.
/// Lore: Hervor walked through the barrow fires of Samsø to wake her father and claim Tyrfing.
/// </summary>
public sealed class TyrfingBarrowFires : TyrfingCard
{
    public TyrfingBarrowFires() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithCalculatedVar("HpLoss", 3, -1, static (card, _) => card is MagicSwordCard { SwordLevel: >= 3 } ? 1 : 0);
    }

    private int HpLoss => SwordLevel >= 3 ? 2 : 3;

    protected override bool ShouldGlowGoldInternal =>
        IsMutable && Owner != null && PileType.Hand.GetPile(Owner).Cards.Any(c => c.Type == CardType.Curse);

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.Damage(choiceContext, Owner.Creature, HpLoss, DamageProps.cardHpLoss, this);
        var curses = PileType.Hand.GetPile(Owner).Cards.Where(c => c.Type == CardType.Curse).ToList();
        foreach (var curse in curses) await CardCmd.Exhaust(choiceContext, curse);
        if (curses.Count > 0) await CardPileCmd.Draw(choiceContext, curses.Count, Owner);
    }
}

/// <summary>
/// 열두 광전사 (Rare power): cost 2. At the start of each of your turns, lose 2 HP (1 from level 3) and Tyrfing
/// card attacks deal +2 damage for the rest of this combat (<see cref="TyrfingBerserkersPower"/> →
/// <see cref="TyrfingVictoryPower"/>). Turn-start HP loss also enables 광전사 아른그림's extra hit every turn.
/// Lore: Arngrim's twelve sons, Angantyr the eldest, were all berserkers.
/// </summary>
public sealed class TyrfingTwelveBerserkers : TyrfingCard
{
    public TyrfingTwelveBerserkers() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<TyrfingBerserkersPower>(2);
        WithCalculatedVar("HpLoss", TyrfingBerserkersPower.HpLoss, -1,
            static (card, _) => card is MagicSwordCard { SwordLevel: >= TyrfingBerserkersPower.ReducedFromLevel } ? 1 : 0);
        WithTip(typeof(TyrfingVictoryPower));
    }

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<TyrfingBerserkersPower>(choiceContext, Owner.Creature,
            DynamicVars["TyrfingBerserkersPower"].BaseValue, Owner.Creature, this);
    }
}
