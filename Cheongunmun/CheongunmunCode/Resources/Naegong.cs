using BaseLib.Abstracts;
using Cheongunmun.CheongunmunCode.Character;
using Cheongunmun.CheongunmunCode.Extensions;
using Cheongunmun.CheongunmunCode.Relics;

namespace Cheongunmun.CheongunmunCode.Resources;

// Resources don't persist past combat, so the max is stored on the saved Danjeon relic.
public class Naegong() : BasicCustomResource(ResourceId)
{
    public const string ResourceId = "CHEONGUNMUN-NAEGONG";

    public int MaxAmount => Owner?.GetRelic<Danjeon>()?.MaxNaegong ?? 0;

    public override string TexturePath => "charui/text_energy.png".ImagePath();
    public override string? IconPath => TexturePath;

    public override bool ShouldShowDisplay() => Owner?.Character is CheongunmunCharacter;

    public override string GetTextForAmount(int displayAmount) => $"{displayAmount}/{MaxAmount}";

    public void Gain(int amount)
    {
        var target = Math.Min(Amount + amount, MaxAmount);
        if (target > Amount) ModifyAmount(target - Amount);
    }

    public void Fill() => Gain(MaxAmount);
}
