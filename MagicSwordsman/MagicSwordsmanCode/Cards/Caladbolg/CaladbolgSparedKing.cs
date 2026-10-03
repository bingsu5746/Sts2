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
/// 치지 못한 왕 (Uncommon): 6 Block (+1/L). Apply 1 Weak to ALL enemies.
/// Source: 「코나하르 왕을 치지 못하게 되자」.
/// </summary>
public sealed class CaladbolgSparedKing : CaladbolgCard
{
    public CaladbolgSparedKing() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithBlock(6);
        WithBlockPerLevel(1);
        WithPower<WeakPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        var combatState = Owner.Creature.CombatState;
        if (combatState != null)
            await PowerCmd.Apply<WeakPower>(choiceContext, combatState.HittableEnemies,
                DynamicVars.Weak.BaseValue, Owner.Creature, this);
    }
}
