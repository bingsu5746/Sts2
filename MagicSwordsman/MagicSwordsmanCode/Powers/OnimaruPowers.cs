using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// Visible marker of Onimaru's current auto-attack kind (content doc §2.8: kind powers, the last one played wins).
/// The kind itself is stored in the Onimaru combat counters (OnimaruAttack.SetKind keeps exactly one of these powers).
/// The smart description shows the live numbers of one auto attack (vars AutoDamage / AutoHits / AutoBlock / AutoWeak,
/// refreshed in <see cref="SmartDescriptionLocKey"/> like CurrentSwordPower does).
/// </summary>
public abstract class OnimaruKindPower : MagicSwordsmanPower
{
    public abstract OnimaruKind Kind { get; }

    public override PowerType Type => PowerType.Buff;

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
