namespace MagicSwordsman.MagicSwordsmanCode.Cards.Tokens;

/// <summary>
/// "칼집째 놓인 검" — the shop's sword offer (spec §5 [확정] "? 방·상점에서 낮은 확률로 획득", content doc §6.5).
/// It only exists as a merchant card entry: <see cref="Relics.SwordShopOffer"/> puts it in one character-card slot
/// and sets its price; buying it adds it to the deck for a moment, then Mangeomchong's AfterItemPurchased removes it
/// and acquires a random unowned sword. Token pool + hidden from the library (via <see cref="ChoiceTokenCard"/>),
/// not upgradable (so egg relics leave it alone).
/// </summary>
public sealed class SheathedSwordCard : ChoiceTokenCard
{
    /// <summary>Price rolled when the offer was created (180 ±10%, content doc §6.5). Not saved: shops are not restored.</summary>
    public int ShopPrice { get; set; } = SwordShopPrice;

    public const int SwordShopPrice = 180;
}
