using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// Shows the HP a creature will lose to 망령 상흔 (<see cref="SkofnungWoundPower"/>) at the start of its next turn as a
/// violet segment at the end of its health bar, like the game shows Poison (green) and Doom (user request
/// 2026-10-08: "네크로바인더의 절망처럼 확실하게 시각화"). The segment is a copy of the bar's own poison segment, so it
/// has the same shape; it sits left of a poison segment when both exist (poison ticks are shown first).
/// Presentation only.
/// </summary>
[HarmonyPatch(typeof(NHealthBar), "RefreshForeground")]
internal static class SkofnungScarHealthBar
{
    private const string NodeName = "MagicSwordsmanScarForeground";
    private static readonly Color Violet = new(0.62f, 0.38f, 1f);

    private static readonly AccessTools.FieldRef<NHealthBar, Creature> CreatureRef =
        AccessTools.FieldRefAccess<NHealthBar, Creature>("_creature");
    private static readonly AccessTools.FieldRef<NHealthBar, Control> HpRef =
        AccessTools.FieldRefAccess<NHealthBar, Control>("_hpForeground");
    private static readonly AccessTools.FieldRef<NHealthBar, Control> PoisonRef =
        AccessTools.FieldRefAccess<NHealthBar, Control>("_poisonForeground");
    private static readonly System.Reflection.MethodInfo? FgWidth =
        AccessTools.Method(typeof(NHealthBar), "GetFgWidth", [typeof(int)]);
    private static readonly System.Reflection.PropertyInfo? MaxWidth =
        AccessTools.Property(typeof(NHealthBar), "MaxFgWidth");

    [HarmonyPostfix]
    private static void Postfix(NHealthBar __instance)
    {
        try
        {
            var creature = CreatureRef(__instance);
            var poison = PoisonRef(__instance);
            var hpFg = HpRef(__instance);
            if (creature == null || poison == null || hpFg == null || FgWidth == null || MaxWidth == null) return;

            var parent = poison.GetParent();
            if (parent == null) return;
            var scar = parent.GetNodeOrNull<Control>(NodeName);
            var amount = creature.GetPowerAmount<SkofnungWoundPower>();
            if (amount <= 0 || creature.CurrentHp <= 0 || creature.HpDisplay.IsInfinite())
            {
                if (scar != null) scar.Visible = false;
                return;
            }

            if (scar == null)
            {
                scar = (Control)poison.Duplicate();
                scar.Name = NodeName;
                parent.AddChild(scar);
                parent.MoveChild(scar, poison.GetIndex()); // under the poison segment
            }

            scar.SelfModulate = Violet;
            scar.Modulate = Colors.White;
            scar.Visible = true;

            var fgWidth = FgWidth;
            float Width(int hp) => (float)fgWidth.Invoke(__instance, [hp])!;
            var max = (float)MaxWidth.GetValue(__instance)!;
            var poisonDamage = creature.GetPower<PoisonPower>()?.CalculateTotalDamageNextTurn() ?? 0;
            var afterPoison = Math.Max(0, creature.CurrentHp - poisonDamage);
            var afterScar = Math.Max(0, afterPoison - amount);

            var margin = poison is NinePatchRect np ? np.PatchMarginLeft : 0;
            scar.OffsetLeft = afterScar <= 0 ? 0f : Math.Max(0f, Width(afterScar) - margin);
            scar.OffsetRight = Width(afterPoison) - max;
            // the plain HP part now ends where the scar segment starts
            if (afterScar <= 0) hpFg.Visible = false;
            else hpFg.OffsetRight = Math.Min(hpFg.OffsetRight, Width(afterScar) - max);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SkofnungScarHealthBar] {e.Message}");
        }
    }
}
