using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace MagicSwordsman.MagicSwordsmanCode.Curses;

/// <summary>
/// 스코프눙 돌 — Skofnung's forge-failure curse (content doc §3.1). Unplayable.
/// At the end of your turn, if this is in your hand: every enemy loses 3 wounds; enemies without wounds heal 3 HP.
/// Turn-end-in-hand pattern: game curse Decay (HasTurnEndInHandEffect / OnTurnEndInHand).
/// Source line: 「이 검에 난 상처는 짝이 되는 '스코프눙 돌'로 문지르지 않으면 낫지 않는다」.
/// </summary>
public sealed class SkofnungStone : SwordCurseCard
{
    public const int WoundsRemoved = 3;
    public const int HealAmount = 3;

    public override SwordId Sword => SwordId.Skofnung;

    public override bool HasTurnEndInHandEffect => true;

    public SkofnungStone()
    {
        WithVar("Wounds", WoundsRemoved);
        WithVar("Heal", HealAmount);
        WithTip(new BaseLib.Utils.TooltipSource(_ => HoverTipFactory.FromPower<SkofnungWoundPower>()));
    }

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        if (Owner.Creature.CombatState is not { } combatState) return;
        foreach (var enemy in combatState.HittableEnemies.ToList())
        {
            if (!enemy.IsAlive) continue;
            var wound = enemy.GetPower<SkofnungWoundPower>();
            if (wound is { Amount: > 0 })
                await PowerCmd.ModifyAmount(choiceContext, wound, -Math.Min(WoundsRemoved, wound.Amount), null, this);
            else
                await CreatureCmd.Heal(enemy, HealAmount);
        }
    }
}
