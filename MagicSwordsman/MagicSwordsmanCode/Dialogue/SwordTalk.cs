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
using MegaCrit.Sts2.Core.Nodes.Events;
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
            // Onimaru is already out at combat start, so it is the one most likely to speak up.
            var owned = relic.OwnedSwords;
            var sword = owned.Contains(SwordId.Onimaru) && Rand.NextDouble() < 0.5
                ? SwordId.Onimaru
                : owned[Rand.Next(owned.Count)];
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
                if (sword == null && player.GetRelic<Mangeomchong>() is { } relic && relic.OwnedSwords.Count > 0)
                    sword = relic.OwnedSwords[Rand.Next(relic.OwnedSwords.Count)];
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
                var seconds = Math.Max(2.4, CharCount(text) * 0.13);
                if (!Show(player, text, who, seconds)) return;
                await Wait(seconds - 0.2);
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordTalk] banter {key}: {e.Message}");
        }
        finally
        {
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

    /// <summary>Shows one bubble (combat, rest site, shop or ordinary event). Returns false when there is no place to show it.</summary>
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

        // shop: above Ensifer's merchant figure (the local player's visual is always first)
        var merchant = NMerchantRoom.Instance;
        if (merchant != null && GodotObject.IsInstanceValid(merchant) && merchant.IsVisibleInTree() &&
            merchant.PlayerVisuals.Count > 0 && GodotObject.IsInstanceValid(merchant.PlayerVisuals[0]))
        {
            // magic_swordsman_merchant.tscn: a 256x400 sprite centred 200 px above the node origin
            var figure = merchant.PlayerVisuals[0].GlobalPosition;
            var anchor = sword != null ? figure + new Vector2(-70f, -430f) : figure + new Vector2(70f, -330f);
            var side = anchor.X > merchant.GetViewportRect().Size.X * 0.5f ? DialogueSide.Right : DialogueSide.Left;
            var color = sword is { } s3 ? ColorOf(s3) : player.Character.SpeechBubbleColor;
            var bubble = NSpeechBubbleVfx.Create(text, side, anchor, seconds, color);
            if (bubble == null) return false;
            merchant.AddChildSafely(bubble);
            return true;
        }

        // ordinary event: over the lower part of the event portrait (Ensifer has no figure there)
        var eventRoom = NEventRoom.Instance;
        var layout = eventRoom?.Layout;
        if (eventRoom != null && layout != null && layout is not NAncientEventLayout &&
            GodotObject.IsInstanceValid(layout))
        {
            var portrait = layout.GetNodeOrNull<Control>("%Portrait");
            Vector2 anchor;
            if (portrait != null && portrait.IsVisibleInTree())
            {
                var box = portrait.GetGlobalRect();
                anchor = sword != null
                    ? box.Position + new Vector2(box.Size.X * 0.22f, box.Size.Y * 0.62f)
                    : box.Position + new Vector2(box.Size.X * 0.32f, box.Size.Y * 0.84f);
            }
            else
            {
                var view = eventRoom.GetViewportRect().Size;
                anchor = sword != null
                    ? new Vector2(view.X * 0.14f, view.Y * 0.6f)
                    : new Vector2(view.X * 0.2f, view.Y * 0.74f);
            }

            var color = sword is { } s4 ? ColorOf(s4) : player.Character.SpeechBubbleColor;
            var bubble = NSpeechBubbleVfx.Create(text, DialogueSide.Left, anchor, seconds, color);
            if (bubble == null) return false;
            eventRoom.AddChildSafely(bubble);
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
