using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Durandal;

// 뒤랑달 추가 카드 4장: Common 2 · Uncommon 1 · Rare 1.
// Lore: 론세스바예스의 후위, 황금 자루의 네 번째 성유물(성모 마리아의 옷 조각), 롤랑의 뿔나팔 올리펀트,
// 로카마두르 절벽에 박혀 있다는 검.

/// <summary>론세스바예스 (Common): 4 damage (+1/L) to ALL enemies; gain 2 Block per enemy. The rearguard holds the pass.</summary>
public sealed class DurandalRoncevaux : DurandalCard
{
    public const int BlockPerEnemy = 2;

    public DurandalRoncevaux() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
        WithDamage(4);
        WithDamagePerLevel(1);
        WithVar("BlockPerEnemy", BlockPerEnemy);
        WithTip(StaticHoverTip.Block);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var enemies = Owner.Creature.CombatState?.HittableEnemies.Count() ?? 0;
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").SpawningHitVfxOnEachCreature()
            .Execute(choiceContext);
        if (enemies > 0)
            await CreatureCmd.GainBlock(Owner.Creature, BlockPerEnemy * enemies, ValueProp.Move, cardPlay);
    }
}

/// <summary>성모의 옷자락 (Common): 4 Block (+1/L) now and the same Block next turn. DodgeAndRoll pattern.</summary>
public sealed class DurandalVirginsRaiment : DurandalCard
{
    public DurandalVirginsRaiment() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(4);
        WithBlockPerLevel(1);
        WithTip(typeof(BlockNextTurnPower));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var gained = await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (gained > 0)
            await PowerCmd.Apply<BlockNextTurnPower>(choiceContext, Owner.Creature, gained, Owner.Creature, this);
    }
}

/// <summary>올리펀트 (Uncommon): cost 0, Exhaust. Lose 3 HP; apply 2 Weak (3 from level 3) to ALL enemies.</summary>
public sealed class DurandalOlifant : DurandalCard
{
    public const int HpLoss = 3;

    public DurandalOlifant() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithVar("HpLoss", HpLoss);
        WithCalculatedVar("WeakAmt", 2, static (card, _) => card is MagicSwordCard { SwordLevel: >= 3 } ? 1 : 0);
        WithTip(typeof(WeakPower));
    }

    private int WeakAmount => SwordLevel >= 3 ? 3 : 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.Damage(choiceContext, Owner.Creature, HpLoss, DamageProps.cardHpLoss, this);
        var combatState = Owner.Creature.CombatState;
        if (combatState != null && Owner.Creature.IsAlive)
            await PowerCmd.Apply<WeakPower>(choiceContext, combatState.HittableEnemies, WeakAmount, Owner.Creature,
                this);
    }
}

/// <summary>
/// 로카마두르 (Rare power): this combat, if Durandal's effect is active at the start of your turn, your Block is not
/// removed. Conditional Barricade (game cost 3 -> 2 here).
/// </summary>
public sealed class DurandalRocamadour : DurandalCard
{
    public DurandalRocamadour() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<DurandalRocamadourPower>()));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.HasPower<DurandalRocamadourPower>()) return;
        await PowerCmd.Apply<DurandalRocamadourPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }
}
