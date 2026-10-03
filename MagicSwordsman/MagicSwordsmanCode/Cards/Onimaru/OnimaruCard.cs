using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Onimaru;

/// <summary>
/// Base of every 오니마루 card (content doc §2.8). Onimaru cards deal no damage of their own: they either give a
/// 【명령】 (<see cref="Command"/>: one auto attack of the current kind, current-sword bonus included, no 25% roll) or
/// change the auto-attack kind (<see cref="ChangeKind"/>; every kind card except 거합 strikes once immediately).
/// Playing an Onimaru card switches to Onimaru BEFORE the effect (it is already present, so this is not a summon:
/// content doc 0.4 #12), so the current-sword bonus applies to these attacks.
/// </summary>
public abstract class OnimaruCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : MagicSwordCard(cost, type, rarity, target)
{
    public sealed override SwordId? Sword => SwordId.Onimaru;

    /// <summary>Energy cost at an Onimaru level (e.g. "3단계부터 비용 1"); null = printed cost.</summary>
    protected virtual int? CostAtLevel(int level) => null;

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) || CostAtLevel(SwordLevel) is not { } c || c >= originalCost) return false;
        modifiedCost = c;
        return true;
    }

    /// <summary>【명령】 <paramref name="times"/> times (target = the card's chosen enemy for 거합/퇴마).</summary>
    protected async Task Command(PlayerChoiceContext choiceContext, CardPlay cardPlay, int times = 1)
    {
        for (var i = 0; i < times; i++)
            await OnimaruAttack.Command(Owner, cardPlay.Target, choiceContext, this);
    }

    /// <summary>Changes the kind; strikes once with it when <paramref name="strikeNow"/>.</summary>
    protected async Task ChangeKind(PlayerChoiceContext choiceContext, OnimaruKind kind, bool strikeNow, int iaiAdd = 0)
    {
        await OnimaruAttack.SetKind(Owner, kind, choiceContext, iaiAdd);
        if (strikeNow) await OnimaruAttack.Perform(Owner, kind, null, choiceContext, this, assumeCurrent: false);
    }

    /// <summary>
    /// Text var: the per-hit damage (or block for 호위) of one <paramref name="kind"/> attack as it would be after this
    /// card is played (Onimaru current). In combat only; outside combat the level-0 base value is shown.
    /// </summary>
    protected void WithKindValueVar(string name, OnimaruKind kind)
    {
        var baseNumbers = OnimaruAttack.BaseNumbers(kind, 0);
        var baseValue = kind == OnimaruKind.Goei ? baseNumbers.Block : baseNumbers.PerHit;
        WithCalculatedVar(name, baseValue, (card, _) =>
        {
            if (card.Owner is not { } owner) return 0;
            var n = OnimaruAttack.Compute(owner, kind, assumeCurrent: true);
            return (kind == OnimaruKind.Goei ? n.Block : n.PerHit) - baseValue;
        });
    }

    /// <summary>Text var: number of hits of one 난무 attack (2 at level 0).</summary>
    protected void WithRanbuHitsVar(string name)
    {
        var baseValue = OnimaruAttack.BaseNumbers(OnimaruKind.Ranbu, 0).Hits;
        WithCalculatedVar(name, baseValue, (card, _) =>
            card is MagicSwordCard m ? OnimaruAttack.BaseNumbers(OnimaruKind.Ranbu, m.SwordLevel).Hits - baseValue : 0);
    }
}
