using BaseLib.Abstracts;
using BaseLib.Extensions;
using Mumyeong.MumyeongCode.Extensions;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace Mumyeong.MumyeongCode.Powers;

/// <summary>Base of the mod's powers: icons from Mumyeong/images/powers/&lt;power id&gt;.png (placeholder when missing).</summary>
public abstract class MumyeongPower : CustomPowerModel
{
    public override string CustomPackedIconPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".PowerImagePath();
    public override string CustomBigIconPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".BigPowerImagePath();

    public abstract override PowerType Type { get; }

    public abstract override PowerStackType StackType { get; }
}
