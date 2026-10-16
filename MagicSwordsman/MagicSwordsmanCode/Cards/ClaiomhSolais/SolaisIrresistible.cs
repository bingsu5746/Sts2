using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

/// <summary>
/// 눈부심 (Uncommon skill): 6 Block (+1/L). Until your next turn, whenever an enemy that is not Vulnerable attacks
/// you, spend 1 Light automatically and apply 2 Vulnerable to it (<see cref="SolaisDazzlePower"/>).
/// Reworked 2026-10-15 (user request: Light spending too samey): it used to be a 【발도】 card (consume all Light,
/// Vulnerable per Light). Now Light spends ITSELF on the enemy's turn, one dose per attacker — so it only works if you
/// ended the turn with Claíomh Solais current (otherwise the Light has already faded).
/// Source: 「아무도 저항할 수 없었다」 — whoever swings at the bearer is blinded by the glare.
/// </summary>
public sealed class SolaisIrresistible : SolaisCard
{
    public SolaisIrresistible() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(6);
        WithBlockPerLevel(1);
        WithPower<SolaisDazzlePower>(SolaisDazzlePower.VulnerablePerTrigger);
        WithTip(typeof(SolaisLightPower));
        WithTip(typeof(VulnerablePower));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<SolaisDazzlePower>(choiceContext, Owner.Creature,
            DynamicVars["SolaisDazzlePower"].BaseValue, Owner.Creature, this);
    }
}
