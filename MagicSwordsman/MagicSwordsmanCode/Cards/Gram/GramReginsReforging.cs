using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Gram;

/// <summary>
/// 레긴의 재단조 — Gram, Skill, Uncommon, cost 1 (0 from Gram level 3), self, Exhaust.
/// This combat Gram counts as 2 levels higher (max 5). Draw 1 card per level that could not be gained over 5.
/// Implemented with the per-combat level bonus <see cref="SwordCombat.CombatLevelBonusKey"/>, which
/// <see cref="SwordCombat.LevelOf"/> adds — so Gram's current-sword effect and every Gram card scale with it.
/// Lore: Regin re-forged the two pieces of Gram.
/// </summary>
public sealed class GramReginsReforging : GramCard
{
    public const int LevelGain = 2;

    public GramReginsReforging() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("Levels", LevelGain);
        WithKeywords(CardKeyword.Exhaust);
    }

    public override int? CostAtLevel(int level) => level >= 3 ? 0 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var state = SwordCombat.Get(Owner);
        if (state == null) return;

        var before = SwordCombat.LevelOf(Owner, SwordId.Gram);
        var after = Math.Min(SwordRegistry.MaxLevel, before + LevelGain);
        var gained = after - before;
        if (gained > 0) state.AddCounter(SwordId.Gram, SwordCombat.CombatLevelBonusKey, gained);

        // refresh the "현재 검" icon number / tooltip
        await SwordCombat.EnsureCurrentSwordPower(Owner, choiceContext);

        var overflow = LevelGain - gained;
        if (overflow > 0) await CardPileCmd.Draw(choiceContext, overflow, Owner);
        MainFile.Logger.Info($"[Gram] Regin's reforging: level {before} -> {after} this combat, draw {overflow}");
    }
}
