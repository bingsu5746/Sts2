using Godot;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Visuals;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Rooms;

namespace MagicSwordsman.MagicSwordsmanCode.Dialogue;

/// <summary>
/// Short exchanges between a sword and Ensifer, shown as the game's speech bubbles (NSpeechBubbleVfx, the same node
/// TalkCmd / WhisperingEarring use). Presentation only: nothing here changes game state except
/// <see cref="SwordAffinity"/> gains, which are applied on every client; the bubbles themselves are shown only for the
/// local player and pick lines with System.Random (never a game Rng).
///
/// Lines (events table): MAGICSWORDSMAN-SWORD_TALK.&lt;SWORD&gt;.&lt;SITUATION&gt;.T&lt;tier&gt;.&lt;n&gt;.A|C|B
///   A = the sword (required), C = its partner blade (Ganjiang &lt;-&gt; Moye, optional), B = Ensifer (optional).
///   Shown in the order A, C, B. Missing tier -> the nearest lower tier, then higher.
/// Rate limits: one exchange at a time, at least <see cref="CooldownSeconds"/> between exchanges, at most
/// <see cref="MaxPerCombat"/> during a combat (victory not counted), plus a chance per moment.
/// </summary>
public static class SwordTalk
{
    public enum Situation
    {
        Summon,
        CombatStart,
        Victory,
        Rest,
        LowHp,
        Upgrade,
    }

    private const double CooldownSeconds = 25.0;
    private const int MaxPerCombat = 2;
    private const int RecentMemory = 16;
    private const float LowHpFraction = 0.3f;

    private static readonly Random Rand = new();

    private sealed class TalkState
    {
        public ulong LastTalkMs;
        public bool Busy;
        public readonly Queue<string> Recent = new();
        public readonly HashSet<SwordId> SpokenThisSession = new();

        // per combat (reset by OnCombatStart / snapshot)
        public int TalksThisCombat;
        public bool LowHpDone;
        public List<SwordId> LastCombatSwordsOut = [];
        public SwordId? LastCombatCurrent;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Player, TalkState> States = new();

    private static TalkState StateOf(Player player) => States.GetValue(player, _ => new TalkState());

    // ------------------------------------------------------------------ hooks (called from Mangeomchong / SwordCombat)

    /// <summary>Turn 1 of a combat, after emerging swords came out.</summary>
    public static void OnCombatStart(Player player)
    {
        try
        {
            var st = StateOf(player);
            st.TalksThisCombat = 0;
            st.LowHpDone = false;
            var relic = player.GetRelic<Mangeomchong>();
            if (relic == null || relic.OwnedSwords.Count == 0) return;
            // Onimaru is already out at combat start, so it is the one most likely to speak up.
            var owned = relic.OwnedSwords;
            var sword = owned.Contains(SwordId.Onimaru) && Rand.NextDouble() < 0.5
                ? SwordId.Onimaru
                : owned[Rand.Next(owned.Count)];
            TryTalk(player, sword, Situation.CombatStart, 0.2, delaySeconds: 0.8);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] combat start: {e.Message}");
        }
    }

