using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using Mumyeong.MumyeongCode.Character;
using Mumyeong.MumyeongCode.Extensions;

namespace Mumyeong.MumyeongCode.Relics;

/// <summary>
/// Base class of the mod's relics: loads images from Mumyeong/images/relics (placeholder when missing).
/// [Pool]: tied to 무명's relic pool.
/// </summary>
[Pool(typeof(MumyeongRelicPool))]
public abstract class MumyeongRelic : CustomRelicModel
{
    public override string PackedIconPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".RelicImagePath();
    protected override string PackedIconOutlinePath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}_outline.png".RelicImagePath();
    protected override string BigIconPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".BigRelicImagePath();
}
