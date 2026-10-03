using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Character;
using MagicSwordsman.MagicSwordsmanCode.Extensions;

namespace MagicSwordsman.MagicSwordsmanCode.Potions;

[Pool(typeof(MagicSwordsmanPotionPool))]
public abstract class MagicSwordsmanPotion : CustomPotionModel
{
	public override string? CustomPackedImagePath =>
		$"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".PotionImagePath();
	public override string? CustomPackedOutlinePath =>
		$"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".PotionOutlineImagePath();
}