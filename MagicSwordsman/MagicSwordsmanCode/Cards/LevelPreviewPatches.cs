using HarmonyLib;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards;

/// <summary>
/// Sword-level numbers on card faces OUTSIDE the hand (user request 2026-10-16: "간장 막야가 5강이면 덱에서 봐도 방어도가
/// 바뀌어야지, 드로우 했을 때만이 아니라").
///
/// Why the numbers were missing (verified in the decompiled game): the level bonuses reach the card text in three ways,
/// and all three only run in combat —
///  1. Damage/Block (+N per level, LevelDamageBonus, the twin cards' Moye block, the own sword's current effect…) come
///     from damage/block HOOKS. <c>CardModel.UpdateDynamicVarPreview</c> passes <c>runGlobalHooks = true</c> only for a
///     card in the Hand/Play pile of a combat; DamageVar/BlockVar otherwise show the bare base value.
///  2. Other numbers ({Hits}, {HpLoss}, WithLevelVar…) are CalculatedVars; <c>CalculatedVar.Calculate</c> uses the
///     multiplier only while <c>CombatManager.IsInProgress &amp;&amp; card.CombatState != null</c>, else base + 0.
///  3. Level-based cost reductions are <c>TryModifyEnergyCostInCombat</c> hooks, which
///     <c>CardEnergyCost.GetWithModifiers</c> runs only when <c>card.CombatState != null</c>.
///
/// What this file does (display only — nothing here changes a played number, a pile, an RNG or any saved state, so it
/// is multiplayer-safe; every client computes the same text from the synced sword levels):
///  A. <see cref="CalculatePatch"/>: outside combat (more exactly: whenever the game would use multiplier 0 because the
///     card has no combat state — deck, rewards, shop, card selection, rest site, the deck view during a fight), a SWORD
///     card's CalculatedVar is evaluated with its multiplier anyway. Every multiplier in this mod is pure and reads
///     combat-only values defensively (0 outside combat), so only the level part shows up.
///  B. <see cref="PreviewPatch"/>: whenever the game skips the hooks (runGlobalHooks == false), a sword card's
///     Damage/CalculatedDamage and Block/CalculatedBlock preview gets the ADDITIVE bonuses that would apply when it is
///     played: the card's own Modify*Additive (level scaling) + the additive current-sword effect of the card's OWN sword
///     (playing a sword card switches to its sword first, exactly what the in-hand preview assumes —
///     SwordCombat.EffectiveSwordFor). The game's "diff" formatter then shows the raised number in green.
///  C. <see cref="EnergyCostPatch"/>: when the card has no combat state, a sword card's cost label shows its level-based
///     cost (the card's own TryModifyEnergyCostInCombat + its sword's TryModifyEnergyCost, e.g. Gram) in green.
///
/// Rules:
///  - Shared (colourless, Sword == null) cards are left alone: they never scale with sword levels (SwordContext.LevelFor)
///    and the sword effect they carry is whatever sword is current in the fight, which only exists in combat.
///  - Multiplicative / situational sword effects (Caladbolg's single-enemy penalty, Dainsleif's "no block", target or
///    enemy dependent bonuses) are NOT shown outside the hand — they depend on the fight.
///  - In the hand the game runs the real hooks and this file does nothing (no double counting).
///  - In combat, a combat card outside the hand (draw/discard/exhaust pile view) uses the sword a real play would use
///    (SwordCombat.EffectiveSwordFor, review fix 2026-10-16): its own sword as a preview when it could be switched to,
///    otherwise the CURRENT sword with the normal (non-preview) context — also when its own sword is already current.
///  - A card with no combat state (CombatState == null: deck, rewards, shop, and the deck view DURING a fight) is
///    evaluated inside a <see cref="NeutralCombatInputs"/> scope (review fix 2026-10-16): the live fight's inputs read
///    as outside combat — SwordCombat.Get/GetOrCreate return null (no current/present sword, no switch count, no
///    temporary combat level bonus), SolaisLight.Get returns 0, and the multipliers that read the player's combat
///    state (CommonCardRules.CombatOwner, current Block, hand contents, damage taken, enemies…) return 0. The own
///    sword's effect is shown with the out-of-combat preview context.
/// </summary>
internal static class LevelPreviewPatches
{
    private static readonly AccessTools.FieldRef<DynamicVar, AbstractModel?> VarOwner =
        AccessTools.FieldRefAccess<DynamicVar, AbstractModel?>("_owner");

    private static readonly AccessTools.FieldRef<CalculatedVar, Func<CardModel, Creature?, decimal>?> Multiplier =
        AccessTools.FieldRefAccess<CalculatedVar, Func<CardModel, Creature?, decimal>?>("_multiplierCalc");

