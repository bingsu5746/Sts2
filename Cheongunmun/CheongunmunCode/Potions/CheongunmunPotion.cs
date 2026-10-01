using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using Cheongunmun.CheongunmunCode.Character;
using Cheongunmun.CheongunmunCode.Extensions;

namespace Cheongunmun.CheongunmunCode.Potions;

[Pool(typeof(CheongunmunPotionPool))]
public abstract class CheongunmunPotion : CustomPotionModel
{
	public override string? CustomPackedImagePath =>
		$"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".PotionImagePath();
	public override string? CustomPackedOutlinePath =>
		$"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".PotionOutlineImagePath();
}