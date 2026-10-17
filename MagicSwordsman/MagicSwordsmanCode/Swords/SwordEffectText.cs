using MagicSwordsman.MagicSwordsmanCode.Cards.Tyrfing;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;

namespace MagicSwordsman.MagicSwordsmanCode.Swords;

/// <summary>
/// Short player-facing text of a sword's current-sword effect and cost with the numbers already computed
/// (user request 2026-10-15 "괄호로 몇 강화마다 몇 이런 거 싹 지우고 코드로만 계산되게"). Every number comes from the
/// same functions the behaviors use (GanjiangBehavior.DamageBonus, DurandalBehavior.ReductionFor, ...), so the text can
/// never disagree with the rules, and no text explains per-level scaling any more.
///
/// Used by: the current-sword power (<see cref="CurrentSwordPower"/>), the sword token cards (hover tips wherever a sword
/// is offered or picked), the floating-sword tooltip, the rest-site forge page, the "new sword" popup and the tomb event
/// buttons. Texts: events table, MAGICSWORDSMAN-SWORD_FX.* (fragment loc_fragments/ui2.&lt;lang&gt;.events.json).
/// </summary>
public static class SwordEffectText
{
    public const string Table = "events";
    private const string Prefix = "MAGICSWORDSMAN-SWORD_FX";

    private static LocString Loc(string part) => new(Table, $"{Prefix}.{part}");

