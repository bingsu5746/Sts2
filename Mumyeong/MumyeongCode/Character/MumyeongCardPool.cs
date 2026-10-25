using BaseLib.Abstracts;
using Mumyeong.MumyeongCode.Extensions;
using Godot;

namespace Mumyeong.MumyeongCode.Character;

public class MumyeongCardPool : CustomCardPoolModel
{
    public override string Title => MumyeongCharacter.CharacterId; // pool id, not a display name
    
    public override string BigEnergyIconPath => "charui/big_energy.png".ImagePath();
    public override string TextEnergyIconPath => "charui/text_energy.png".ImagePath();


    // Card back tint (shader HSV, 1 = unchanged): the base card back for now.
    public override float H => 1f;
    public override float S => 1f;
    public override float V => 1f;

    // Colour of the small card icons.
    public override Color DeckEntryCardColor => new("ffffff");
    
    public override bool IsColorless => false;
}