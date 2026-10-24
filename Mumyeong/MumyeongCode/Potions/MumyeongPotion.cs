using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using Mumyeong.MumyeongCode.Character;
using Mumyeong.MumyeongCode.Extensions;

namespace Mumyeong.MumyeongCode.Potions;

[Pool(typeof(MumyeongPotionPool))]
public abstract class MumyeongPotion : CustomPotionModel
{
	public override string? CustomPackedImagePath =>
		$"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".PotionImagePath();
	public override string? CustomPackedOutlinePath =>
		$"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".PotionOutlineImagePath();
}