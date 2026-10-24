using BaseLib.Abstracts;
using Mumyeong.MumyeongCode.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Mumyeong.MumyeongCode.Stages;

/// <summary>
/// Rest-site option "필름을 돌린다", added by the 코어 only while a stage-up is ready (spec §5). It uses the rest
/// action and gives the stage reward right at the rest site with the game's own card screens (pattern: 엔시페르's
/// forge option): 동기화 = 1-of-3 figure card, 집중 = upgrade 2 cards, 강림 = the figure's relic.
/// Localization: rest_site_ui.json OPTION_MUMYEONG_FILM_REEL.name / .description
/// </summary>
public sealed class FilmReelRestSiteOption(Player owner) : CustomRestSiteOption(owner)
{
    public const string Id = "MUMYEONG_FILM_REEL";

    public override string OptionId => Id;

    public override bool IsEnabled => Owner.GetRelic<SoulCore>() is { } core && StageRules.NextReadyStage(core) != null;

    public override async Task<bool> OnSelect()
    {
        if (Owner.GetRelic<SoulCore>() is not { } core || StageRules.NextReadyStage(core) is not { } stage) return false;
        await StageRewards.Apply(Owner, core, stage, upgradeInsteadOfRemove: true, new BlockingPlayerChoiceContext());
        return true;
    }
}
