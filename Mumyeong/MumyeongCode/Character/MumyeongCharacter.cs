using BaseLib.Abstracts;
using BaseLib.Utils.NodeFactories;
using Mumyeong.MumyeongCode.Cards.Shared;
using Mumyeong.MumyeongCode.Extensions;
using Mumyeong.MumyeongCode.Relics;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace Mumyeong.MumyeongCode.Character;

/// <summary>
/// 무명 (spec docs/mumyeong-spec.md §1). HP 70; gold 99 / energy 3 are the character base defaults.
/// Starting deck: Strike x4, Defend x4. The 황천 영사실 adds the contracted figure's 2 starter cards (10 cards).
/// Starting relic: 코어 (SoulCore).
/// </summary>
public class MumyeongCharacter : PlaceholderCharacterModel
{
    public const string CharacterId = "Mumyeong";

    public static readonly Color Color = new("8fa3b8");

    public override Color NameColor => Color;
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 70;

    public override IEnumerable<CardModel> StartingDeck => [
        ModelDb.Card<MumyeongStrike>(),
        ModelDb.Card<MumyeongStrike>(),
        ModelDb.Card<MumyeongStrike>(),
        ModelDb.Card<MumyeongStrike>(),
        ModelDb.Card<MumyeongDefend>(),
        ModelDb.Card<MumyeongDefend>(),
        ModelDb.Card<MumyeongDefend>(),
        ModelDb.Card<MumyeongDefend>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<SoulCore>()
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
