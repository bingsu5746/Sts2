using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Dainsleif;

// 다인슬레이프 메커니즘 카드 3장 (2026-10-17, 사용자 요청 "검의 메커니즘을 제대로 반영하는 카드"). Dáinsleif puts played
// cards back into the draw pile, so WHAT IS ON TOP of the draw pile matters (돌아온 칼 picks from it, 되살아난 전사들 plays
// it), and its cost (no Block while current) gets a card that plays around it (방패를 버리다). Common 1 · Uncommon 2.

/// <summary>
/// 돌아온 칼 (Common skill, cost 0): look at the top 3 cards of your draw pile (4 from level 3) and put 1 of them into
/// your hand. Cost 0, so it is discarded normally (Dáinsleif only returns cards that cost 1+). Pairs with 햐드닝아비그,
/// which puts returned cards on top.
/// </summary>
public sealed class DainsleifBladeReturns : DainsleifCard
{
    public DainsleifBladeReturns() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithCalculatedVar("Look", 3, static (card, _) => card is MagicSwordCard { SwordLevel: >= 3 } ? 1 : 0);
    }

    private int Look => SwordLevel >= 3 ? 4 : 3;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.ShuffleIfNecessary(choiceContext, Owner);
        var draw = PileType.Draw.GetPile(Owner);
        var top = draw.Cards.Take(Look).ToList();
        if (top.Count == 0) return;
        var picked = (await CardSelectCmd.FromCombatPile(choiceContext, draw, Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1), top.Contains)).FirstOrDefault();
        if (picked != null) await CardPileCmd.Add(picked, PileType.Hand);
    }
}

/// <summary>
/// 되살아난 전사들 (Uncommon attack): 6 damage (+1/L). If the top card of your draw pile is an Attack, play it (free,
/// random target, game CardCmd.AutoPlay like Havoc/Cascade). A card played this way does not chain (IsAutoPlay check),
/// so two copies cannot loop. Lore (Skáldskaparmál, high confidence): every night Hildr wakes the slain of the
/// Hjaðningavíg with her spells and they fight again.
/// </summary>
public sealed class DainsleifRisenSlain : DainsleifCard
{
    public DainsleifRisenSlain() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(6);
        WithDamagePerLevel(1);
    }

    private CardModel? TopCard =>
        IsMutable && Owner?.PlayerCombatState != null ? PileType.Draw.GetPile(Owner).Cards.FirstOrDefault() : null;

    protected override bool ShouldGlowGoldInternal => TopCard is { Type: CardType.Attack };

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        // Same guard as the game's CardPileCmd.AutoPlayFromDrawPile: if this hit ended the combat, play nothing.
        if (cardPlay.IsAutoPlay || Owner.Creature.IsDead || CombatManager.Instance.IsOverOrEnding) return;
        if (TopCard is not { Type: CardType.Attack } top) return;
        await CardPileCmd.Add(top, PileType.Play);
        await CardCmd.AutoPlay(choiceContext, top, null);
    }
}

/// <summary>
/// 방패를 버리다 (Uncommon power): whenever a card would give you Block while Dáinsleif is your current sword (its cost
/// makes that Block 0), deal 5 (+⌊L/2⌋) damage to a random enemy instead. <see cref="DainsleifShieldCastAsidePower"/>.
/// </summary>
public sealed class DainsleifShieldCastAside : DainsleifCard
{
    public const int BaseDamage = 5;

    public DainsleifShieldCastAside() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithCalculatedVar("HitDamage", BaseDamage, static (card, _) => card is MagicSwordCard m ? m.SwordLevel / 2 : 0);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<DainsleifShieldCastAsidePower>()));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<DainsleifShieldCastAsidePower>(choiceContext, Owner.Creature, BaseDamage + SwordLevel / 2,
            Owner.Creature, this);
    }
}
