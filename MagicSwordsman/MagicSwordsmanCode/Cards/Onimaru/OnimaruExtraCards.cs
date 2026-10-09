using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Onimaru;

// 오니마루 추가 카드 4장 (Common 2 · Uncommon 1 · Rare 1). Like the rest of the pool they deal no damage of their own:
// everything goes through OnimaruAttack (auto-attack table, content doc §1.2). New here: attacks of an EXPLICIT kind
// that leave the chosen kind untouched, and a 【연속】 command. Lore (Taiheiki): Hōjō Tokimasa was haunted every night
// by a small oni in his dreams; the sword fell over on its own and cut the head off the oni-shaped silver stand of a
// brazier, and the haunting stopped.

/// <summary>쓰러지는 칼 (Common): Onimaru makes one 거합 on the target whatever the kind; if that kills it, gain 1 Energy.</summary>
public sealed class OnimaruFallingBlade : OnimaruCard
{
    public OnimaruFallingBlade() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithKindValueVar("IaiDamage", OnimaruKind.Iai);
        WithEnergy(1);
        WithTip(OnimaruTips.Iai);
        WithTip(StaticHoverTip.Fatal);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null || !target.IsAlive) return;
        var fatal = target.Powers.All((PowerModel p) => p.ShouldOwnerDeathTriggerFatal());
        await OnimaruAttack.Perform(Owner, OnimaruKind.Iai, target, choiceContext, this, assumeCurrent: false);
        if (fatal && target.IsDead) await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
    }
}

/// <summary>불침번 (Common): Onimaru guards once (호위 Block) whatever the kind. Draw 1 card.</summary>
public sealed class OnimaruNightWatch : OnimaruCard
{
    public OnimaruNightWatch() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithKindValueVar("AutoBlock", OnimaruKind.Goei);
        WithCards(1);
        WithTip(OnimaruTips.Goei);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await OnimaruAttack.Perform(Owner, OnimaruKind.Goei, null, choiceContext, this, assumeCurrent: false);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}

/// <summary>칼집을 박차다 (Uncommon): 【명령】. 【연속】 (Onimaru was already current): 【명령】 once more.</summary>
public sealed class OnimaruLeapFromSheath : OnimaruCard
{
    private SwordId? _swordBeforePlay;

    public OnimaruLeapFromSheath() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithKindValueVar("IaiDamage", OnimaruKind.Iai);
    }

    /// <summary>Remembers the current sword right before the switch (GramCard 【연속】 pattern).</summary>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, this) && IsMutable && Owner != null)
            _swordBeforePlay = SwordCombat.CurrentSword(Owner);
        return base.BeforeCardPlayed(cardPlay);
    }

    protected override bool ShouldGlowGoldInternal =>
        IsMutable && Owner != null && SwordCombat.CurrentSword(Owner) == SwordId.Onimaru;

    protected override Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Command(choiceContext, cardPlay, _swordBeforePlay == SwordId.Onimaru ? 2 : 1);
}

/// <summary>다섯 자세 (Rare, cost 2): Onimaru strikes once in each of the five kinds (kind unchanged).</summary>
public sealed class OnimaruFiveStances : OnimaruCard
{
    private static readonly OnimaruKind[] Order =
        [OnimaruKind.Goei, OnimaruKind.Taima, OnimaruKind.Iai, OnimaruKind.Ranbu, OnimaruKind.Giri];

    public OnimaruFiveStances() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithTip(OnimaruTips.Iai);
        WithTip(OnimaruTips.Taima);
        WithTip(OnimaruTips.Ranbu);
        WithTip(OnimaruTips.Giri);
        WithTip(OnimaruTips.Goei);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<WeakPower>()));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        foreach (var kind in Order)
            await OnimaruAttack.Perform(Owner, kind, cardPlay.Target, choiceContext, this, assumeCurrent: false);
    }
}
