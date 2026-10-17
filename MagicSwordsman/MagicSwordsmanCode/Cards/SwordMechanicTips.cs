using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace MagicSwordsman.MagicSwordsmanCode.Cards;

/// <summary>
/// Hover tip "&lt;sword&gt; 효과" for cards whose text refers to a sword's current-sword effect or cost (뒤랑달의 피해
/// 감소, 다인슬레이프의 되돌리기·방어도 불가, 티르빙의 방어도 무시 …) that has no power of its own to point a tip at
/// (user request 2026-10-17: card text must make the mechanic obvious to a new player). The body is the same live text
/// the sword UI uses (<see cref="SwordEffectText.Full"/>: effect + cost at the owner's level; level 0 in the library).
/// </summary>
internal static class SwordMechanicTips
{
    private const string TitleKey = "MAGICSWORDSMAN-MECH_SWORD_EFFECT.title";

    public static TooltipSource Effect(SwordId sword) => new(card =>
    {
        var player = card.IsMutable ? card.Owner : null;
        var title = new LocString("card_keywords", TitleKey);
        title.Add("Sword", SwordRegistry.DisplayName(sword));
        string body;
        try
        {
            body = SwordEffectText.Full(sword, player, SwordEffectText.LevelOf(player, sword));
        }
        catch (Exception)
        {
            body = "";
        }

        return new HoverTip(title, body);
    });
}
