using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

/// <summary>
/// 섬광 (Common): cost 2. 12 damage (+1/L). Costs 1 less for each Light you have; when played, spend Light equal to
/// that reduction (at most 2). Light pays the energy cost instead of adding damage.
/// Reworked 2026-10-16 (user request: Light spending too samey): it was "7 damage, costs 0 at 3+ Light" — a second
/// "3 Light threshold" card next to 왕위 복귀. Now Light is spent like energy. 「'빛의 검'」.
/// Implementation (review fix 2026-10-16: Light was spent even when the discount was not used):
///  - Cost hook: discount = min(Light, 2, cost reaching this hook), so a cost already lowered by something else
///    (local "costs 0 this turn", Dainsleif-style hooks…) never takes more Light than it can still reduce. The game
///    runs the same hook for the hand cost label (CardEnergyCost.GetWithModifiers), so the label shows the discount.
///  - What is spent: in <see cref="AfterEnergySpent"/> (CardModel.SpendEnergy, right after the energy is paid and
///    before OnPlayWrapper / BeforeCardPlayed — verified in the decompiled CardModel.SpendResources) the card
///    compares its full cost WITH and WITHOUT the Light discount (local modifiers and every other hook, incl. the
///    late "free" hooks like FreeAttackPower/Corruption, still apply). The difference is the energy the Light really
///    saved; OnCardPlay spends exactly that and clears it. A play that paid nothing (CardCmd.AutoPlay without
///    SpendResources) never reaches AfterEnergySpent, so it spends 0; extra repeats of one play (play count &gt; 1)
///    spend 0 too.
/// </summary>
public sealed class SolaisLightFlash : SolaisCard
{
    public const int PrintedCost = 2;

    /// <summary>Light that reduced the energy paid for the play being resolved (set in AfterEnergySpent).</summary>
    private int _lightPaid;

    /// <summary>True while computing the cost without the Light discount (AfterEnergySpent only).</summary>
    private bool _suppressLightDiscount;

    public SolaisLightFlash() : base(PrintedCost, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(12);
        WithDamagePerLevel(1);
        WithVar("MaxSpend", PrintedCost);
        WithTip(typeof(SolaisLightPower));
    }

    /// <summary>Light discount on a cost of <paramref name="cost"/>: min(Light, 2, cost).</summary>
    private int DiscountFor(decimal cost) =>
        (int)Math.Min(Math.Min(Light, PrintedCost), Math.Max(0m, Math.Floor(cost)));

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        var changed = base.TryModifyEnergyCostInCombat(card, originalCost, out modifiedCost);
        if (!ReferenceEquals(card, this) || _suppressLightDiscount) return changed;
        var discount = DiscountFor(modifiedCost);
        if (discount <= 0) return changed;
        modifiedCost -= discount;
        return true;
    }

    public override Task AfterEnergySpent(CardModel card, int amount)
    {
        if (!ReferenceEquals(card, this) || EnergyCost.CostsX) return Task.CompletedTask;
        int withoutLight;
        _suppressLightDiscount = true;
        try { withoutLight = EnergyCost.GetWithModifiers(CostModifiers.All); }
        finally { _suppressLightDiscount = false; }
        var withLight = EnergyCost.GetWithModifiers(CostModifiers.All);
        _lightPaid = Math.Clamp(withoutLight - withLight, 0, Math.Min(Light, PrintedCost));
        return Task.CompletedTask;
    }

    protected override bool ShouldGlowGoldInternal => Light > 0;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var spend = Math.Min(_lightPaid, Light);
        _lightPaid = 0;
        if (spend > 0) await SolaisLightSpend.TrySpend(choiceContext, Owner, spend, this);
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}
