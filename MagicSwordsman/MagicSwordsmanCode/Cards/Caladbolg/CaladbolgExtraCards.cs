using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Caladbolg;

// 칼라드볼그 추가 카드 4장 (Common 2 · Uncommon 1 · Rare 1). Lore (Táin Bó Cúailnge): Fergus mac Róich, exiled from
// Ulster after Conchobar's betrayal of the sons of Uisnech, fought for Connacht; in the last battle he struck
// Conchobar's shield Óchaín, which cried out, before Cormac turned him aside and he cut the tops off three hills.

/// <summary>오한의 비명 (Common): 5 damage (+1/L). Gain 2 Block for each enemy this hit.</summary>
public sealed class CaladbolgOchainsCry : CaladbolgCard
{
    public CaladbolgOchainsCry() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(5);
        WithDamagePerLevel(1);
        WithVar("BlockPerEnemy", 2);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var attack = await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        var hit = CaladbolgSpread.DistinctTargetsHit(attack);
        if (hit > 0)
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars["BlockPerEnemy"].BaseValue * hit, ValueProp.Move,
                cardPlay);
    }
}

/// <summary>추방자의 원한 (Common): cost 0. Vulnerable 1 (2 from level 3) to the target and up to 2 other random enemies.</summary>
public sealed class CaladbolgExilesGrudge : CaladbolgCard
{
    public CaladbolgExilesGrudge() : base(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithCalculatedVar("Vuln", 1, 1, static (card, _) => card is MagicSwordCard { SwordLevel: >= 3 } ? 1 : 0);
        WithTip(typeof(VulnerablePower));
    }

    private int VulnerableAmount => SwordLevel >= 3 ? 2 : 1;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        var state = Owner.Creature.CombatState;
        if (target == null || state == null) return;

        var others = state.HittableEnemies.Where(c => c != target).ToList();
        if (others.Count > CaladbolgBehavior.MaxTargets - 1)
        {
            Owner.RunState.Rng.CombatTargets.Shuffle(others);
            others = others.Take(CaladbolgBehavior.MaxTargets - 1).ToList();
        }

        var targets = new List<Creature> { target };
        targets.AddRange(others);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, targets, VulnerableAmount, Owner.Creature, this);
    }
}

/// <summary>외로운 언덕 (Uncommon, cost 2): 10 damage (+2/L). If it hit only one enemy, deal it again.</summary>
public sealed class CaladbolgLoneHill : CaladbolgCard
{
    public CaladbolgLoneHill() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(10);
        WithDamagePerLevel(2);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var attack = await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (CaladbolgSpread.DistinctTargetsHit(attack) != 1 || cardPlay.Target is not { IsAlive: true }) return;
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}

/// <summary>
/// 페르구스의 분노 (Rare power): this combat, whenever one of your attacks hits 2+ enemies, deal 3 (+⌊L/2⌋) damage to
/// ALL enemies.
/// </summary>
public sealed class CaladbolgFergusWrath : CaladbolgCard
{
    public CaladbolgFergusWrath() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithCalculatedVar("WrathDamage", 3, 1, static (card, _) => card is MagicSwordCard m ? m.SwordLevel / 2 : 0);
        WithTip(typeof(CaladbolgFergusWrathPower));
    }

    private int WrathDamage => 3 + SwordLevel / 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<CaladbolgFergusWrathPower>(choiceContext, Owner.Creature, WrathDamage, Owner.Creature,
            this);
    }
}
