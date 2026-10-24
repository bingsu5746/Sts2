using BaseLib.Abstracts;
using Mumyeong.MumyeongCode.Extensions;
using Godot;

namespace Mumyeong.MumyeongCode.Character;

public class MumyeongPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => MumyeongCharacter.Color;
    

    public override string BigEnergyIconPath => "charui/big_energy.png".ImagePath();
    public override string TextEnergyIconPath => "charui/text_energy.png".ImagePath();
}