using System.Reflection;
using Godot;
using HarmonyLib;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Modding;

namespace MagicSwordsman.MagicSwordsmanCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "MagicSwordsman"; // Used for resource file paths (res://MagicSwordsman/...)
    public const string ResPath = $"res://{ModId}";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Registers every SwordBehavior subclass in this assembly (see SwordRegistry.AutoRegister).
        SwordRegistry.AutoRegister(assembly);

        Harmony harmony = new(ModId);
        harmony.PatchAll(assembly);
    }
}