    /// <summary>A sword was summoned out of Mangeomchong (first time in this combat).</summary>
    public static void OnSummoned(Player player, SwordId sword)
    {
        try
        {
            var st = StateOf(player);
            var chance = st.SpokenThisSession.Contains(sword) ? 0.3 : 0.6;
            TryTalk(player, sword, Situation.Summon, chance, delaySeconds: 0.5);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] summon: {e.Message}");
        }
    }

    /// <summary>After damage to the owner: once per combat when HP falls to 30% or below.</summary>
    public static void OnOwnerDamaged(Player player)
    {
        try
        {
            var st = StateOf(player);
            var creature = player.Creature;
            if (st.LowHpDone || creature.IsDead || creature.MaxHp <= 0) return;
            if (creature.CurrentHp > creature.MaxHp * LowHpFraction) return;
            st.LowHpDone = true;
            var combat = SwordCombat.Get(player);
            SwordId? sword = combat?.Current;
            if (sword == null && combat != null && combat.Present.Count > 0)
                sword = combat.Present.ElementAt(Rand.Next(combat.Present.Count));
            if (sword == null) return;
            TryTalk(player, sword.Value, Situation.LowHp, 0.75, delaySeconds: 0.4);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] low hp: {e.Message}");
        }
    }

    /// <summary>Mangeomchong.AfterCombatEnd, before the combat sword state is cleared.</summary>
    public static void SnapshotCombat(Player player)
    {
        try
        {
            var st = StateOf(player);
            var combat = SwordCombat.Get(player);
            st.LastCombatSwordsOut = combat == null ? [] : combat.Present.Union(combat.Summoned).Distinct().ToList();
            st.LastCombatCurrent = combat?.Current;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] snapshot: {e.Message}");
        }
    }

    /// <summary>Combat won: affinity for the swords that were out, then maybe a line from one of them.</summary>
    public static void OnVictory(Player player, CombatRoom room)
    {
        try
        {
            var st = StateOf(player);
            var swordsOut = st.LastCombatSwordsOut;
            st.LastCombatSwordsOut = [];
            var tiersBefore = swordsOut.ToDictionary(s => s, s => SwordAffinity.Tier(player, s));
            var gained = SwordAffinity.OnVictory(player, swordsOut, room);
            if (gained.Count == 0 || player.Creature.IsDead) return;

            // a sword that just reached a new tier always gets the chance to say so
            var risen = gained.Where(s => SwordAffinity.Tier(player, s) > tiersBefore[s]).ToList();
            SwordId sword;
            double chance;
            if (risen.Count > 0)
            {
                sword = risen[Rand.Next(risen.Count)];
                chance = 1.0;
            }
            else
            {
                sword = st.LastCombatCurrent is { } cur && gained.Contains(cur) && Rand.NextDouble() < 0.6
                    ? cur
                    : gained[Rand.Next(gained.Count)];
                chance = 0.35;
            }

            st.TalksThisCombat = 0; // the victory line does not count against the combat cap
            TryTalk(player, sword, Situation.Victory, chance, delaySeconds: 0.6, ignoreCooldown: risen.Count > 0);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] victory: {e.Message}");
        }
    }

    /// <summary>Any room entered: rest sites get a quiet line by the fire.</summary>
    public static void OnRoomEntered(Player player, AbstractRoom room)
    {
        try
        {
            if (room is not RestSiteRoom) return;
            var relic = player.GetRelic<Mangeomchong>();
            if (relic == null || relic.OwnedSwords.Count == 0) return;
            var owned = relic.OwnedSwords;
            // the closest sword is a little more likely to speak
            var sword = owned.OrderByDescending(s => SwordAffinity.Points(player, s) + Rand.Next(30)).First();
            TryTalk(player, sword, Situation.Rest, 0.5, delaySeconds: 1.6);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] rest: {e.Message}");
        }
    }

    /// <summary>Mangeomchong.SetLevel raised a sword's (pair's) level.</summary>
    public static void OnUpgraded(Player player, SwordId sword)
    {
        try
        {
            SwordAffinity.OnUpgraded(player, sword);
            var group = SwordRegistry.WithPartners(sword);
            var speaker = group[Rand.Next(group.Count)];
            TryTalk(player, speaker, Situation.Upgrade, 0.7, delaySeconds: 2.0, ignoreCooldown: true);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] upgrade: {e.Message}");
        }
    }

    // ------------------------------------------------------------------ selection

    private static void TryTalk(Player player, SwordId sword, Situation situation, double chance, double delaySeconds,
        bool ignoreCooldown = false)
    {
        if (!LocalContext.IsMe(player)) return;
        var st = StateOf(player);
        if (st.Busy) return;
        var inCombat = situation is Situation.Summon or Situation.CombatStart or Situation.LowHp;
        if (inCombat && st.TalksThisCombat >= MaxPerCombat) return;
        var now = Time.GetTicksMsec();
        if (!ignoreCooldown && st.LastTalkMs != 0 && now - st.LastTalkMs < CooldownSeconds * 1000) return;
        if (Rand.NextDouble() >= chance) return;

        var key = PickLine(player, sword, situation, st);
        if (key == null) return;

        st.Busy = true;
        st.LastTalkMs = now;
        if (inCombat) st.TalksThisCombat++;
        st.SpokenThisSession.Add(sword);
        st.Recent.Enqueue(key);
        while (st.Recent.Count > RecentMemory) st.Recent.Dequeue();
        TaskHelper.RunSafely(Play(player, sword, key, situation, delaySeconds, st));
    }

    private static string SwordKey(SwordId sword) => sword.ToString().ToUpperInvariant();

    /// <summary>Base key of a random unused variant (…&lt;n&gt;), or null when the sword has no line for this moment.</summary>
    private static string? PickLine(Player player, SwordId sword, Situation situation, TalkState st)
    {
        var tier = SwordAffinity.Tier(player, sword);
        var order = Enumerable.Range(0, tier + 1).Reverse()
            .Concat(Enumerable.Range(tier + 1, SwordAffinity.TierCount - tier - 1));
        foreach (var t in order)
        {
            var baseKey = $"{SwordAffinity.LocPrefix}.{SwordKey(sword)}.{situation.ToString().ToUpperInvariant()}.T{t}";
            var variants = new List<string>();
            for (var n = 0; n < 12; n++)
            {
                var k = $"{baseKey}.{n}";
                if (LocString.Exists(SwordAffinity.LocTable, k + ".A")) variants.Add(k);
                else break;
            }

            if (variants.Count == 0) continue;
            var fresh = variants.Where(v => !st.Recent.Contains(v)).ToList();
            var pool = fresh.Count > 0 ? fresh : variants;
            return pool[Rand.Next(pool.Count)];
        }

        return null;
    }

    // ------------------------------------------------------------------ display

    private static async Task Play(Player player, SwordId sword, string key, Situation situation, double delay,
        TalkState st)
    {
        try
        {
            await Wait(delay);
            var lines = new List<(string Text, SwordId? Speaker)>();
            AddLine(lines, key + ".A", sword, sword);
            foreach (var partner in SwordRegistry.GetDefinition(sword).Partners.Take(1))
                AddLine(lines, key + ".C", partner, partner);
            AddLine(lines, key + ".B", null, null);

            foreach (var (text, speaker) in lines)
            {
                var seconds = Math.Max(2.4, CharCount(text) * 0.13);
                if (!Show(player, text, speaker, seconds)) return;
                await Wait(seconds - 0.2);
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] play {key}: {e.Message}");
        }
        finally
        {
            st.Busy = false;
            st.LastTalkMs = Time.GetTicksMsec();
        }
    }

    private static void AddLine(List<(string, SwordId?)> lines, string key, SwordId? nameOf, SwordId? speaker)
    {
        if (!LocString.Exists(SwordAffinity.LocTable, key)) return;
        var text = new LocString(SwordAffinity.LocTable, key).GetFormattedText();
        if (string.IsNullOrWhiteSpace(text)) return;
        if (nameOf is { } s) text = $"[color=#{NameColor(s)}]{SwordRegistry.DisplayName(s)}[/color]  {text}";
        lines.Add((text, speaker));
    }

    /// <summary>Shows one bubble. Returns false when the place to show it is gone (room left, owner dead).</summary>
    private static bool Show(Player player, string text, SwordId? sword, double seconds)
    {
        var creature = player.Creature;

        // combat: Ensifer speaks from his creature, a sword from its floating blade (or from above his head)
        var combatNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (combatNode != null)
        {
            if (creature.IsDead) return false;
            NSpeechBubbleVfx? bubble;
            if (sword is { } s)
            {
                var anchor = SwordVisuals.SpeechAnchor(player, s);
                if (anchor == null) return false;
                bubble = NSpeechBubbleVfx.Create(text, DialogueSide.Left, anchor.Value, seconds, ColorOf(s));
            }
            else
            {
                bubble = NSpeechBubbleVfx.Create(text, creature, seconds, player.Character.SpeechBubbleColor);
            }

            if (bubble == null) return false;
            creature.GetVfxContainer()?.AddChildSafely(bubble);
            return true;
        }

        // rest site: above the character sitting by the fire
        var rest = NRestSiteRoom.Instance;
        var character = rest?.GetCharacterForPlayer(player);
        if (rest != null && character != null && GodotObject.IsInstanceValid(character))
        {
            var box = character.Hitbox;
            var top = box.GlobalPosition + new Vector2(box.Size.X * 0.5f, box.Size.Y * 0.1f);
            var side = top.X > rest.GetViewportRect().Size.X * 0.5f ? DialogueSide.Right : DialogueSide.Left;
            var dir = side == DialogueSide.Left ? 1f : -1f;
            var anchor = sword != null
                ? top + new Vector2(-30f * dir, -40f)
                : top + new Vector2(box.Size.X * 0.35f * dir, box.Size.Y * 0.25f);
            var color = sword is { } s2 ? ColorOf(s2) : player.Character.SpeechBubbleColor;
            var bubble = NSpeechBubbleVfx.Create(text, side, anchor, seconds, color);
            if (bubble == null) return false;
            rest.AddChildSafely(bubble);
            return true;
        }

        return false;
    }

    private static async Task Wait(double seconds)
    {
        if (seconds <= 0) return;
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }

    private static int CharCount(string bbcode) =>
        System.Text.RegularExpressions.Regex.Replace(bbcode, "\\[/?[^\\]]+\\]", "").Count(c => !char.IsWhiteSpace(c));

    private static VfxColor ColorOf(SwordId sword) => sword switch
    {
        SwordId.Gram => VfxColor.Orange,
        SwordId.Ganjiang => VfxColor.Red,
        SwordId.Moye => VfxColor.Blue,
        SwordId.Kusanagi => VfxColor.Green,
        SwordId.Tyrfing => VfxColor.Gold,
        SwordId.Dainsleif => VfxColor.Red,
        SwordId.Durandal => VfxColor.Gold,
        SwordId.Skofnung => VfxColor.DarkGray,
        SwordId.Onimaru => VfxColor.Black,
        SwordId.ClaiomhSolais => VfxColor.Cyan,
        SwordId.Caladbolg => VfxColor.Purple,
        _ => VfxColor.Purple,
    };

    private static string NameColor(SwordId sword) => sword switch
    {
        SwordId.Gram => "ffb070",
        SwordId.Ganjiang => "ff8a7a",
        SwordId.Moye => "8ac8ff",
        SwordId.Kusanagi => "9be39b",
        SwordId.Tyrfing => "ffd66b",
        SwordId.Dainsleif => "ff6b6b",
        SwordId.Durandal => "fff0a0",
        SwordId.Skofnung => "c0c0c8",
        SwordId.Onimaru => "d0b0ff",
        SwordId.ClaiomhSolais => "a0ffff",
        SwordId.Caladbolg => "d8a0ff",
        _ => "ffffff",
    };
}
