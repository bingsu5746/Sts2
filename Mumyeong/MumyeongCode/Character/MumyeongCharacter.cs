using BaseLib.Abstracts;
using BaseLib.Utils.NodeFactories;
using Mumyeong.MumyeongCode.Cards.Basic;
using Mumyeong.MumyeongCode.Extensions;
using Mumyeong.MumyeongCode.Relics;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace Mumyeong.MumyeongCode.Character;

public class MumyeongCharacter : PlaceholderCharacterModel
{
    public const string CharacterId = "Mumyeong";

    public static readonly Color Color = new("4f86b8");

    public override Color NameColor => Color;
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 70;

    public override IEnumerable<CardModel> StartingDeck => [
        ModelDb.Card<SamjaeGeombeop>(),
        ModelDb.Card<SamjaeGeombeop>(),
        ModelDb.Card<SamjaeGeombeop>(),
        ModelDb.Card<SamjaeGeombeop>(),
        ModelDb.Card<Cheolposam>(),
        ModelDb.Card<Cheolposam>(),
        ModelDb.Card<Cheolposam>(),
        ModelDb.Card<Cheolposam>(),
        ModelDb.Card<UngiJosik>(),
        ModelDb.Card<YukhapGeombeop>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<Danjeon>()
    ];

    public override CardPoolModel CardPool => ModelDb.CardPool<MumyeongCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<MumyeongRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<MumyeongPotionPool>();

    public override Control CustomIcon
    {
        get
        {
            var icon = NodeFactory<Control>.CreateFromResource(CustomIconTexturePath);
            icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            return icon;
        }
    }
    public override string CustomIconTexturePath => "character_icon_char_name.png".CharacterUiPath();
    public override string CustomCharacterSelectIconPath => "char_select_char_name.png".CharacterUiPath();
    public override string CustomCharacterSelectLockedIconPath => "char_select_char_name_locked.png".CharacterUiPath();
    public override string CustomMapMarkerPath => "map_marker_char_name.png".CharacterUiPath();
}
