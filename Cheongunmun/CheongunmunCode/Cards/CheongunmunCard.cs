using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using Cheongunmun.CheongunmunCode.Character;
using Cheongunmun.CheongunmunCode.Extensions;
using Cheongunmun.CheongunmunCode.Resources;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace Cheongunmun.CheongunmunCode.Cards;

[Pool(typeof(CheongunmunCardPool))]
public abstract class CheongunmunCard(int cost, CardType type, CardRarity rarity, TargetType target) :
    ConstructedCardModel(cost, type, rarity, target)
{
    //Image size:
    //Normal art: 1000x760 (Using 500x380 should also work, it will simply be scaled.)
    //Full art: 606x852
    public override string CustomPortraitPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".BigCardImagePath();

    //Smaller variant of fullart: 250x350
    //Smaller variant of normalart: 250x190
    public override string PortraitPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();
    public override string BetaPortraitPath => $"beta/{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();

    protected void WithNaegongCost(int amount) => CustomResources<Naegong>.SetCanonicalCost(this, amount);
}
