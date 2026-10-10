using MagicSwordsman.MagicSwordsmanCode.Cards.Onimaru;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 오니마루. Spec §7 [확정]: from the start of combat it comes out ON ITS OWN (SwordDefinition.EmergesAtCombatStart;
/// Mangeomchong calls SwordCombat.Emerge — not a summon, no Mangeomchong bonus, not current) and auto-attacks every
/// turn. The attack kind is chosen by dedicated Power cards (last one wins): 거합 Iai (default) / 난무 Ranbu /
/// 베기 Giri / 퇴마 Taima / 호위 Goei. Attack cards are 【명령】 commands: Onimaru immediately makes an extra auto attack
/// of the current kind.
/// Cost: normal fights — the end-of-turn auto attack has a 25% chance to use a random kind; elites/bosses — always
/// the chosen kind + bonus. Kusanagi inherits "half of the current-sword bonus" (+1).
/// Numbers and timing: content doc §1.2 and 0.4 #13/#14 (end of the player's turn, ValueProp.Unpowered like the
/// game's LightningOrb passive: Strength/Vulnerable/Weak do not apply).
/// </summary>
public sealed class OnimaruBehavior : SwordBehavior
{
    public override SwordId Id => SwordId.Onimaru;

    public override IEnumerable<CardModel> StarterCards =>
        [ModelDb.Card<OnimaruOniSlash>(), ModelDb.Card<OnimaruGoei>()];

    /// <summary>주인 없는 칼 (content doc §3.1).</summary>
    public override CardModel? FailureCurse => ModelDb.Card<MasterlessBlade>();

    /// <summary>Content doc 0.4 #14: one auto attack when the owner's turn ends (unless 주인 없는 칼 is in hand).</summary>
    public override async Task OnPlayerTurnEnd(SwordContext ctx, PlayerChoiceContext choiceContext)
    {
        if (!ctx.IsPresent) return;
        await OnimaruAttack.AutoAttack(ctx.Player, choiceContext, turnEnd: true);
    }

    /// <summary>스스로 움직이는 칼: one more auto attack at the start of each of the owner's turns.</summary>
    public override async Task OnPlayerTurnStart(SwordContext ctx, PlayerChoiceContext choiceContext)
    {
        if (!ctx.IsPresent || !ctx.Creature.HasPower<OnimaruSelfMovingBladePower>()) return;
        await OnimaruAttack.AutoAttack(ctx.Player, choiceContext, turnEnd: false);
    }
}

/// <summary>Onimaru's auto-attack kinds. Values are stored in the per-combat sword counter "kind".</summary>
public enum OnimaruKind
{
    Iai = 0,
    Ranbu = 1,
    Giri = 2,
    Taima = 3,
    Goei = 4,
}

/// <summary>Numbers of one Onimaru auto attack.</summary>
public readonly record struct OnimaruAttackNumbers(int PerHit, int Hits, int Block, int Weak);

/// <summary>
/// Onimaru's auto attack (content doc §1.2), shared by the behavior (turn end / turn start), 【명령】 cards and the
/// kind Power cards. State lives in the per-combat Onimaru counters of <see cref="SwordCombatState"/>.
/// </summary>
public static class OnimaruAttack
{
    public const string KindKey = "kind";
    public const string IaiBonusKey = "iai_bonus";
    public const int RandomKindChancePercent = 25;
    public const int KindCount = 5;

    // ------------------------------------------------------------------ state

    public static OnimaruKind GetKind(Player player) =>
        (OnimaruKind)Math.Clamp(SwordCombat.Get(player)?.GetCounter(SwordId.Onimaru, KindKey) ?? 0, 0, KindCount - 1);

    /// <summary>Accumulated 거합 bonus of this combat (거합 card: +2, +3 from level 3).</summary>
    public static int IaiBonus(Player player) => SwordCombat.Get(player)?.GetCounter(SwordId.Onimaru, IaiBonusKey) ?? 0;

    /// <summary>
    /// Sets the kind (last kind card wins, [확정]) and shows it as a kind power on the player. <paramref name="iaiAdd"/>
    /// adds to the combat-long 거합 bonus.
    /// </summary>
    public static async Task SetKind(Player player, OnimaruKind kind, PlayerChoiceContext choiceContext, int iaiAdd = 0)
    {
        var state = SwordCombat.Get(player);
        if (state == null) return;
        state.SetCounter(SwordId.Onimaru, KindKey, (int)kind);
        if (iaiAdd > 0) state.AddCounter(SwordId.Onimaru, IaiBonusKey, iaiAdd);
        SwordVisuals.Sync(player); // presentation only: the katana takes the stance's pose

        var creature = player.Creature;
        foreach (var other in creature.Powers.OfType<OnimaruKindPower>().Where(p => p.Kind != kind).ToList())
            await PowerCmd.Remove(other);

        switch (kind)
        {
            case OnimaruKind.Iai:
                var iai = creature.GetPower<OnimaruIaiPower>();
                var total = IaiBonus(player);
                if (iai == null)
                {
                    if (total > 0)
                        await PowerCmd.Apply<OnimaruIaiPower>(choiceContext, creature, total, creature, null);
                }
                else if (iaiAdd > 0)
                {
                    await PowerCmd.ModifyAmount(choiceContext, iai, iaiAdd, creature, null);
                }
                break;
            case OnimaruKind.Ranbu:
                if (!creature.HasPower<OnimaruRanbuPower>())
                    await PowerCmd.Apply<OnimaruRanbuPower>(choiceContext, creature, 1, creature, null);
                break;
            case OnimaruKind.Giri:
                if (!creature.HasPower<OnimaruGiriPower>())
                    await PowerCmd.Apply<OnimaruGiriPower>(choiceContext, creature, 1, creature, null);
                break;
            case OnimaruKind.Taima:
                if (!creature.HasPower<OnimaruTaimaPower>())
                    await PowerCmd.Apply<OnimaruTaimaPower>(choiceContext, creature, 1, creature, null);
                break;
            case OnimaruKind.Goei:
                if (!creature.HasPower<OnimaruGoeiPower>())
                    await PowerCmd.Apply<OnimaruGoeiPower>(choiceContext, creature, 1, creature, null);
                break;
        }

        // presentation: while Onimaru is current the stance shows on the current-sword power (one katana icon)
        if (creature.GetPower<CurrentSwordPower>() is { } current)
        {
            current.Refresh();
            current.RefreshIcon();
        }
        OnimaruKindPower.SyncVisibility(player);
    }

    // ------------------------------------------------------------------ numbers (pure: also used for previews)

    /// <summary>Level-0..5 base numbers of a kind (content doc §1.2), without any bonus.</summary>
    public static OnimaruAttackNumbers BaseNumbers(OnimaruKind kind, int level)
    {
        level = Math.Clamp(level, 0, SwordRegistry.MaxLevel);
        var step = (level + 1) / 2; // 0,1,1,2,2,3
        return kind switch
        {
            OnimaruKind.Iai => new OnimaruAttackNumbers(5 + level, 1, 0, 0),
            OnimaruKind.Ranbu => new OnimaruAttackNumbers(2, 2 + step, 0, 0),
            OnimaruKind.Giri => new OnimaruAttackNumbers(2 + step, 1, 0, 0),
            OnimaruKind.Taima => new OnimaruAttackNumbers(3 + step, 1, 0, 1),
            OnimaruKind.Goei => new OnimaruAttackNumbers(0, 0, 4 + level, 0),
            _ => new OnimaruAttackNumbers(0, 0, 0, 0),
        };
    }

    /// <summary>
    /// Bonus while Onimaru is the current sword (per hit / Goei block): 1,1,2,2,3,3 by level; 난무 always +1 per hit.
    /// Kusanagi inheriting Onimaru: half, min 1 (= +1). <paramref name="assumeCurrent"/>: preview of an Onimaru card
    /// in hand (playing it switches to Onimaru first).
    /// </summary>
    public static int CurrentBonus(Player player, OnimaruKind kind, int level, bool assumeCurrent)
    {
        var full = kind == OnimaruKind.Ranbu ? 1 : 1 + Math.Clamp(level, 0, SwordRegistry.MaxLevel) / 2;
        if (assumeCurrent) return full;
        if (!DurandalBehavior.IsSwordEffectActive(player, SwordId.Onimaru, out var inherited)) return 0;
        return inherited ? Math.Max(1, full / 2) : full;
    }

    /// <summary>Elite / boss fight ([확정] "추가 피해"): game pattern BoomingConch / Pantograph (RunState.CurrentRoom).</summary>
    public static bool IsEliteOrBoss(Player player) =>
        player.RunState.CurrentRoom?.RoomType is RoomType.Elite or RoomType.Boss;

    /// <summary>Elite/boss bonus: 거합·퇴마 +3, 난무 +1 per hit, 베기 +2, 호위 block +3.</summary>
    public static int EliteBonus(OnimaruKind kind) => kind switch
    {
        OnimaruKind.Iai or OnimaruKind.Taima => 3,
        OnimaruKind.Ranbu => 1,
        OnimaruKind.Giri => 2,
        OnimaruKind.Goei => 3,
        _ => 0,
    };

    /// <summary>Final numbers of one auto attack of <paramref name="kind"/> for this player right now.</summary>
    public static OnimaruAttackNumbers Compute(Player player, OnimaruKind kind, bool assumeCurrent = false)
    {
        var level = SwordCombat.LevelOf(player, SwordId.Onimaru);
        var n = BaseNumbers(kind, level);
        var bonus = CurrentBonus(player, kind, level, assumeCurrent);
        if (IsEliteOrBoss(player)) bonus += EliteBonus(kind);
        if (kind == OnimaruKind.Iai) bonus += IaiBonus(player);
        return kind == OnimaruKind.Goei ? n with { Block = n.Block + bonus } : n with { PerHit = n.PerHit + bonus };
    }

    // ------------------------------------------------------------------ actions

    /// <summary>
    /// The automatic attack. turnEnd: the regular end-of-turn attack (blocked by 주인 없는 칼 in hand); otherwise the
    /// extra turn-start attack of 스스로 움직이는 칼. In normal fights 25% chance to use a random kind instead.
    /// RNG: the game's Rng.CombatTargets (same stream the game uses for random orb targets), never System.Random.
    /// </summary>
    public static async Task AutoAttack(Player player, PlayerChoiceContext choiceContext, bool turnEnd)
    {
        if (!player.Creature.IsAlive) return;
        if (turnEnd && MasterlessBlade.IsInHand(player)) return;

        var kind = GetKind(player);
        if (!IsEliteOrBoss(player))
        {
            var rng = player.RunState.Rng.CombatTargets;
            if (rng.NextInt(100) < RandomKindChancePercent)
            {
                kind = (OnimaruKind)rng.NextInt(KindCount);
                MainFile.Logger.Info($"[Onimaru] {player.NetId}: wild auto attack -> {kind}");
            }
        }

        await Perform(player, kind, null, choiceContext, null, assumeCurrent: false);
    }

    /// <summary>【명령】: one auto attack of the current kind right now (no 25% roll). Target used by 거합/퇴마.</summary>
    public static Task Command(Player player, Creature? target, PlayerChoiceContext choiceContext, CardModel? source) =>
        Perform(player, GetKind(player), target, choiceContext, source, assumeCurrent: false);

    /// <summary>One attack of an explicit kind (kind cards strike once right after changing the kind).</summary>
    public static async Task Perform(Player player, OnimaruKind kind, Creature? target, PlayerChoiceContext choiceContext,
        CardModel? source, bool assumeCurrent)
    {
        var creature = player.Creature;
        if (!creature.IsAlive || creature.CombatState is not { } combatState) return;
        var n = Compute(player, kind, assumeCurrent);
        var rng = player.RunState.Rng.CombatTargets;

        if (kind == OnimaruKind.Goei)
        {
            if (n.Block > 0) await CreatureCmd.GainBlock(creature, n.Block, ValueProp.Unpowered, null);
            return;
        }

        var enemies = combatState.HittableEnemies.ToList();
        if (enemies.Count == 0 || n.PerHit <= 0) return;

        switch (kind)
        {
            case OnimaruKind.Iai:
            case OnimaruKind.Taima:
            {
                var t = target != null && enemies.Contains(target) ? target : rng.NextItem(enemies);
                if (t == null) return;
                Present(player, kind, [t]);
                VfxCmd.PlayOnCreature(t, "vfx/vfx_attack_slash");
                await CreatureCmd.Damage(choiceContext, t, n.PerHit, ValueProp.Unpowered, creature, source);
                if (n.Weak > 0 && t.IsAlive)
                    await PowerCmd.Apply<WeakPower>(choiceContext, t, n.Weak, creature, source);
                break;
            }
            case OnimaruKind.Ranbu:
                for (var i = 0; i < n.Hits; i++)
                {
                    var alive = combatState.HittableEnemies.ToList();
                    var t = rng.NextItem(alive);
                    if (t == null) break;
                    Present(player, kind, [t]);
                    VfxCmd.PlayOnCreature(t, "vfx/vfx_attack_slash");
                    await CreatureCmd.Damage(choiceContext, t, n.PerHit, ValueProp.Unpowered, creature, source);
                }
                break;
            case OnimaruKind.Giri:
                Present(player, kind, enemies);
                VfxCmd.PlayOnCreatures(enemies, "vfx/vfx_attack_slash");
                await CreatureCmd.Damage(choiceContext, enemies, n.PerHit, ValueProp.Unpowered, creature, source);
                break;
        }
    }

    /// <summary>Presentation only (never throws): the katana flies at the targets and Ensifer gestures the command.</summary>
    private static void Present(Player player, OnimaruKind kind, IReadOnlyList<Creature> targets)
    {
        if (SwordVisuals.OnimaruStrike(player, kind, targets)) MotionDirector.OnOnimaruAttack(player);
    }
}
