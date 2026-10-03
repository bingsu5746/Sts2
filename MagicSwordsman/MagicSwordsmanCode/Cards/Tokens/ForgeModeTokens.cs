using MagicSwordsman.MagicSwordsmanCode.RestSite;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Tokens;

/// <summary>Choice cards for the rest-site "마검 강화" mode pick (안전 강화 / 도박 강화 +2 / +3).</summary>
public abstract class ForgeModeToken : ChoiceTokenCard
{
    public abstract ForgeMode Mode { get; }

    protected ForgeModeToken()
    {
        var odds = SwordForge.OddsFor(Mode);
        WithVar("Gain", SwordForge.GainFor(Mode));
        WithVar("Success", odds.Success);
        WithVar("Failure", odds.Failure);
        WithVar("Shatter", odds.Shatter);
    }
}

public sealed class ForgeSafeToken : ForgeModeToken { public override ForgeMode Mode => ForgeMode.Safe; }
public sealed class ForgeGamble2Token : ForgeModeToken { public override ForgeMode Mode => ForgeMode.GamblePlus2; }
public sealed class ForgeGamble3Token : ForgeModeToken { public override ForgeMode Mode => ForgeMode.GamblePlus3; }
