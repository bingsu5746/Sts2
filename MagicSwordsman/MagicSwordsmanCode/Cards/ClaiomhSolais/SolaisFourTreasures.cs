using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

/// <summary>
/// 네 개의 보물 (Uncommon power): each turn, the first time you play an Attack, a Skill and a Power, gain 1 Light
/// each (so up to 3 Light a turn for varied play). From level 3 also gain 2 Block each time.
/// Reworked 2026-10-15 (user request: diversify how Light is GAINED): it used to add +1 to the end-of-turn charge,
/// the same "charge more at turn end" source as the sword itself. Now it rewards mixing card types — one gift per
/// treasure. Lore (high confidence): the four treasures of the Tuatha Dé Danann (Lia Fáil, Lugh's spear, Nuada's
/// sword, the Dagda's cauldron) came from four different cities; the sword from Findias.
/// </summary>
public sealed class SolaisFourTreasures : SolaisCard
{
    public SolaisFourTreasures() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<SolaisFourTreasuresPower>(1);
        WithVar("TreasureBlock", SolaisFourTreasuresPower.BlockPerStack);
        WithTip(typeof(SolaisLightPower));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SolaisFourTreasuresPower>(choiceContext, Owner.Creature,
            DynamicVars["SolaisFourTreasuresPower"].BaseValue, Owner.Creature, this);
    }
}
