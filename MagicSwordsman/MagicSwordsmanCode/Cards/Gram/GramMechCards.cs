using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Gram;

// 그람 메커니즘 카드 3장 (mech2, 2026-10-17, user request "현재의 검들의 메커니즘을 제대로 반영하는 카드들"):
// Gram's cards so far all turned its level into "a bigger attack" (+ 【연속】 and Regin's level boost). These use the
// level and the level-5 cost in other ways: the Nibelung curse as a payoff, the level as a resource to spend, and the
// level as defense.

/// <summary>
/// 안드바리의 저주 — Gram, Attack, Uncommon, cost 1, one enemy. Deal 7 (+⌊L/2⌋) damage, +4 for each Curse card you
/// have in this combat (all piles; max +12). Turns Gram's level-5 cost (니벨룽의 보물, Eternal) into a payoff; also
/// counts Gram's forge-failure curse and every other curse (Tyrfing's deeds...). Count: PlayerCombatState.AllCards.
/// Lore (Reginsmál prose / Völsunga saga ch. 14, high confidence): Loki took the dwarf Andvari's gold and his ring
/// Andvaranaut; Andvari cursed the ring and the gold to be the death of whoever owned it — the hoard Fafnir later
/// guarded and Sigurd won with Gram.
/// </summary>
public sealed class GramAndvarisCurse : GramCard
{
    public const int PerCurse = 4;
    public const int MaxBonus = 12;

    public GramAndvarisCurse() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(7);
        WithVar("PerCurse", PerCurse);
        WithVar("MaxBonus", MaxBonus);
        WithCalculatedVar("CurseBonus", 0, static (card, _) => card is GramAndvarisCurse g ? g.CurseBonus : 0);
        WithTip(SwordMechTips.Nibelung);
        WithTip(SwordMechTips.GramLevel);
    }

    private int CurseBonus
    {
        get
        {
            if (!IsMutable || Owner?.PlayerCombatState == null || LevelPreviewPatches.NeutralCombatInputs) return 0;
            var curses = Owner.PlayerCombatState.AllCards.Count(c => c.Type == CardType.Curse);
            return Math.Min(MaxBonus, curses * PerCurse);
        }
    }

    protected override int LevelDamageBonus(int level) => level / 2;

    protected override int TargetDamageBonus(Creature? target) => CurseBonus;

    protected override bool ShouldGlowGoldInternal => CurseBonus > 0;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}

/// <summary>
/// 창에 부러진 칼 — Gram, Skill, Uncommon, cost 0, self, Exhaust. Only playable while Gram is level 1 or higher.
/// This combat Gram counts as 1 level LOWER (its current-sword +L and every Gram card's level numbers drop by 1); gain
/// 2 Energy. Gram's level as a resource: spend it for tempo (SeeingRed-like energy at a real price). Uses the same
/// per-combat level counter as 레긴의 재단조 (SwordCombat.CombatLevelBonusKey, here −1), so the two cancel out cleanly.
/// Lore (Völsunga saga ch. 11, high confidence): in Sigmund's last battle a one-eyed man in a hood and dark cloak
/// (Odin) stepped in front of him; Sigmund's sword struck the man's spear and broke in two.
/// </summary>
public sealed class GramBrokenOnSpear : GramCard
{
    public GramBrokenOnSpear() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithEnergy(2);
        WithVar("LevelLoss", 1);
        WithKeywords(CardKeyword.Exhaust);
        WithTip(SwordMechTips.GramLevel);
    }

    protected override bool IsPlayableExtra =>
        !IsMutable || Owner?.PlayerCombatState == null || SwordCombat.LevelOf(Owner, SwordId.Gram) >= 1;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var state = SwordCombat.Get(Owner);
        if (state == null) return;
        var before = SwordCombat.LevelOf(Owner, SwordId.Gram);
        if (before < 1) return;
        state.AddCounter(SwordId.Gram, SwordCombat.CombatLevelBonusKey, -DynamicVars["LevelLoss"].IntValue);
        await SwordCombat.EnsureCurrentSwordPower(Owner, choiceContext); // refresh the "현재 검" number / text
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        MainFile.Logger.Info($"[Gram] Broken on the spear: level {before} -> {SwordCombat.LevelOf(Owner, SwordId.Gram)} this combat");
    }
}

/// <summary>
/// 용의 피 — Gram, Skill, Common, cost 1, self. This turn, whenever you play an Attack, gain (Gram level + 2) Block
/// (<see cref="GramDragonBloodPower"/>, fixed when played). Gram's level as defense: the more you attack with it, the
/// more you are covered.
/// Lore (Nibelungenlied, 3rd Aventiure, high confidence): Siegfried — whose sword is Balmung, Gram's name there —
/// bathed in the blood of a dragon he slew and his skin became horn-hard, except one spot between his shoulders
/// where a linden leaf had fallen.
/// </summary>
public sealed class GramDragonBlood : GramCard
{
    public GramDragonBlood() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithLevelVar("BloodBlock", 2, 1);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<GramDragonBloodPower>()));
        WithTip(StaticHoverTip.Block);
        WithTip(SwordMechTips.GramLevel);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<GramDragonBloodPower>(choiceContext, Owner.Creature, LevelVar("BloodBlock"),
            Owner.Creature, this);
    }
}
