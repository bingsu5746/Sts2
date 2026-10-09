using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

// 간장·막야 추가 카드 4장 (Moye 2 · Ganjiang 1 · twin 1): Common 2 · Uncommon 1 · Rare 1.

/// <summary>
/// 녹지 않는 쇠 — Moye, Skill, Common, cost 1, self. Gain 6 (+1/L) Block. 【짝】 gain 1 Blur (Block is not removed
/// at the start of your next turn). Lore (Wuyue Chunqiu): the iron would not melt in the furnace.
/// </summary>
public sealed class MoyeUnmeltingIron : GanjiangMoyeCard
{
    public MoyeUnmeltingIron() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(6);
        WithBlockPerLevel(1);
        WithPower<BlurPower>(1);
    }

    protected override SwordId Side => SwordId.Moye;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        await CommonActions.CardBlock(this, cardPlay);
        if (pair)
            await PowerCmd.Apply<BlurPower>(choiceContext, Owner.Creature, DynamicVars["BlurPower"].BaseValue,
                Owner.Creature, this);
    }
}

/// <summary>
/// 물결무늬 — Moye, Skill, Common, cost 1, one enemy. Gain 6 (+1/L) Block. 【짝】 apply 2 Weak.
/// Lore (Wuyue Chunqiu): Ganjiang bore a tortoise-shell pattern, Moye a flowing, rippled one.
/// </summary>
public sealed class MoyeRipplePattern : GanjiangMoyeCard
{
    public MoyeRipplePattern() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithBlock(6);
        WithBlockPerLevel(1);
        WithPower<WeakPower>(2);
    }

    protected override SwordId Side => SwordId.Moye;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        await CommonActions.CardBlock(this, cardPlay);
        if (pair && cardPlay.Target is { IsAlive: true } target)
            await PowerCmd.Apply<WeakPower>(choiceContext, target, DynamicVars.Weak.BaseValue, Owner.Creature, this);
    }
}

/// <summary>
/// 삼왕묘 — Ganjiang, Attack, Uncommon, cost 2, all enemies. Deal 4 damage to ALL enemies twice (+⌊L/2⌋ per hit).
/// 【짝】 one more time. Lore (Soushenji): the heads of the king, Chi and the assassin boiled in one cauldron and
/// were buried together as the Tomb of the Three Kings.
/// </summary>
public sealed class GanjiangThreeKingsTomb : GanjiangMoyeCard
{
    public GanjiangThreeKingsTomb() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithDamage(4);
        WithVar("Hits", 2);
        WithVar("PairHits", 1);
    }

    protected override SwordId Side => SwordId.Ganjiang;

    protected override int LevelDamageBonus(int level) => level / 2;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        var hits = DynamicVars["Hits"].IntValue + (pair ? DynamicVars["PairHits"].IntValue : 0);
        await CommonActions.CardAttack(this, cardPlay, hits)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}

/// <summary>
/// 쌍룡 — twin sword (Ganjiang + Moye), Power, Rare, cost 1, self. This combat, whenever 【짝】 triggers, deal
/// 4 (+⌊L/2⌋) damage to a random enemy (<see cref="TwinDragonsPower"/>).
/// Lore (Book of Jin): the pair leapt into the water at Yanping Ford and two dragons rose in their place.
/// </summary>
public sealed class TwinYanpingDragons : GanjiangMoyeCard
{
    public TwinYanpingDragons() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithCalculatedVar("DragonDamage", 4, 1, static (card, _) => card is MagicSwordCard m ? m.SwordLevel / 2 : 0);
        WithTip(typeof(TwinDragonsPower));
    }

    protected override SwordId Side => SwordId.Ganjiang;
    protected override bool IsTwin => true;

    private int DragonDamage => DynamicVars["DragonDamageBase"].IntValue + SwordLevel / 2;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        await PowerCmd.Apply<TwinDragonsPower>(choiceContext, Owner.Creature, DragonDamage, Owner.Creature, this);
    }
}
