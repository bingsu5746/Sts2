using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Onimaru;

// 오니마루 메커니즘 카드 3장 (mech2, 2026-10-17, user request "현재의 검들의 메커니즘을 제대로 반영하는 카드들"):
// protecting the auto attack's tempo (no random stance this turn), a payoff for commanding several times in one turn,
// and a payoff for changing the stance mid-fight. Like every Onimaru card they deal no damage of their own: everything
// goes through OnimaruAttack (content doc §1.2).

/// <summary>
/// 녹을 닦다 — Onimaru, Skill, Common, cost 0, self. This turn, Onimaru's end-of-turn auto attack never turns into a
/// random stance (the 25% roll of normal fights is skipped) and deals 3 (+⌊L/2⌋) more damage per hit (호위: more Block)
/// (<see cref="OnimaruPolishedPower"/>, read by OnimaruAttack.AutoAttack).
/// Lore (Taiheiki, "Onimaru and Onikiri", medium confidence): the spirit of the sword appeared to Hōjō Tokimasa in a
/// dream as an old man, complaining that it could not leave its scabbard because its blade had been soiled and had
/// rusted; once it was cleaned, it cut the oni on its own.
/// </summary>
public sealed class OnimaruPolishRust : OnimaruCard
{
    public OnimaruPolishRust() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithCalculatedVar("Polish", 3, 1, static (card, _) => card is MagicSwordCard m ? m.SwordLevel / 2 : 0);
        WithTip(SwordMechTips.AutoAttack);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<OnimaruPolishedPower>()));
    }

    private int Polish => DynamicVars["PolishBase"].IntValue + SwordLevel / 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<OnimaruPolishedPower>(choiceContext, Owner.Creature, Polish, Owner.Creature, this);
    }
}

/// <summary>
/// 잇따른 호령 — Onimaru, Attack, Uncommon, cost 1, one enemy. 【명령】 that is always a 거합 on the target (the stance
/// does not change): +3 damage for each 【명령】 you already made this turn (max +12). It counts as a 【명령】 itself.
/// Always 거합 so the bonus cannot multiply over 난무's hits or 베기's targets. Counter: OnimaruAttack.CommandsThisTurn
/// (every 【명령】 of any card counts; this one is counted after it reads the number).
/// </summary>
public sealed class OnimaruChainedOrders : OnimaruCard
{
    public const int PerCommand = 3;
    public const int MaxBonus = 12;

    public OnimaruChainedOrders() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithKindValueVar("IaiDamage", OnimaruKind.Iai);
        WithVar("PerCommand", PerCommand);
        WithVar("MaxBonus", MaxBonus);
        WithCalculatedVar("ChainBonus", 0, static (card, _) => card is OnimaruChainedOrders c ? c.ChainBonus : 0);
        WithTip(SwordMechTips.Command);
        WithTip(OnimaruTips.Iai);
    }

    private int ChainBonus => IsMutable && Owner?.PlayerCombatState != null && !LevelPreviewPatches.NeutralCombatInputs
        ? Math.Min(MaxBonus, PerCommand * OnimaruAttack.CommandsThisTurn(Owner))
        : 0;

    protected override bool ShouldGlowGoldInternal => ChainBonus > 0;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var bonus = ChainBonus;
        OnimaruAttack.CountCommand(Owner);
        await OnimaruAttack.Perform(Owner, OnimaruKind.Iai, cardPlay.Target, choiceContext, this, assumeCurrent: false,
            extra: bonus);
    }
}

/// <summary>
/// 바뀌는 자세 — Onimaru, Power, Uncommon, cost 1, self. This combat, whenever Onimaru's stance changes to a different
/// stance, Onimaru acts once more with the new stance right away (<see cref="OnimaruShiftingStancePower"/>).
/// Rewards switching stances in the middle of a fight (each stance card then acts twice; 거합 자세 acts once).
/// </summary>
public sealed class OnimaruShiftingStance : OnimaruCard
{
    public OnimaruShiftingStance() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<OnimaruShiftingStancePower>()));
        WithTip(SwordMechTips.AutoAttack);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<OnimaruShiftingStancePower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }
}
