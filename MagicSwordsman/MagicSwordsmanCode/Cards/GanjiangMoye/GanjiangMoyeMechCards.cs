using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

// 간장·막야 메커니즘 카드 3장 (mech2, 2026-10-17, user request "현재의 검들의 메커니즘을 제대로 반영하는 카드들"):
// every earlier Ganjiang/Moye card read 【짝】 the same way ("alternate -> bonus this play"). These read the rhythm
// differently: the number of pairs over the whole combat, NOT alternating on purpose (Moye twice in a row doubles down
// on Moye's own Block bonus), and choosing which blade leads after a card.

/// <summary>
/// 삼 년의 담금질 — Ganjiang, Attack, Uncommon, cost 2, one enemy. Deal 8 (+2/L) damage, +2 for each 【짝】 that
/// triggered this combat (max +20). No pair effect of its own (it uses up a twin card's "primed" flag like other
/// no-pair cards). Counter: PairRules.PairsThisCombat.
/// Lore (Wuyue Chunqiu, high confidence): King Helü ordered the swords; the iron would not melt, and only after the
/// couple's long labour were they finished — three years in the Soushen ji version.
/// </summary>
public sealed class GanjiangThreeYears : GanjiangMoyeCard
{
    public const int PerPair = 2;
    public const int MaxBonus = 20;

    public GanjiangThreeYears() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(8);
        WithDamagePerLevel(2);
        WithVar("PerPair", PerPair);
        WithVar("MaxBonus", MaxBonus);
        WithCalculatedVar("PairCount", 0, static (card, _) => card is GanjiangThreeYears g ? g.Pairs : 0);
        WithTip(SwordMechTips.Pair);
        WithTip(SwordMechTips.PairBlades);
    }

    protected override SwordId Side => SwordId.Ganjiang;
    protected override bool HasPairEffect => false;

    private int Pairs => IsMutable && Owner?.PlayerCombatState != null && !LevelPreviewPatches.NeutralCombatInputs
        ? PairRules.PairsThisCombat(Owner)
        : 0;

    protected override int ExtraDamage(Creature? target) => Math.Min(MaxBonus, Pairs * PerPair);

    protected override bool ShouldGlowGoldInternal => Pairs > 0;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_heavy_blunt")
            .Execute(choiceContext);
    }
}

/// <summary>
/// 홀로 남은 막야 — Moye, Skill, Common, cost 1, self. Gain 5 (+1/L) Block. If Moye was ALREADY the current sword
/// right before this card (you did not alternate), gain that Block again. The anti-【짝】 card: two Moye cards in a row
/// get Moye's current-sword Block bonus twice on this one. No pair effect (uses up a twin card's "primed" flag).
/// Lore (Soushen ji, high confidence): Ganjiang went to the King of Chu alone and was executed; his wife Moye, left
/// behind with child, raised their son Chi alone.
/// </summary>
public sealed class MoyeAlone : GanjiangMoyeCard
{
    public MoyeAlone() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(5);
        WithBlockPerLevel(1);
        WithTip(SwordMechTips.PairBlades);
        WithTip(SwordMechTips.CurrentSword);
    }

    protected override SwordId Side => SwordId.Moye;
    protected override bool HasPairEffect => false;

    protected override bool ShouldGlowGoldInternal =>
        IsMutable && Owner?.PlayerCombatState != null && SwordCombat.CurrentSword(Owner) == SwordId.Moye;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        var alone = SwordBeforePlay == SwordId.Moye;
        await CommonActions.CardBlock(this, cardPlay);
        if (alone) await CommonActions.CardBlock(this, cardPlay);
    }
}

/// <summary>
/// 암검만 바치다 — Moye, Skill, Uncommon, cost 0, self. 【짝】 draw 1 card. Then choose Ganjiang or Moye: it becomes
/// the current sword. Lets you pick which blade leads: the next card's 【짝】 (it pairs with the OTHER blade) and which
/// current-sword effect (attack or Block) shared cards carry. Choice screen: the sword token cards (SwordAcquisition
/// .PickSword, like 검 바꾸기), which show each blade's effect.
/// Lore (Soushen ji, high confidence): Ganjiang brought only the female sword to the King of Chu and hid the male one.
/// </summary>
public sealed class MoyeFemaleBladeOnly : GanjiangMoyeCard
{
    public MoyeFemaleBladeOnly() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithCards(1);
        WithTip(SwordMechTips.Pair);
        WithTip(SwordMechTips.PairBlades);
    }

    protected override SwordId Side => SwordId.Moye;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        if (pair) await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);

        var options = new List<SwordId>();
        foreach (var s in new[] { SwordId.Ganjiang, SwordId.Moye })
            if (SwordCombat.IsCurrent(Owner, s) || SwordCombat.CanSwitchTo(Owner, s, SwitchReason.SwapEffect))
                options.Add(s);
        if (options.Count < 2) return;

        var chosen = await SwordAcquisition.PickSword(Owner, options, choiceContext, canSkip: false,
            new LocString("card_selection", "MAGICSWORDSMAN-CHOOSE_LEAD_BLADE"));
        if (chosen is { } sword && !SwordCombat.IsCurrent(Owner, sword))
            await SwordCombat.SwitchTo(Owner, sword, choiceContext, SwitchReason.SwapEffect);
    }
}
