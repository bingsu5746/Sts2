using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

/// <summary>
/// 삼백 아이의 풀무 — Moye, Skill, Common, cost 0, self. Gain 3 (+1/L) Block. 【짝】 next turn gain 1 energy
/// (game EnergyNextTurnPower, as 기 모으기).
/// Reworked 2026-10-16 (near-clone pass): it was "Block, 【짝】 draw 1" — the starter 막야 막기 at cost 0. Now the
/// bellows stoke the furnace for the next turn instead.
/// Lore: 300 children worked the bellows.
/// </summary>
public sealed class MoyeBellows : GanjiangMoyeCard
{
    public MoyeBellows() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(3);
        WithBlockPerLevel(1);
        WithEnergy(1);
        WithTip(typeof(EnergyNextTurnPower));
    }

    protected override SwordId Side => SwordId.Moye;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        await CommonActions.CardBlock(this, cardPlay);
        if (pair)
            await PowerCmd.Apply<EnergyNextTurnPower>(choiceContext, Owner.Creature, DynamicVars.Energy.BaseValue,
                Owner.Creature, this);
    }
}
