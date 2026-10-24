using Godot;
using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Relics;
using Mumyeong.MumyeongCode.Visuals;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Rooms;

namespace Mumyeong.MumyeongCode.Dialogue;

/// <summary>
/// Short exchanges between the contracted figure's spirit and the reaper (user request 2026-10-24: "전투 중에도 영혼이랑
/// 대화할 수 있게"), shown with the game's speech bubbles (NSpeechBubbleVfx, as in MagicSwordsman's SwordTalk).
/// The spirit speaks first (S), the reaper may answer (R). Moments: combat start / elite / boss, low HP, victory,
/// the rest site, and the player clicking the spirit in combat (CHAT).
/// Presentation only; shown for the local player; lines picked with System.Random (never a game Rng).
/// Lines: events table, MUMYEONG-SPIRIT_TALK.&lt;FIG&gt;.&lt;SITUATION&gt;.&lt;n&gt;.S|R (tools/loc_talk.py).
/// </summary>
public static class SpiritTalk
{
    public const string Table = "events";
    public const string Prefix = "MUMYEONG-SPIRIT_TALK";

    private const double CombatStartChance = 0.4;
    private const double EliteChance = 0.7;
    private const double BossChance = 1.0;
    private const double VictoryChance = 0.45;
    private const double BossVictoryChance = 1.0;
    private const double RestChance = 0.8;
    private const float LowHpFraction = 0.3f;
    private const double CooldownSeconds = 20.0;
    private const double ClickCooldownSeconds = 4.0;
    private const int MaxAutoPerCombat = 3;
    private const double CalmWaitSeconds = 8.0;

    private static readonly Random Rand = new();

    private sealed class TalkState
    {
        public ulong LastTalkMs;
        public bool Busy;
        public int TalksThisCombat;
        public bool LowHpDone;
        public readonly Queue<string> Recent = new();
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Player, TalkState> States = new();

    private static TalkState StateOf(Player player) => States.GetValue(player, _ => new TalkState());

    // ------------------------------------------------------------------ moments (called from SoulCore)

    /// <summary>Turn 1 of a combat: the spirit is shown, the click hook is attached, maybe an opening line.</summary>
    public static void OnCombatStart(Player player)
    {
        try
        {
            var st = StateOf(player);
            st.TalksThisCombat = 0;
            st.LowHpDone = false;
            if (!LocalContext.IsMe(player)) return;
            SpiritVisuals.Refresh(player);
            HookClick(player);
            var (situation, chance) = player.RunState.CurrentRoom?.RoomType switch
            {
                RoomType.Boss => ("BOSS", BossChance),
                RoomType.Elite => ("ELITE", EliteChance),
                _ => ("COMBAT_START", CombatStartChance),
            };
            TryTalk(player, situation, chance, 1.4, ignoreCooldown: situation == "BOSS");
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SpiritTalk] combat start: {e.Message}");
        }
    }

