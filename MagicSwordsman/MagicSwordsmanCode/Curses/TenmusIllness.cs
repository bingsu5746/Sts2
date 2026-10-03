using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Curses;

/// <summary>
/// 덴무의 병 — Kusanagi's forge-failure curse (content doc §3.1). Unplayable. At the end of your turn, if it is in
/// your hand: receive 1 of Weak / Frail / Vulnerable at random (Doubt pattern, game Rng.CombatCardSelection).
/// Source line: 「688년 덴무 천황의 원인 모를 병사가 이 검 탓이라는 말이 돌아」.
/// </summary>
public sealed class TenmusIllness : SwordCurseCard
{
    public const int DebuffAmount = 1;

    public override SwordId Sword => SwordId.Kusanagi;

    public override bool HasTurnEndInHandEffect => true;

    public TenmusIllness()
    {
        WithVar("Amount", DebuffAmount);
        WithTip(new BaseLib.Utils.TooltipSource(_ => HoverTipFactory.FromPower<WeakPower>()));
        WithTip(new BaseLib.Utils.TooltipSource(_ => HoverTipFactory.FromPower<FrailPower>()));
        WithTip(new BaseLib.Utils.TooltipSource(_ => HoverTipFactory.FromPower<VulnerablePower>()));
    }

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        var creature = Owner.Creature;
        var roll = Owner.RunState.Rng.CombatCardSelection.NextInt(3);
        PowerModel? power;
        bool hadIt;
        switch (roll)
        {
            case 0:
                hadIt = creature.HasPower<WeakPower>();
                power = await PowerCmd.Apply<WeakPower>(choiceContext, creature, DebuffAmount, null, this);
                break;
            case 1:
                hadIt = creature.HasPower<FrailPower>();
                power = await PowerCmd.Apply<FrailPower>(choiceContext, creature, DebuffAmount, null, this);
                break;
            default:
                hadIt = creature.HasPower<VulnerablePower>();
                power = await PowerCmd.Apply<VulnerablePower>(choiceContext, creature, DebuffAmount, null, this);
                break;
        }

        // Same as the game's Doubt: a freshly applied duration debuff must survive its first tick.
        if (power != null && !hadIt) power.SkipNextDurationTick = true;
    }
}
