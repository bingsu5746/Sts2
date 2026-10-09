using BaseLib.Abstracts;
using BaseLib.Utils.NodeFactories;
using Godot;
using MagicSwordsman.MagicSwordsmanCode.Cards.Basic;
using MagicSwordsman.MagicSwordsmanCode.Extensions;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Character;

/// <summary>
/// 마검사 (Magic Swordsman). Spec: docs/magic-swordsman-spec.md §1, §6.
/// HP 68, gold 99 / energy 3 come from the character base defaults.
/// Starting deck (2026-10-09): Strike x4, Defend x4, 검 바꾸기 — no sword card. The run's random first sword adds its
/// own 2 starter cards (Mangeomchong.GrantStartingSword) and the run-start pick 2 more (13 cards after the pick).
/// </summary>
public class MagicSwordsmanCharacter : PlaceholderCharacterModel
{
    public const string CharacterId = "MagicSwordsman";

    public static readonly Color Color = new("6f7fb8");

    public override Color NameColor => Color;
    public override CharacterGender Gender => CharacterGender.Masculine;
    public override int StartingHp => 68;

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<SwordsmanStrike>(),
        ModelDb.Card<SwordsmanStrike>(),
        ModelDb.Card<SwordsmanStrike>(),
        ModelDb.Card<SwordsmanStrike>(),
        ModelDb.Card<SwordsmanDefend>(),
        ModelDb.Card<SwordsmanDefend>(),
        ModelDb.Card<SwordsmanDefend>(),
        ModelDb.Card<SwordsmanDefend>(),
        ModelDb.Card<SwordSwap>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<Mangeomchong>()
    ];

    public override CardPoolModel CardPool => ModelDb.CardPool<MagicSwordsmanCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<MagicSwordsmanRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<MagicSwordsmanPotionPool>();

    public override Control CustomIcon
    {
        get
        {
            var icon = NodeFactory<Control>.CreateFromResource(CustomIconTexturePath);
            icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            return icon;
        }
    }

    // Character body scenes (MagicSwordsman/scenes). BaseLib converts these plain scenes into the game's node types
    // (RegisterSceneForConversion) and plays their AnimationPlayer clips by name (idle / Attack / Cast / Hit / Dead)
    // since there is no Spine rig. If a scene is missing from the .pck, fall back to the placeholder (Ironclad) visuals.
    public override string CustomVisualPath => SceneOrPlaceholder("magic_swordsman_combat.tscn", base.CustomVisualPath);
    public override string CustomRestSiteAnimPath =>
        SceneOrPlaceholder("magic_swordsman_rest_site.tscn", base.CustomRestSiteAnimPath);
    public override string CustomMerchantAnimPath =>
        SceneOrPlaceholder("magic_swordsman_merchant.tscn", base.CustomMerchantAnimPath);

    // Character select background (user request 2026-10-08: no more Ironclad picture).
    public override string CustomCharacterSelectBg =>
        SceneOrPlaceholder("magic_swordsman_char_select_bg.tscn", base.CustomCharacterSelectBg);

    // Top-panel icon outline: the placeholder points at Ironclad's outline; use our own (a plain circle).
    public override string? CustomIconOutlineTexturePath => "character_icon_outline.png".CharacterUiPath();

    private static string SceneOrPlaceholder(string file, string? fallback)
    {
        var path = $"{MainFile.ResPath}/scenes/{file}";
        return ResourceLoader.Exists(path) || fallback == null ? path : fallback;
    }

    public override string CustomIconTexturePath => "character_icon_char_name.png".CharacterUiPath();
    public override string CustomCharacterSelectIconPath => "char_select_char_name.png".CharacterUiPath();
    public override string CustomCharacterSelectLockedIconPath => "char_select_char_name_locked.png".CharacterUiPath();
    public override string CustomMapMarkerPath => "map_marker_char_name.png".CharacterUiPath();
}
