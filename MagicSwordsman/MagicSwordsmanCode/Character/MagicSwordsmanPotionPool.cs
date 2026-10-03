using BaseLib.Abstracts;
using MagicSwordsman.MagicSwordsmanCode.Extensions;
using Godot;

namespace MagicSwordsman.MagicSwordsmanCode.Character;

public class MagicSwordsmanPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => MagicSwordsmanCharacter.Color;
    

    public override string BigEnergyIconPath => "charui/big_energy.png".ImagePath();
    public override string TextEnergyIconPath => "charui/text_energy.png".ImagePath();
}