    private static readonly Func<CalculatedVar, DynamicVar> BaseVarOf =
        AccessTools.MethodDelegate<Func<CalculatedVar, DynamicVar>>(
            AccessTools.Method(typeof(CalculatedVar), "GetBaseVar"), virtualCall: true);

    private static readonly Func<CalculatedVar, DynamicVar> ExtraVarOf =
        AccessTools.MethodDelegate<Func<CalculatedVar, DynamicVar>>(
            AccessTools.Method(typeof(CalculatedVar), "GetExtraVar"), virtualCall: true);

    private static readonly AccessTools.FieldRef<NCard, MegaLabel> EnergyLabel =
        AccessTools.FieldRefAccess<NCard, MegaLabel>("_energyLabel");

    [ThreadStatic] private static int _neutralDepth;

    /// <summary>
    /// True while this file evaluates a card that has no combat state (see the class remarks): combat-only readers
    /// (SwordCombat.Get, SolaisLight.Get, multipliers reading the player's combat state) must read as outside combat.
    /// Display only; the scope is synchronous and never open while game actions run.
    /// </summary>
    internal static bool NeutralCombatInputs => _neutralDepth > 0;

    private readonly struct NeutralScope : IDisposable
    {
        private readonly bool _active;

        public NeutralScope(bool active)
        {
            _active = active;
            if (active) _neutralDepth++;
        }

        public void Dispose()
        {
            if (_active) _neutralDepth--;
        }
    }

    /// <summary>Opens a neutral scope when <paramref name="card"/> has no combat state.</summary>
    private static NeutralScope NeutralFor(CardModel card) => new(card.CombatState == null);

    /// <summary>
    /// The sword effects a real play of <paramref name="card"/> would get: in combat (combat card) the sword chosen by
    /// SwordCombat.EffectiveSwordFor (own sword as preview if it can be switched to, else the current sword with its
    /// normal context); otherwise (no combat state, inside the neutral scope) the card's own sword, previewed.
    /// </summary>
    private static IEnumerable<(Swords.SwordBehavior Behavior, Swords.SwordContext Ctx)> PlayEffects(
        MagicSwordCard card)
    {
        var owner = card.Owner;
        if (card.CombatState != null && SwordCombat.Get(owner) != null)
        {
            var (sword, isPreview) = SwordCombat.EffectiveSwordFor(owner, card);
            return sword is { } s ? SwordCombat.CurrentEffects(owner, s, isPreview) : [];
        }
        return card.Sword is { } own ? SwordCombat.CurrentEffects(owner, own, preview: true) : [];
    }

    /// <summary>A sword card (incl. 【조합】) of a player, i.e. one whose numbers scale with a sword level.</summary>
    private static bool IsOwnedSwordCard(CardModel? card, out MagicSwordCard swordCard)
    {
        swordCard = null!;
        if (card is not MagicSwordCard { IsSwordCard: true } m || !m.IsMutable || m.Owner == null) return false;
        swordCard = m;
        return true;
    }

    /// <summary>Same test as the game's CalculatedVar.Calculate: does the game evaluate the multiplier itself?</summary>
    private static bool GameEvaluatesMultiplier(CardModel card) =>
        CombatManager.Instance.IsInProgress && card.CombatState != null;

    /// <summary>Same test as CardModel.UpdateDynamicVarPreview: does the game run the damage/block hooks itself?</summary>
    private static bool GameRunsHooks(CardModel card)
    {
        if (card.CombatState == null) return false;
        var pile = card.Pile?.Type;
        return pile is PileType.Hand or PileType.Play || card.UpgradePreviewType == CardUpgradePreviewType.Combat;
    }

    // ------------------------------------------------------------------ bonuses

    /// <summary>
    /// The additive damage the card would get when played (per hit): its own level scaling + its own sword's additive
    /// current effect (as if it had just switched to that sword). Pure.
    /// </summary>
    internal static decimal DamageBonus(MagicSwordCard card, decimal amount, ValueProp props)
    {
        var owner = card.Owner;
        var me = owner.Creature;
        decimal sum = 0m;
        try { sum += card.ModifyDamageAdditive(null, amount, props, me, card); }
        catch (Exception e) { Warn(card, "card damage", e); }

        try
        {
            foreach (var (b, ctx) in PlayEffects(card))
                sum += b.ModifyDamageAdditive(ctx, null, amount, props, me, card);
        }
        catch (Exception e) { Warn(card, "sword damage", e); }

        return sum;
    }

    /// <summary>Block counterpart of <see cref="DamageBonus"/>.</summary>
    internal static decimal BlockBonus(MagicSwordCard card, decimal amount, ValueProp props)
    {
        var owner = card.Owner;
        var me = owner.Creature;
        decimal sum = 0m;
        try { sum += card.ModifyBlockAdditive(me, amount, props, card, null); }
        catch (Exception e) { Warn(card, "card block", e); }

        try
        {
            foreach (var (b, ctx) in PlayEffects(card))
                sum += b.ModifyBlockAdditive(ctx, me, amount, props, card, null);
        }
        catch (Exception e) { Warn(card, "sword block", e); }

        return sum;
    }

