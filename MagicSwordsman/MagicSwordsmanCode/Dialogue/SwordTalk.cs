using Godot;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.RestSite;
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
///
/// Moments (2026-10-09 "더 다양하게"): besides the original six, a sword speaks when it becomes the current sword
/// (already out), at elite / boss combat start, after a boss kill, after a near-flawless win (Ensifer gloats), when it
/// gives Ensifer a curse, when it leaves the tomb (farewell) or comes back to it, at a shop or an ordinary event, and
/// when the player has done nothing for a while in combat. Two specific swords out together may banter once per
/// combat (<see cref="SwordBanter"/>). Chances are the constants below.
///
/// Who may speak (2026-10-10, see <see cref="MaySpeak"/>): in combat only a sword that is out; outside combat only a
/// sword in Mangeomchong. Where (see Show): Ensifer from his head (combat: the rig's Head sprite; rest site / shop:
/// his face on that scene, through the node's global transform), a sword from its floating blade in combat, and
/// outside combat from a briefly shown sprite of that sword beside him (in ordinary events, where Ensifer has no
/// figure, only the swords speak, from beside the event picture).
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
        Switch,
        EliteStart,
        BossStart,
        BossVictory,
        Flawless,
        Cursed,
        Released,
        Reacquired,
        Shop,
        Event,
        Idle,
    }

    // chance per moment (after cooldown / caps)
    private const double CombatStartChance = 0.2;
    private const double EliteStartChance = 0.5;
    private const double BossStartChance = 0.8;
    private const double SwitchChance = 0.12;
    private const double BossVictoryChance = 0.9;
    private const double FlawlessShare = 0.5; // of the ordinary victory line, when HP >= FlawlessHpFraction
    private const float FlawlessHpFraction = 0.9f;
    private const double CursedChance = 0.7;
    private const double ReleasedChance = 0.85;
    private const double ReacquiredChance = 0.85;
    private const double ShopChance = 0.35;
    private const double EventChance = 0.2;
    private const double IdleChance = 0.7;
    private const double IdleSeconds = 40.0;

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
        public bool BanterDone;
        public bool IdleDone;
        public int CombatGeneration;
        public bool CombatOver;
        public SwordId? JustSummoned;
        public ulong JustSummonedMs;
        public List<SwordId> LastCombatSwordsOut = [];
        public SwordId? LastCombatCurrent;
        /// <summary>Sword sprites shown outside combat for the exchange in progress (see GhostAnchor).</summary>
        public readonly Dictionary<SwordId, Node2D> Ghosts = new();
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
            st.BanterDone = false;
            st.IdleDone = false;
            st.CombatOver = false;
            st.CombatGeneration++;
            var relic = player.GetRelic<Mangeomchong>();
            if (relic == null || relic.OwnedSwords.Count == 0) return;
            if (LocalContext.IsMe(player)) TaskHelper.RunSafely(IdleWatch(player, st, st.CombatGeneration));
            // Only a sword that is out can speak in combat (user 2026-10-10 "검이 소환 안 되어 있으면 대화 못 해야지"):
            // at combat start that is whatever emerged on its own (Onimaru); with nothing out, nobody speaks.
            var present = SwordCombat.Get(player)?.Present.ToList() ?? [];
            if (present.Count == 0) return;
            var sword = present[Rand.Next(present.Count)];
            var (situation, chance) = player.RunState.CurrentRoom?.RoomType switch
            {
                RoomType.Boss => (Situation.BossStart, BossStartChance),
                RoomType.Elite => (Situation.EliteStart, EliteStartChance),
                _ => (Situation.CombatStart, CombatStartChance),
            };
            TryTalk(player, sword, situation, chance, delaySeconds: 0.8);
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
            st.JustSummoned = sword;
            st.JustSummonedMs = Time.GetTicksMsec();
            if (TryBanter(player, sword, st)) return;
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
            st.CombatOver = true;
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
            var situation = Situation.Victory;
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
                var creature = player.Creature;
                if (room.RoomType != RoomType.Boss && creature.MaxHp > 0 &&
                    creature.CurrentHp >= creature.MaxHp * FlawlessHpFraction && Rand.NextDouble() < FlawlessShare)
                    situation = Situation.Flawless;
            }

            // a boss kill always gets its own line (a tier-up line waits for an ordinary fight)
            if (room.RoomType == RoomType.Boss)
            {
                situation = Situation.BossVictory;
                chance = BossVictoryChance;
            }

            st.TalksThisCombat = 0; // the victory line does not count against the combat cap
            TryTalk(player, sword, situation, chance, delaySeconds: 0.6,
                ignoreCooldown: risen.Count > 0 || situation == Situation.BossVictory);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] victory: {e.Message}");
        }
    }

    /// <summary>Any room entered: rest sites get a quiet line by the fire; shops and ordinary events a remark.</summary>
    public static void OnRoomEntered(Player player, AbstractRoom room)
    {
        try
        {
            var (situation, chance, delay) = room switch
            {
                RestSiteRoom => (Situation.Rest, 0.5, 1.6),
                MerchantRoom => (Situation.Shop, ShopChance, 1.8),
                EventRoom { CanonicalEvent: not AncientEventModel } => (Situation.Event, EventChance, 2.2),
                _ => (Situation.Rest, 0.0, 0.0),
            };
            if (chance <= 0) return;
            var relic = player.GetRelic<Mangeomchong>();
            if (relic == null || relic.OwnedSwords.Count == 0) return;
            var owned = relic.OwnedSwords;
            // the closest sword is a little more likely to speak
            var sword = owned.OrderByDescending(s => SwordAffinity.Points(player, s) + Rand.Next(30)).First();
            TryTalk(player, sword, situation, chance, delaySeconds: delay);
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

    /// <summary>SwordCombat.SwitchTo: <paramref name="sword"/> became the current sword.</summary>
    public static void OnSwitched(Player player, SwordId? old, SwordId sword)
    {
        try
        {
            if (old == sword) return;
            var st = StateOf(player);
            // a sword summoned by this very switch already had its summon moment
            if (st.JustSummoned == sword && Time.GetTicksMsec() - st.JustSummonedMs < 1500) return;
            TryTalk(player, sword, Situation.Switch, SwitchChance, delaySeconds: 0.4);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] switch: {e.Message}");
        }
    }

    /// <summary>Mangeomchong.AddCurse: a sword's own curse went into the deck.</summary>
    public static void OnCursed(Player player, CardModel curse)
    {
        try
        {
            if (curse is not Curses.SwordCurseCard sc) return;
            TryTalk(player, sc.Sword, Situation.Cursed, CursedChance, delaySeconds: 1.4, ignoreCooldown: true);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] cursed: {e.Message}");
        }
    }

    /// <summary>Mangeomchong.LoseSword: the sword (pair) left the tomb.</summary>
    public static void OnReleased(Player player, SwordId sword)
    {
        try
        {
            var group = SwordRegistry.WithPartners(sword);
            TryTalk(player, group[Rand.Next(group.Count)], Situation.Released, ReleasedChance, delaySeconds: 1.0,
                ignoreCooldown: true);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] released: {e.Message}");
        }
    }

    /// <summary>Mangeomchong.AcquireSword: a sword that had been in the tomb earlier this run came back.</summary>
    public static void OnReacquired(Player player, SwordId sword)
    {
        try
        {
            var group = SwordRegistry.WithPartners(sword);
            TryTalk(player, group[Rand.Next(group.Count)], Situation.Reacquired, ReacquiredChance, delaySeconds: 1.2,
                ignoreCooldown: true);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] reacquired: {e.Message}");
        }
    }

    // ------------------------------------------------------------------ idle (local, single player only)

    /// <summary>
    /// Polls once a second during the player's turn; when nothing at all changed (hand, piles, energy, HP, block,
    /// turn) for <see cref="IdleSeconds"/>, one sword nudges Ensifer. Once per combat. Stops when the combat ends.
    /// </summary>
    private static async Task IdleWatch(Player player, TalkState st, int generation)
    {
        try
        {
            if (player.RunState.Players.Count > 1) return; // waiting for teammates is not idling
            var tree = (SceneTree)Engine.GetMainLoop();
            long lastSignature = 0;
            var still = 0.0;
            while (st.CombatGeneration == generation && !st.CombatOver && !st.IdleDone)
            {
                await Wait(1.0);
                if (st.CombatGeneration != generation || st.CombatOver) return;
                var pcs = player.PlayerCombatState;
                var cm = CombatManager.Instance;
                if (pcs == null || player.Creature.IsDead || !cm.IsInProgress || cm.IsOverOrEnding) return;
                if (tree.Paused || pcs.Phase != PlayerTurnPhase.Play || st.Busy)
                {
                    still = 0;
                    continue;
                }

                var signature = IdleSignature(player, pcs);
                if (signature != lastSignature)
                {
                    lastSignature = signature;
                    still = 0;
                    continue;
                }

                still += 1.0;
                if (still < IdleSeconds) continue;

                st.IdleDone = true;
                var combat = SwordCombat.Get(player);
                SwordId? sword = combat?.Current;
                if (sword == null && combat != null && combat.Present.Count > 0)
                    sword = combat.Present.ElementAt(Rand.Next(combat.Present.Count));
                if (sword != null)
                    TryTalk(player, sword.Value, Situation.Idle, IdleChance, delaySeconds: 0.0, ignoreCombatCap: true);
                return;
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] idle: {e.Message}");
        }
    }

    private static long IdleSignature(Player player, PlayerCombatState pcs)
    {
        unchecked
        {
            long h = pcs.TurnNumber;
            h = h * 31 + pcs.Hand.Cards.Count;
            h = h * 31 + pcs.DrawPile.Cards.Count;
            h = h * 31 + pcs.DiscardPile.Cards.Count;
            h = h * 31 + pcs.ExhaustPile.Cards.Count;
            h = h * 31 + pcs.Energy;
            h = h * 31 + player.Creature.CurrentHp;
            h = h * 31 + player.Creature.Block;
            foreach (var card in pcs.Hand.Cards) h = h * 31 + card.GetHashCode();
            return h;
        }
    }

    // ------------------------------------------------------------------ banter (two swords out together)

    /// <summary>
    /// <paramref name="arrived"/> just came out: if a banter partner is already out, maybe play their exchange
    /// (once per combat). Returns true when a banter was started (the summon line is then skipped).
    /// </summary>
    private static bool TryBanter(Player player, SwordId arrived, TalkState st)
    {
        if (!LocalContext.IsMe(player) || st.BanterDone || st.Busy) return false;
        var combat = SwordCombat.Get(player);
        if (combat == null) return false;
        var now = Time.GetTicksMsec();
        if (st.LastTalkMs != 0 && now - st.LastTalkMs < CooldownSeconds * 1000) return false;

        var candidates = SwordBanter.PairsFor(arrived, combat.Present.ToList());
        if (candidates.Count == 0) return false;
        if (Rand.NextDouble() >= SwordBanter.Chance) return false;

        var pair = candidates[Rand.Next(candidates.Count)];
        var variants = SwordBanter.Variants(pair);
        if (variants.Count == 0) return false;
        var fresh = variants.Where(v => !st.Recent.Contains(v)).ToList();
        var pool = fresh.Count > 0 ? fresh : variants;
        var key = pool[Rand.Next(pool.Count)];

        st.BanterDone = true;
        st.Busy = true;
        st.LastTalkMs = now;
        st.Recent.Enqueue(key);
        while (st.Recent.Count > RecentMemory) st.Recent.Dequeue();
        TaskHelper.RunSafely(PlayBanter(player, key, st));
        return true;
    }

    private static async Task PlayBanter(Player player, string key, TalkState st)
    {
        try
        {
            await Wait(0.6);
            foreach (var (locKey, speaker) in SwordBanter.Lines(key))
            {
                var lines = new List<(string Text, SwordId? Speaker)>();
                AddLine(lines, locKey, speaker, speaker);
                if (lines.Count == 0) continue;
                var (text, who) = lines[0];
                // a sword that went back into the tomb mid-exchange cannot finish it
                if (who is { } w && SwordCombat.Get(player) is { } combat && !combat.Present.Contains(w)) return;
                var seconds = Math.Max(2.4, CharCount(text) * 0.13);
                if (!Show(player, text, who, seconds, st)) return;
                await Wait(seconds - 0.2);
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] banter {key}: {e.Message}");
        }
        finally
        {
            DismissGhosts(st);
            st.Busy = false;
            st.LastTalkMs = Time.GetTicksMsec();
        }
    }

    // ------------------------------------------------------------------ selection

    private static void TryTalk(Player player, SwordId sword, Situation situation, double chance, double delaySeconds,
        bool ignoreCooldown = false, bool ignoreCombatCap = false)
    {
        if (!LocalContext.IsMe(player)) return;
        var st = StateOf(player);
        if (st.Busy) return;
        if (!MaySpeak(player, sword, situation)) return;
        var inCombat = situation is Situation.Summon or Situation.CombatStart or Situation.LowHp or Situation.Switch
            or Situation.EliteStart or Situation.BossStart;
        if (inCombat && !ignoreCombatCap && st.TalksThisCombat >= MaxPerCombat) return;
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

    /// <summary>
    /// Who may speak right now. In combat (from Mangeomchong.BeforeCombatStart until the combat state is cleared after
    /// the combat ends) only a sword that is currently out — summoned this combat or emerged on its own, and not sent
    /// back — may speak. Outside combat only a sword in Mangeomchong may speak, except the farewell line of a sword that
    /// is leaving it right now (<see cref="Situation.Released"/>).
    /// </summary>
    private static bool MaySpeak(Player player, SwordId sword, Situation situation)
    {
        var combat = SwordCombat.Get(player);
        if (combat != null) return combat.Present.Contains(sword);
        if (situation == Situation.Released) return true;
        return player.GetRelic<Mangeomchong>()?.OwnedSwords.Contains(sword) ?? false;
    }

    internal static string SwordKey(SwordId sword) => sword.ToString().ToUpperInvariant();

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
            for (var n = 0; n < 16; n++)
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
                if (MaySpeak(player, partner, situation))
                    AddLine(lines, key + ".C", partner, partner);
            // ordinary events have no figure of Ensifer to speak from: only the swords talk there
            if (EnsiferHasFigure(player)) AddLine(lines, key + ".B", null, null);

            foreach (var (text, speaker) in lines)
            {
                // a sword that went back into the tomb mid-exchange cannot finish it
                if (speaker is { } s && SwordCombat.Get(player) is { } combat && !combat.Present.Contains(s)) return;
                var seconds = Math.Max(2.4, CharCount(text) * 0.13);
                if (!Show(player, text, speaker, seconds, st)) return;
                await Wait(seconds - 0.2);
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] play {key}: {e.Message}");
        }
        finally
        {
            DismissGhosts(st);
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

    /// <summary>False in an ordinary event room (no combat, rest site or shop figure of Ensifer on screen).</summary>
    private static bool EnsiferHasFigure(Player player)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(player.Creature) != null) return true;
        if (RestCharacter(player) != null) return true;
        if (MerchantFigure() != null) return true;
        return NEventRoom.Instance == null;
    }

    // Anchors (2026-10-10 "말풍선이 이상한 데로 가 있는 버그"). NSpeechBubbleVfx puts the tail of its bubble at the
    // global (canvas) position it is given; DialogueSide.Left = the speaker is left of the bubble, which opens to the
    // right. The old code let the game place Ensifer's combat bubble: our combat scene has no %TalkPos, so the game's
    // fallback (centre + 75 % of the hitbox width to the right) put the tail ~190 px right of him, on the current
    // sword's slot — Ensifer's lines looked like they came from a sword. Rest site / shop anchors also ignored the
    // node transforms (BgContainer scale, the FlipX of rest-site characters 2 and 4). Every anchor is now a local point
    // on the actual node, mapped through that node's global transform.

    /// <summary>Rest site: right edge of Ensifer's face in magic_swordsman_rest.tscn (ControlRoot space).</summary>
    private static readonly Vector2 RestFace = new(132, -352);
    /// <summary>Rest site: where a talking sword hovers, left of his head (ControlRoot space).</summary>
    private static readonly Vector2 RestSword = new(-70, -300);
    /// <summary>Shop: right edge of Ensifer's face in magic_swordsman_merchant.tscn (node space).</summary>
    private static readonly Vector2 ShopFace = new(26, -356);
    /// <summary>Shop: where a talking sword hovers, left of him (node space).</summary>
    private static readonly Vector2 ShopSword = new(-120, -300);
    /// <summary>Combat, sword not on screen (e.g. after the victory cleared the rig): left of him (creature space).</summary>
    private static readonly Vector2 CombatSword = new(-150, -230);

    private const float GhostHeight = 170f;

    /// <summary>Shows one bubble (combat, rest site, shop or ordinary event). Returns false when there is no place to show it.</summary>
    private static bool Show(Player player, string text, SwordId? sword, double seconds, TalkState st)
    {
        var creature = player.Creature;

        // combat: Ensifer speaks from his head, a sword from its floating blade
        var combatNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (combatNode != null)
        {
            if (creature.IsDead) return false;
            var container = creature.GetVfxContainer() ?? NCombatRoom.Instance?.CombatVfxContainer;
            if (container == null) return false;
            NSpeechBubbleVfx? bubble;
            if (sword is { } s)
            {
                var anchor = FloatingSwordAnchor(player, combatNode, s)
                             ?? GhostAnchor(st, s, combatNode, combatNode.GetGlobalTransform() * CombatSword);
                if (anchor == null) return false;
                bubble = NSpeechBubbleVfx.Create(text, DialogueSide.Left, anchor.Value, seconds, ColorOf(s));
            }
            else
            {
                var head = HeadAnchor(combatNode);
                bubble = head is { } h
                    ? NSpeechBubbleVfx.Create(text, DialogueSide.Left, h, seconds, player.Character.SpeechBubbleColor)
                    : NSpeechBubbleVfx.Create(text, creature, seconds, player.Character.SpeechBubbleColor);
            }

            if (bubble == null) return false;
            container.AddChildSafely(bubble);
            if (sword != null) Visuals.Sfx.Talk();
            return true;
        }

        // rest site: Ensifer by the fire; a talking sword hovers beside his head
        var rest = NRestSiteRoom.Instance;
        var character = RestCharacter(player);
        if (rest != null && character != null)
        {
            var root = character.GetNodeOrNull<CanvasItem>("ControlRoot") ?? character;
            var xf = root.GetGlobalTransform();
            var side = xf.X.X < 0 ? DialogueSide.Right : DialogueSide.Left; // characters 2 and 4 are flipped
            Vector2? anchor = sword is { } s
                ? GhostAnchor(st, s, character, xf * RestSword) ?? xf * RestSword
                : xf * RestFace;
            var color = sword is { } s2 ? ColorOf(s2) : player.Character.SpeechBubbleColor;
            var bubble = NSpeechBubbleVfx.Create(text, side, anchor.Value, seconds, color);
            if (bubble == null) return false;
            rest.AddChildSafely(bubble);
            if (sword != null) Visuals.Sfx.Talk();
            return true;
        }

        // shop: Ensifer's merchant figure (the local player's visual is always first)
        var merchant = NMerchantRoom.Instance;
        var figure = MerchantFigure();
        if (merchant != null && figure != null)
        {
            var xf = figure.GetGlobalTransform();
            Vector2? anchor = sword is { } s
                ? GhostAnchor(st, s, figure, xf * ShopSword) ?? xf * ShopSword
                : xf * ShopFace;
            var color = sword is { } s3 ? ColorOf(s3) : player.Character.SpeechBubbleColor;
            var bubble = NSpeechBubbleVfx.Create(text, DialogueSide.Left, anchor.Value, seconds, color);
            if (bubble == null) return false;
            merchant.AddChildSafely(bubble);
            if (sword != null) Visuals.Sfx.Talk();
            return true;
        }

        // ordinary event: Ensifer has no figure here, so only a sword speaks, hovering at the portrait's lower left
        var eventRoom = NEventRoom.Instance;
        var layout = eventRoom?.Layout;
        if (sword is { } s4 && eventRoom != null && layout != null && layout is not NAncientEventLayout &&
            GodotObject.IsInstanceValid(layout))
        {
            var portrait = layout.GetNodeOrNull<Control>("%Portrait");
            Vector2 at;
            if (portrait != null && portrait.IsVisibleInTree())
            {
                var box = portrait.GetGlobalRect();
                at = box.Position + new Vector2(box.Size.X * 0.12f, box.Size.Y * 0.78f);
            }
            else
            {
                var view = eventRoom.GetViewportRect().Size;
                at = new Vector2(view.X * 0.1f, view.Y * 0.7f);
            }

            var anchor = GhostAnchor(st, s4, eventRoom, at) ?? at;
            var bubble = NSpeechBubbleVfx.Create(text, DialogueSide.Left, anchor, seconds, ColorOf(s4));
            if (bubble == null) return false;
            eventRoom.AddChildSafely(bubble);
            Visuals.Sfx.Talk();
            return true;
        }

        return false;
    }

    private static NRestSiteCharacter? RestCharacter(Player player)
    {
        var character = NRestSiteRoom.Instance?.GetCharacterForPlayer(player);
        return character != null && GodotObject.IsInstanceValid(character) && character.IsVisibleInTree()
            ? character
            : null;
    }

    private static Node2D? MerchantFigure()
    {
        var merchant = NMerchantRoom.Instance;
        if (merchant == null || !GodotObject.IsInstanceValid(merchant) || !merchant.IsVisibleInTree()) return null;
        if (merchant.PlayerVisuals.Count == 0) return null;
        var figure = merchant.PlayerVisuals[0];
        return GodotObject.IsInstanceValid(figure) ? figure : null;
    }

    /// <summary>
    /// Right edge of Ensifer's face, following the rig's "Head" sprite (so it moves with his animations). Null when the
    /// combat scene has no such node (placeholder visuals): the caller then lets the game place the bubble.
    /// </summary>
    private static Vector2? HeadAnchor(NCreature creatureNode)
    {
        try
        {
            var visuals = creatureNode.Visuals;
            if (visuals == null) return null;
            if (visuals.TalkPosition != null) return visuals.TalkPosition.GlobalPosition;
            if (visuals.FindChild("Head", true, false) is not Sprite2D head || head.Texture == null) return null;
            var rect = head.GetRect();
            return head.ToGlobal(new Vector2(rect.End.X + 10f, rect.Position.Y + rect.Size.Y * 0.45f));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Just above the floating blade of <paramref name="sword"/>: SwordVisuals.SpeechAnchor while the combat's sword
    /// state exists (the rig is authoritative then), else the blade node itself if it is still on screen (the victory
    /// line comes after the combat state was cleared). Null when the sword is not on screen.
    /// </summary>
    private static Vector2? FloatingSwordAnchor(Player player, NCreature creatureNode, SwordId sword)
    {
        try
        {
            if (SwordCombat.Get(player) is { } combat && combat.Present.Contains(sword))
                return SwordVisuals.SpeechAnchor(player, sword);
            var rig = creatureNode.GetNodeOrNull<Node2D>("MagicSwordsRig");
            if (rig == null) return null;
            foreach (var child in rig.GetChildren())
            {
                if (child is not Node2D node || node.Name != $"Sword_{sword}" || node.IsQueuedForDeletion() ||
                    !node.IsVisibleInTree() || node.Modulate.A < 0.2f) continue;
                return node.ToGlobal(new Vector2(20f, -95f));
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Outside combat the swords are not on screen, so the one talking briefly appears (images/swords art) at
    /// <paramref name="globalPos"/> under <paramref name="parent"/>, bobbing, until the exchange ends; the bubble's tail
    /// points at its upper blade. Reused for the sword's next line. Null when the art is missing.
    /// </summary>
    private static Vector2? GhostAnchor(TalkState st, SwordId sword, Node parent, Vector2 globalPos)
    {
        try
        {
            if (!st.Ghosts.TryGetValue(sword, out var ghost) || !GodotObject.IsInstanceValid(ghost))
            {
                var path = $"{MainFile.ResPath}/images/swords/{sword.ToString().ToLowerInvariant()}.png";
                if (!ResourceLoader.Exists(path)) return null;
                var tex = GD.Load<Texture2D>(path);
                if (tex == null || tex.GetHeight() <= 0) return null;

                ghost = new Node2D { Name = $"SwordTalkGhost_{sword}" };
                var scale = GhostHeight / tex.GetHeight();
                var sprite = new Sprite2D { Texture = tex, Scale = new Vector2(scale, scale), Name = "Sprite" };
                ghost.AddChild(sprite);
                parent.AddChildSafely(ghost);
                ghost.GlobalPosition = globalPos;
                ghost.Modulate = new Color(1, 1, 1, 0);
                ghost.CreateTween().TweenProperty(ghost, "modulate:a", 1f, 0.3);
                var bob = sprite.CreateTween().SetLoops();
                bob.TweenProperty(sprite, "position:y", -6f, 0.9).SetTrans(Tween.TransitionType.Sine);
                bob.TweenProperty(sprite, "position:y", 6f, 0.9).SetTrans(Tween.TransitionType.Sine);
                st.Ghosts[sword] = ghost;
            }

            return ghost.ToGlobal(new Vector2(18f, -GhostHeight * 0.32f));
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] ghost {sword}: {e.Message}");
            return null;
        }
    }

    private static void DismissGhosts(TalkState st)
    {
        foreach (var ghost in st.Ghosts.Values)
        {
            if (!GodotObject.IsInstanceValid(ghost) || !ghost.IsInsideTree()) continue;
            var t = ghost.CreateTween();
            t.TweenProperty(ghost, "modulate:a", 0f, 0.4);
            t.TweenCallback(Callable.From(ghost.QueueFree));
        }

        st.Ghosts.Clear();
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
