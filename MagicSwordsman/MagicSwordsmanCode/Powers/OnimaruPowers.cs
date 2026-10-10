using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// Visible marker of Onimaru's current auto-attack kind (content doc §2.8: kind powers, the last one played wins).
/// The kind itself is stored in the Onimaru combat counters (OnimaruAttack.SetKind keeps exactly one of these powers).
/// The smart description shows the live numbers of one auto attack (vars AutoDamage / AutoHits / AutoBlock / AutoWeak,
/// refreshed in <see cref="SmartDescriptionLocKey"/> like CurrentSwordPower does).
///
/// Hidden while Onimaru is the current sword (user 2026-10-10: two identical katana icons -> one): CurrentSwordPower
/// then shows the stance in its own title, text and icon. Visibility is read by the power bar when a power is added,
/// so <see cref="SyncVisibility"/> adds / removes the icon node when Onimaru becomes / stops being current.
/// Icons: onimaru_&lt;kind&gt;_power.png, the katana with a stance glyph (tools/gen_power_icons.py).
/// </summary>
public abstract class OnimaruKindPower : MagicSwordsmanPower
{
    public abstract OnimaruKind Kind { get; }

    public override PowerType Type => PowerType.Buff;

    protected override bool IsVisibleInternal =>
        !(IsMutable && Owner?.Player is { } p && SwordCombat.CurrentSword(p) == SwordId.Onimaru);

    private static readonly System.Reflection.MethodInfo? ContainerAdd =
        HarmonyLib.AccessTools.Method(typeof(NPowerContainer), "Add");
    private static readonly System.Reflection.MethodInfo? ContainerRemove =
        HarmonyLib.AccessTools.Method(typeof(NPowerContainer), "Remove");
    private static readonly System.Reflection.FieldInfo? ContainerCreature =
        HarmonyLib.AccessTools.Field(typeof(NPowerContainer), "_creature");
    private static readonly System.Reflection.FieldInfo? ContainerNodes =
        HarmonyLib.AccessTools.Field(typeof(NPowerContainer), "_powerNodes");

    /// <summary>
    /// Presentation only: shows / hides the stance power's icon to match <see cref="IsVisibleInternal"/> (called after
    /// a sword switch and after the stance changes). Never throws.
    /// </summary>
    public static void SyncVisibility(Player player)
    {
        try
        {
            var creature = player.Creature;
            var kinds = creature.Powers.OfType<OnimaruKindPower>().ToList();
            if (kinds.Count == 0 || ContainerAdd == null || ContainerRemove == null || ContainerCreature == null ||
                ContainerNodes == null) return;
            var creatureNode = NCombatRoom.Instance?.GetCreatureNode(creature);
            if (creatureNode == null) return;
            var stack = new Stack<Godot.Node>([creatureNode]);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                foreach (var c in n.GetChildren()) stack.Push(c);
                if (n is not NPowerContainer container || !ReferenceEquals(ContainerCreature.GetValue(container), creature))
                    continue;
                if (ContainerNodes.GetValue(container) is not List<NPower> nodes) continue;
                foreach (var power in kinds)
                {
                    var shown = nodes.Any(x => ReferenceEquals(x.Model, power));
                    if (power.IsVisible && !shown) ContainerAdd.Invoke(container, [power]);
                    else if (!power.IsVisible && shown) ContainerRemove.Invoke(container, [power]);
                }
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[OnimaruKindPower] visibility sync failed: {e.Message}");
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("AutoDamage", 0m),
        new DynamicVar("AutoHits", 0m),
        new DynamicVar("AutoBlock", 0m),
        new DynamicVar("AutoWeak", 0m),
    ];

    protected override string SmartDescriptionLocKey
    {
        get
        {
            if (IsMutable && Owner?.Player is { } player)
            {
                var n = OnimaruAttack.Compute(player, Kind);
                DynamicVars["AutoDamage"].BaseValue = n.PerHit;
                DynamicVars["AutoHits"].BaseValue = n.Hits;
                DynamicVars["AutoBlock"].BaseValue = n.Block;
                DynamicVars["AutoWeak"].BaseValue = n.Weak;
            }

            return base.SmartDescriptionLocKey;
        }
    }
}

/// <summary>거합 (Iai) — the default kind. Amount = this combat's accumulated 거합 damage bonus.</summary>
public sealed class OnimaruIaiPower : OnimaruKindPower
{
    public override OnimaruKind Kind => OnimaruKind.Iai;
    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>난무 (Ranbu): random enemies, several hits.</summary>
public sealed class OnimaruRanbuPower : OnimaruKindPower
{
    public override OnimaruKind Kind => OnimaruKind.Ranbu;
    public override PowerStackType StackType => PowerStackType.Single;
}

/// <summary>베기 (Giri): all enemies.</summary>
public sealed class OnimaruGiriPower : OnimaruKindPower
{
    public override OnimaruKind Kind => OnimaruKind.Giri;
    public override PowerStackType StackType => PowerStackType.Single;
}

/// <summary>퇴마 (Taima): one enemy + Weak 1.</summary>
public sealed class OnimaruTaimaPower : OnimaruKindPower
{
    public override OnimaruKind Kind => OnimaruKind.Taima;
    public override PowerStackType StackType => PowerStackType.Single;
}

/// <summary>호위 (Goei): Block for the owner instead of an attack.</summary>
public sealed class OnimaruGoeiPower : OnimaruKindPower
{
    public override OnimaruKind Kind => OnimaruKind.Goei;
    public override PowerStackType StackType => PowerStackType.Single;
}

/// <summary>
/// 스스로 움직이는 칼 (OnimaruSelfMovingBlade): this combat Onimaru also auto-attacks at the start of your turn
/// (OnimaruBehavior.OnPlayerTurnStart checks for this power).
/// </summary>
public sealed class OnimaruSelfMovingBladePower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}