    /// <summary>The card's level-based energy cost (null = unchanged).</summary>
    private static int? LevelCost(MagicSwordCard card, int printed)
    {
        decimal cost = printed;
        if (card.TryModifyEnergyCostInCombat(card, cost, out var own)) cost = own;
        if (card.Sword is { } sword &&
            Swords.SwordRegistry.Get(sword).TryModifyEnergyCost(SwordCombat.ContextFor(card.Owner, sword), card, cost,
                out var bySword))
            cost = bySword;
        var result = Math.Max(0, (int)cost);
        return result < printed ? result : null;
    }

    private static readonly HashSet<string> Warned = [];

    private static void Warn(CardModel card, string what, Exception e) => Warn(card.Id.Entry, what, e);

    /// <summary>Logs each (source, what) failure once (these run on every card redraw).</summary>
    private static void Warn(string source, string what, Exception e)
    {
        if (Warned.Add($"{source}:{what}"))
            MainFile.Logger.Warn($"[LevelPreview] {source} {what}: {e.Message}");
    }

    // ------------------------------------------------------------------ patches

    /// <summary>A. CalculatedVar numbers ({Hits}, {HpLoss}, WithLevelVar…) of sword cards outside combat.</summary>
    [HarmonyPatch(typeof(CalculatedVar), nameof(CalculatedVar.Calculate))]
    private static class CalculatePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(CalculatedVar __instance, Creature? target, ref decimal __result)
        {
            try
            {
                if (!IsOwnedSwordCard(VarOwner(__instance) as CardModel, out var card)) return true;
                if (GameEvaluatesMultiplier(card)) return true;
                if (Multiplier(__instance) is not { } calc) return true;
                using (NeutralFor(card))
                    __result = BaseVarOf(__instance).BaseValue + ExtraVarOf(__instance).BaseValue * calc(card, target);
                return false;
            }
            catch (Exception e)
            {
                var source = (VarOwner(__instance) as CardModel)?.Id.Entry ?? "?";
                Warn(source, $"calculated var {__instance.Name}", e);
                return true;
            }
        }
    }

    /// <summary>B. Damage/Block previews of sword cards wherever the game does not run the hooks.</summary>
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.UpdateDynamicVarPreview))]
    private static class PreviewPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CardModel __instance, DynamicVarSet dynamicVarSet)
        {
            try
            {
                if (!IsOwnedSwordCard(__instance, out var card)) return;
                if (!ReferenceEquals(dynamicVarSet, card.DynamicVars)) return; // not the enchantment's vars
                if (card.RunState == null && card.CombatState == null) return; // the game skipped the preview too
                if (GameRunsHooks(card)) return;

                using var neutral = NeutralFor(card);
                foreach (var v in dynamicVarSet.Values)
                {
                    decimal bonus = v switch
                    {
                        DamageVar d => DamageBonus(card, d.BaseValue, d.Props),
                        CalculatedDamageVar cd => DamageBonus(card, cd.PreviewValue, cd.Props),
                        BlockVar b => BlockBonus(card, b.BaseValue, b.Props),
                        CalculatedBlockVar cb => BlockBonus(card, cb.PreviewValue, cb.Props),
                        _ => 0m,
                    };
                    if (bonus != 0m) v.PreviewValue = Math.Max(0m, v.PreviewValue + bonus);
                }
            }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"[LevelPreview] preview {__instance.Id.Entry}: {e.Message}");
            }
        }
    }

    /// <summary>C. Level-based energy cost of sword cards that have no combat state (deck, rewards, shop…).</summary>
    [HarmonyPatch(typeof(NCard), "UpdateEnergyCostVisuals")]
    private static class EnergyCostPatch
    {
        [HarmonyPostfix]
        private static void Postfix(NCard __instance)
        {
            try
            {
                if (__instance.Visibility != ModelVisibility.Visible) return;
                if (!IsOwnedSwordCard(__instance.Model, out var card)) return;
                if (card.CombatState != null || card.EnergyCost.CostsX) return; // the game applies the cost hooks itself
                var printed = card.EnergyCost.GetWithModifiers(CostModifiers.All);
                int? levelCost;
                using (NeutralFor(card)) levelCost = printed <= 0 ? null : LevelCost(card, printed);
                if (levelCost is not { } cost) return;
                var label = EnergyLabel(__instance);
                label.SetTextAutoSize(cost.ToString());
                label.AddThemeColorOverride(ThemeConstants.Label.FontColor, StsColors.green);
                label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, StsColors.energyGreenOutline);
            }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"[LevelPreview] energy cost: {e.Message}");
            }
        }
    }
}
