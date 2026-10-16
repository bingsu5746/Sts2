using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
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
/// 하늘 가르기 (Common): cost 2. 9 damage (+2/L), +3 for each other Attack you played earlier this turn.
/// Reworked 2026-10-16 (near-clone pass): it was a plain "11 damage" (one of four "deal X damage" Caladbolg cards).
/// Now the blade grows in the air with every swing before it. Count: CombatHistory CardPlaysStarted this turn
/// (일섬 / Issen pattern), this card's own current play excluded.
/// Source: 「내리치려 하면 공중에서 거대하게 커졌다」.
/// </summary>
public sealed class CaladbolgRainbowArc : CaladbolgCard
{
    public const int PerAttack = 3;

    public CaladbolgRainbowArc() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(9);
        WithDamagePerLevel(2);
        WithVar("PerAttack", PerAttack);
        WithCalculatedVar("Growth", 0, static (card, _) => card is CaladbolgRainbowArc r ? r.EarlierAttacks() * PerAttack : 0);
    }

    private int EarlierAttacks()
    {
        if (!IsMutable || CombatState == null || Owner?.PlayerCombatState == null) return 0;
        var owner = Owner;
        var count = CombatManager.Instance.History.CardPlaysStarted.Count(e =>
            e.HappenedThisTurn(CombatState) && e.CardPlay.Card.Owner == owner && e.CardPlay.Card.Type == CardType.Attack);
        var inPlay = Pile is { Type: PileType.Play };
        if (inPlay) count -= 1;
        return Math.Max(0, count);
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        var bonus = base.ModifyDamageAdditive(target, amount, props, dealer, cardSource);
        if (ReferenceEquals(cardSource, this) && props.IsPoweredAttack()) bonus += EarlierAttacks() * PerAttack;
        return bonus;
    }

    protected override bool ShouldGlowGoldInternal => EarlierAttacks() > 0;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}