    private static string Fmt(string part, params (string Name, object Value)[] vars)
    {
        try
        {
            var loc = Loc(part);
            foreach (var (name, value) in vars)
            {
                switch (value)
                {
                    case int i: loc.Add(name, (decimal)i); break;
                    case decimal d: loc.Add(name, d); break;
                    case LocString l: loc.Add(name, l); break;
                    default: loc.Add(name, value.ToString() ?? ""); break;
                }
            }
            return loc.GetFormattedText();
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordEffectText] {part}: {e.Message}");
            return "";
        }
    }

    /// <summary>Kusanagi's 50% (rounded down, min 1; 0 stays 0) — same rule as SwordContext.Scale.</summary>
    private static int Half(int full) => full == 0 ? 0 : Math.Max(1, full / 2);

    private static int Scale(int full, bool inherited) => inherited ? Half(full) : full;

    /// <summary>
    /// Durandal's per-hit reduction as DurandalBehavior.ModifyDamageAdditive applies it: the level reduction (halved when
    /// inherited) + DurandalBehavior.BonusReduction (단단한 날 / 롤랑의 최후, never halved) when a player is given.
    /// </summary>
    private static int DurandalReduction(Player? player, int level, bool inherited) =>
        Scale(DurandalBehavior.ReductionFor(level), inherited) +
        (player != null ? DurandalBehavior.BonusReduction(player.Creature) : 0);

    /// <summary>The level a text should use: the owner's level of the sword, 0 without a player.</summary>
    public static int LevelOf(Player? player, SwordId sword) => player != null ? SwordCombat.LevelOf(player, sword) : 0;

    /// <summary>
    /// " · 공용 카드 +N" when shared cards get a different number (they always use the level-0 bonus), else "".
    /// </summary>
    private static string CommonNote(int swordValue, int commonValue, string part = "COMMON_NOTE") =>
        swordValue == commonValue ? "" : Fmt(part, ("Value", commonValue));

    /// <summary>
    /// What the sword does while it is the current sword, with live numbers. <paramref name="inherited"/>: the half-strength
    /// version Kusanagi passes on. <paramref name="withPartner"/>: 간장/막야 also show the other blade (token / lore pages).
    /// </summary>
    public static string Effect(SwordId sword, Player? player, int level, bool inherited = false, bool withPartner = false)
    {
        var half = inherited ? "_HALF" : "";
        switch (sword)
        {
            case SwordId.Gram:
                return Fmt("GRAM", ("Damage", Scale(level, inherited)));
            case SwordId.Ganjiang:
            {
                var full = Scale(GanjiangBehavior.DamageBonus(level), inherited);
                var common = Scale(GanjiangBehavior.DamageBonus(0), inherited);
                var text = Fmt("GANJIANG", ("Damage", full)) + CommonNote(full, common);
                return withPartner ? text + "\n" + Fmt("PAIR_MOYE", ("Text", Effect(SwordId.Moye, player, level, inherited))) : text;
            }
            case SwordId.Moye:
            {
                var full = Scale(MoyeBehavior.BlockBonus(level), inherited);
                var common = Scale(MoyeBehavior.BlockBonus(0), inherited);
                var text = Fmt("MOYE", ("Block", full)) + CommonNote(full, common);
                return withPartner ? text + "\n" + Fmt("PAIR_GANJIANG", ("Text", Effect(SwordId.Ganjiang, player, level, inherited))) : text;
            }
            case SwordId.Kusanagi:
                return KusanagiEffect(player);
            case SwordId.Tyrfing:
                return Fmt("TYRFING" + half);
            case SwordId.Dainsleif:
                return Fmt("DAINSLEIF" + half);
            case SwordId.Durandal:
                return Fmt("DURANDAL", ("Reduction", DurandalReduction(player, level, inherited)));
            case SwordId.Skofnung:
                return Fmt("SKOFNUNG" + half) + "\n" + Fmt("SKOFNUNG_BURST",
                    ("Threshold", SkofnungWoundPower.BurstThreshold), ("Burst", SkofnungWoundPower.BurstDamageFor(level)));
            case SwordId.Onimaru:
            {
                var kind = player != null && SwordCombat.Get(player) != null ? OnimaruAttack.GetKind(player) : OnimaruKind.Iai;
                var full = OnimaruAttack.CurrentBonus(player!, kind, level, assumeCurrent: true);
                var bonus = inherited ? Math.Max(1, full / 2) : full;
                var line = Fmt(kind == OnimaruKind.Goei ? "ONIMARU_BONUS_BLOCK" : "ONIMARU_BONUS", ("Bonus", bonus));
                return inherited ? line : Fmt("ONIMARU") + "\n" + line;
            }
            case SwordId.ClaiomhSolais:
                return Fmt("CLAIOMHSOLAIS", ("Light", SolaisLight.ChargePerTurn), ("PerLight", SolaisLight.DamagePerLight(level)));
            case SwordId.Caladbolg:
            {
                if (inherited) return Fmt("CALADBOLG", ("Extra", CaladbolgBehavior.InheritedMaxTargets - 1));
                var ulster = player?.Creature?.CombatState != null && player.Creature.HasPower<CaladbolgUlsterHeroPower>();
                return ulster ? Fmt("CALADBOLG_ALL") : Fmt("CALADBOLG", ("Extra", CaladbolgBehavior.MaxTargets - 1));
            }
            default:
                return "";
        }
    }

    /// <summary>
    /// <see cref="Effect"/> as it would be right after switching to <paramref name="sword"/> (sword-card hover tip): the
    /// same text, except for Kusanagi while another sword is current — it would inherit THAT sword (the one current now).
    /// </summary>
    public static string EffectIfSwitched(SwordId sword, Player? player, int level)
    {
        if (sword == SwordId.Kusanagi && player != null && SwordCombat.CurrentSword(player) is { } cur &&
            cur != SwordId.Kusanagi && SwordRegistry.Get(cur).CanBeInherited)
        {
            var inner = Effect(cur, player, SwordCombat.LevelOf(player, cur), inherited: true);
            return Fmt("KUSANAGI") + "\n" + Fmt("KUSANAGI_FROM", ("Sword", SwordRegistry.DisplayName(cur)), ("Text", inner));
        }

        return Effect(sword, player, level);
    }

    private static string KusanagiEffect(Player? player)
    {
        var text = Fmt("KUSANAGI");
        if (player == null || SwordCombat.Get(player) == null) return text;
        var inherited = SwordRegistry.Get(SwordId.Kusanagi)
            .GetInheritedSword(SwordCombat.ContextFor(player, SwordId.Kusanagi));
        if (inherited is not { } from || !SwordRegistry.Get(from).CanBeInherited) return text;
        var inner = Effect(from, player, SwordCombat.LevelOf(player, from), inherited: true);
        return text + "\n" + Fmt("KUSANAGI_FROM", ("Sword", SwordRegistry.DisplayName(from)), ("Text", inner));
    }

    /// <summary>
    /// A few words of the current-sword effect with live numbers, plain text without BBCode (the current-sword tag under
    /// the health bar, user feedback 2026-10-17): e.g. "받는 공격 피해 -3", "빛 3 (턴 종료 +1)". Same numbers as
    /// <see cref="Effect"/>. Texts: MAGICSWORDSMAN-SWORD_FX.SHORT_* (fragment loc_fragments/clarity.&lt;lang&gt;.events.json).
    /// </summary>
    public static string Short(SwordId sword, Player? player, int level, bool inherited = false)
    {
        var half = inherited ? "_HALF" : "";
        string text;
        switch (sword)
        {
            case SwordId.Gram:
                text = Fmt("SHORT_GRAM", ("Damage", Scale(level, inherited)));
                break;
            case SwordId.Ganjiang:
                text = Fmt("SHORT_GANJIANG", ("Damage", Scale(GanjiangBehavior.DamageBonus(level), inherited)));
                break;
            case SwordId.Moye:
                text = Fmt("SHORT_MOYE", ("Block", Scale(MoyeBehavior.BlockBonus(level), inherited)));
                break;
            case SwordId.Kusanagi:
            {
                text = Fmt("SHORT_KUSANAGI");
                if (inherited || player == null || SwordCombat.Get(player) == null) break;
                var from = SwordRegistry.Get(SwordId.Kusanagi)
                    .GetInheritedSword(SwordCombat.ContextFor(player, SwordId.Kusanagi));
                if (from is not { } f || !SwordRegistry.Get(f).CanBeInherited) break;
                text = Fmt("SHORT_KUSANAGI_FROM", ("Sword", SwordRegistry.DisplayName(f)),
                    ("Text", Short(f, player, SwordCombat.LevelOf(player, f), inherited: true)));
                break;
            }
            case SwordId.Tyrfing:
                text = Fmt("SHORT_TYRFING" + half);
                break;
            case SwordId.Dainsleif:
                text = Fmt("SHORT_DAINSLEIF" + half);
                break;
            case SwordId.Durandal:
                text = Fmt("SHORT_DURANDAL", ("Reduction", DurandalReduction(player, level, inherited)));
                break;
            case SwordId.Skofnung:
                text = Fmt("SHORT_SKOFNUNG" + half);
                break;
            case SwordId.Onimaru:
            {
                var kind = player != null && SwordCombat.Get(player) != null ? OnimaruAttack.GetKind(player) : OnimaruKind.Iai;
                var full = OnimaruAttack.CurrentBonus(player!, kind, level, assumeCurrent: true);
                var bonus = inherited ? Math.Max(1, full / 2) : full;
                text = Fmt(kind == OnimaruKind.Goei ? "SHORT_ONIMARU_BLOCK" : "SHORT_ONIMARU", ("Bonus", bonus));
                break;
            }
            case SwordId.ClaiomhSolais:
            {
                var light = player?.Creature?.CombatState != null ? SolaisLight.Get(player) : 0;
                text = Fmt("SHORT_CLAIOMHSOLAIS", ("Light", light), ("Charge", SolaisLight.ChargePerTurn));
                break;
            }
            case SwordId.Caladbolg:
            {
                if (inherited)
                {
                    text = Fmt("SHORT_CALADBOLG", ("Targets", CaladbolgBehavior.InheritedMaxTargets));
                    break;
                }

                var ulster = player?.Creature?.CombatState != null && player.Creature.HasPower<CaladbolgUlsterHeroPower>();
                text = ulster ? Fmt("SHORT_CALADBOLG_ALL") : Fmt("SHORT_CALADBOLG", ("Targets", CaladbolgBehavior.MaxTargets));
                break;
            }
            default:
                text = "";
                break;
        }

        return Plain(text);
    }

    /// <summary>The sword's cost (대가), with live numbers. Empty when the sword has none.</summary>
    public static string Cost(SwordId sword, Player? player, int level)
    {
        var tomb = player?.GetRelic<Mangeomchong>();
        switch (sword)
        {
            case SwordId.Gram:
                return tomb != null && tomb.GetRunCounter(GramBehavior.NibelungCounter) > 0
                    ? Fmt("COST_NONE")
                    : Fmt("COST_GRAM", ("Level", SwordRegistry.MaxLevel));
            case SwordId.Ganjiang:
            case SwordId.Moye:
                return Fmt("COST_GANJIANG", ("Slots", SwordRegistry.GroupSlotCost(SwordId.Ganjiang)));
            case SwordId.Kusanagi:
                return Fmt("COST_KUSANAGI", ("Loss", KusanagiBehavior.CombatEndHpLossFor(level)),
                    ("Max", KusanagiBehavior.MaxSwitchInsPerCombat));
            case SwordId.Tyrfing:
            {
                var given = tomb?.GetRunCounter(TyrfingCurses.RunCounterKey) ?? 0;
                if (given >= TyrfingCurses.MaxCurses) return Fmt("COST_NONE");
                return level < TyrfingCurses.CurseStartLevel
                    ? Fmt("COST_TYRFING_LATER", ("Level", TyrfingCurses.CurseStartLevel))
                    : Fmt("COST_TYRFING", ("Left", TyrfingCurses.MaxCurses - given));
            }
            case SwordId.Dainsleif:
                return Fmt("COST_DAINSLEIF");
            case SwordId.Durandal:
                return Fmt("COST_DURANDAL");
            case SwordId.Skofnung:
                return Fmt("COST_SKOFNUNG");
            case SwordId.Onimaru:
                return Fmt("COST_ONIMARU", ("Chance", OnimaruAttack.RandomKindChancePercent));
            case SwordId.ClaiomhSolais:
                return Fmt("COST_CLAIOMHSOLAIS");
            case SwordId.Caladbolg:
            {
                var ulster = player?.Creature?.CombatState != null && player.Creature.HasPower<CaladbolgUlsterHeroPower>();
                int Pct(int lv) => ulster ? CaladbolgBehavior.PenaltyPercent(lv) / 2 : CaladbolgBehavior.PenaltyPercent(lv);
                var full = Pct(level);
                return Fmt("COST_CALADBOLG", ("Penalty", full)) + CommonNote(full, Pct(0), "COMMON_NOTE_PCT");
            }
            default:
                return "";
        }
    }

    /// <summary>Effect, then "대가: …" on its own line (current-sword power, sword token cards).</summary>
    public static string Full(SwordId sword, Player? player, int level, bool withPartner = false)
    {
        var effect = Effect(sword, player, level, withPartner: withPartner);
        var cost = Cost(sword, player, level);
        return cost.Length == 0 ? effect : effect + "\n" + Fmt("COST_LINE", ("Cost", cost));
    }

    /// <summary>Text without BBCode tags (plain Godot Labels, e.g. the "new sword" popup).</summary>
    public static string Plain(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\[/?[a-z_]+(=[^\]]*)?\]", "");

    /// <summary>The first line of <see cref="Effect"/> (one-line buttons).</summary>
    public static string FirstLine(string text)
    {
        var i = text.IndexOf('\n');
        return i < 0 ? text : text[..i];
    }

    /// <summary>Onimaru's "일반 전투: 25% 확률로 다른 자세" line, or "" in elite/boss fights (no wild stance there).</summary>
    public static string OnimaruWildLine(Player? player)
    {
        if (player != null && OnimaruAttack.IsEliteOrBoss(player)) return "";
        return "\n" + Fmt("ONIMARU_WILD", ("Chance", OnimaruAttack.RandomKindChancePercent));
    }
}