    public static void OnOwnerDamaged(Player player)
    {
        try
        {
            var st = StateOf(player);
            var c = player.Creature;
            if (st.LowHpDone || c.IsDead || c.MaxHp <= 0 || c.CurrentHp > c.MaxHp * LowHpFraction) return;
            st.LowHpDone = true;
            TryTalk(player, "LOW_HP", 1.0, 0.5, ignoreCooldown: true);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SpiritTalk] low hp: {e.Message}");
        }
    }

    public static void OnVictory(Player player, CombatRoom room)
    {
        try
        {
            if (player.Creature.IsDead) return;
            var boss = room.RoomType == RoomType.Boss;
            StateOf(player).TalksThisCombat = 0; // the victory line does not count against the combat cap
            TryTalk(player, boss ? "BOSS_VICTORY" : "VICTORY", boss ? BossVictoryChance : VictoryChance, 0.8,
                ignoreCooldown: boss);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SpiritTalk] victory: {e.Message}");
        }
    }

    public static void OnRoomEntered(Player player, AbstractRoom room)
    {
        try
        {
            if (!LocalContext.IsMe(player)) return;
            SpiritVisuals.RefreshSoon(player);
            if (room is RestSiteRoom) TryTalk(player, "REST", RestChance, 2.0, ignoreCooldown: true);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SpiritTalk] room: {e.Message}");
        }
    }

    /// <summary>The player clicked the spirit in combat: it always answers (unless an exchange is running).</summary>
    public static void OnSpiritClicked(Player player)
    {
        var st = StateOf(player);
        if (st.Busy || Time.GetTicksMsec() - st.LastTalkMs < ClickCooldownSeconds * 1000) return;
        SpiritVisuals.Pulse(player, 0.6f);
        TryTalk(player, "CHAT", 1.0, 0.0, ignoreCooldown: true, countsAgainstCap: false);
    }

    // ------------------------------------------------------------------ core

    private static void TryTalk(Player player, string situation, double chance, double delaySeconds,
        bool ignoreCooldown = false, bool countsAgainstCap = true)
    {
        if (!LocalContext.IsMe(player)) return;
        if (player.GetRelic<SoulCore>()?.Figure is not { } figure) return;
        var st = StateOf(player);
        if (st.Busy) return;
        if (!ignoreCooldown && Time.GetTicksMsec() - st.LastTalkMs < CooldownSeconds * 1000) return;
        var inCombat = NCombatRoom.Instance?.GetCreatureNode(player.Creature) != null;
        if (countsAgainstCap && inCombat && situation != "VICTORY" && situation != "BOSS_VICTORY" &&
            st.TalksThisCombat >= MaxAutoPerCombat) return;
        if (Rand.NextDouble() >= chance) return;
        var key = PickLine(figure, situation, st);
        if (key == null) return;

        st.Busy = true;
        if (countsAgainstCap) st.TalksThisCombat++;
        st.Recent.Enqueue(key);
        while (st.Recent.Count > 12) st.Recent.Dequeue();
        TaskHelper.RunSafely(Play(player, figure, key, delaySeconds, st));
    }

    private static string? PickLine(FigureId figure, string situation, TalkState st)
    {
        var baseKey = $"{Prefix}.{FigureRegistry.Key(figure)}.{situation}";
        var variants = new List<string>();
        for (var n = 0; n < 16; n++)
        {
            var k = $"{baseKey}.{n}";
            if (LocString.Exists(Table, k + ".S")) variants.Add(k);
            else break;
        }

        if (variants.Count == 0) return null;
        var fresh = variants.Where(v => !st.Recent.Contains(v)).ToList();
        var pool = fresh.Count > 0 ? fresh : variants;
        return pool[Rand.Next(pool.Count)];
    }

    private static async Task Play(Player player, FigureId figure, string key, double delay, TalkState st)
    {
        try
        {
            await Wait(delay);
            var lines = new List<(string Text, bool Spirit)>();
            if (Text(key + ".S") is { } s)
                lines.Add(($"[color=#{NameColor(figure)}]{FigureRegistry.Name(figure)}[/color]  {s}", true));
            if (Text(key + ".R") is { } r) lines.Add((r, false));

            foreach (var (text, spirit) in lines)
            {
                if (!await WaitForCalm()) return;
                var seconds = Math.Max(2.4, CharCount(text) * 0.12);
                if (spirit) SpiritVisuals.Pulse(player, 0.4f);
                if (!Show(player, text, spirit, seconds)) return;
                await Wait(seconds - 0.2);
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SpiritTalk] play {key}: {e.Message}");
        }
        finally
        {
            st.Busy = false;
            st.LastTalkMs = Time.GetTicksMsec();
        }
    }

    private static string? Text(string key)
    {
        if (!LocString.Exists(Table, key)) return null;
        var text = new LocString(Table, key).GetFormattedText();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    // ------------------------------------------------------------------ display

    // Anchors: local points on the sprites (texture centre = 0,0), see tools/gen_character.py for the art layout.
    /// <summary>Right edge of the reaper's smoke face on reaper.png (560x860).</summary>
    private static readonly Vector2 BodyFace = new(62, -194);
    /// <summary>Right edge of the spirit's head on spirit_*.png (680x1010, standing).</summary>
    private static readonly Vector2 SpiritHead = new(40, -170);
    /// <summary>Right edge of the reaper's smoke face on reaper_rest.png (420x480).</summary>
    private static readonly Vector2 RestFace = new(42, -116);
    /// <summary>Right edge of the spirit's head on spirit_*_rest.png (680x1010, sitting).</summary>
    private static readonly Vector2 RestSpiritHead = new(40, -30);

    private static bool Show(Player player, string text, bool spirit, double seconds)
    {
        var creature = player.Creature;
        var spiritColor = VfxColor.Cyan;

        var combatNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (combatNode != null)
        {
            if (creature.IsDead) return false;
            var container = creature.GetVfxContainer() ?? NCombatRoom.Instance?.CombatVfxContainer;
            if (container == null) return false;
            Sprite2D? sprite = spirit ? SpiritVisuals.CombatSpirit(player) : SpiritVisuals.CombatBody(player);
            NSpeechBubbleVfx? bubble;
            if (sprite != null && sprite.IsVisibleInTree())
                bubble = NSpeechBubbleVfx.Create(text, DialogueSide.Left, sprite.ToGlobal(spirit ? SpiritHead : BodyFace),
                    seconds, spirit ? spiritColor : player.Character.SpeechBubbleColor);
            else if (spirit)
                return false; // no spirit on screen, it cannot speak
            else
                bubble = NSpeechBubbleVfx.Create(text, creature, seconds, player.Character.SpeechBubbleColor);
            if (bubble == null) return false;
            container.AddChildSafely(bubble);
            return true;
        }

        var rest = NRestSiteRoom.Instance;
        var character = SpiritVisuals.RestCharacter(player);
        if (rest != null && character != null)
        {
            var sprite = spirit
                ? character.FindChild("Spirit", true, false) as Sprite2D
                : character.FindChild("Visuals", true, false) as Sprite2D;
            if (sprite == null || !sprite.IsVisibleInTree()) return false;
            var xf = sprite.GetGlobalTransform();
            var side = xf.X.X < 0 ? DialogueSide.Right : DialogueSide.Left; // rest-site characters 2 and 4 are flipped
            var anchor = sprite.ToGlobal(spirit ? RestSpiritHead : RestFace);
            var bubble = NSpeechBubbleVfx.Create(text, side, anchor, seconds,
                spirit ? spiritColor : player.Character.SpeechBubbleColor);
            if (bubble == null) return false;
            rest.AddChildSafely(bubble);
            return true;
        }

        return false;
    }

    /// <summary>Lets the player click the spirit in combat to talk to it (the scene's "SpiritClick" control).</summary>
    private static void HookClick(Player player)
    {
        var visuals = NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.Visuals;
        if (visuals?.FindChild("SpiritClick", true, false) is not Control click) return;
        if (click.HasMeta("mumyeong_hooked")) return;
        click.SetMeta("mumyeong_hooked", true);
        click.TooltipText = "";
        click.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } &&
                SpiritVisuals.CombatSpirit(player) is { Visible: true })
                OnSpiritClicked(player);
        };
    }

    /// <summary>A popup, choice screen or game overlay covers the scene right now.</summary>
    private static bool ScreenBusy()
    {
        try
        {
            if (MegaCrit.Sts2.Core.Nodes.Screens.Overlays.NOverlayStack.Instance is { } overlays && overlays.ScreenCount > 0)
                return true;
            return MegaCrit.Sts2.Core.Nodes.Screens.Capstones.NCapstoneContainer.Instance is { InUse: true };
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<bool> WaitForCalm()
    {
        var waited = 0.0;
        while (ScreenBusy() && waited < CalmWaitSeconds)
        {
            await Wait(0.25);
            waited += 0.25;
        }

        return !ScreenBusy();
    }

    private static async Task Wait(double seconds)
    {
        if (seconds <= 0) return;
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }

    private static int CharCount(string bbcode) =>
        System.Text.RegularExpressions.Regex.Replace(bbcode, "\\[/?[^\\]]+\\]", "").Count(c => !char.IsWhiteSpace(c));

    private static string NameColor(FigureId figure) => figure switch
    {
        FigureId.Guanyu => "7fd6a0",
        FigureId.Joan => "f0dc8a",
        FigureId.Leonidas => "ff9a80",
        FigureId.Genghis => "d8b27a",
        FigureId.YiSunsin => "8fb6ff",
        FigureId.Napoleon => "b9a6ff",
        _ => "ffffff",
    };
}
