using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

/// <summary>
/// 섬광 (Common): cost 2. 12 damage (+1/L). Costs 1 less for each Light you have; when played, spend Light equal to
/// that reduction (at most 2). Light pays the energy cost instead of adding damage.
/// Reworked 2026-10-16 (user request: Light spending too samey): it was "7 damage, costs 0 at 3+ Light" — a second
/// "3 Light threshold" card next to 왕위 복귀. Now Light is spent like energy. 「'빛의 검'」.
/// Implementation: the cost hook (SolaisCard.CostAtLevel, which only lowers) reads Light; OnCardPlay spends
/// min(Light, printed cost). Light does not change between paying the cost and OnCardPlay.
/// </summary>
public sealed class SolaisLightFlash : SolaisCard
{
    public const int PrintedCost = 2;

    public SolaisLightFlash() : base(PrintedCost, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(12);
        WithDamagePerLevel(1);
        WithVar("MaxSpend", PrintedCost);
        WithTip(typeof(SolaisLightPower));
    }

    private int Reduction => Math.Min(Light, PrintedCost);

    protected override int? CostAtLevel(int level) => Reduction > 0 ? PrintedCost - Reduction : null;

    protected override bool ShouldGlowGoldInternal => Light > 0;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var spend = Reduction;
        if (spend > 0) await SolaisLightSpend.TrySpend(choiceContext, Owner, spend, this);
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}
