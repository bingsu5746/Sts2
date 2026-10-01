using BaseLib.Abstracts;
using Cheongunmun.CheongunmunCode.Extensions;
using Godot;

namespace Cheongunmun.CheongunmunCode.Character;

public class CheongunmunPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => CheongunmunCharacter.Color;
    

    public override string BigEnergyIconPath => "charui/big_energy.png".ImagePath();
    public override string TextEnergyIconPath => "charui/text_energy.png".ImagePath();
}