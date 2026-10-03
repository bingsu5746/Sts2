using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Gram;

/// <summary>
/// 파프니르 살해 — Gram, Attack, Rare, cost 2, one enemy, Exhaust. Deal 16 (+1/L) damage, +8 against elites and
/// bosses. If this kills the target, gain 3 Max HP. Kill/Max HP pattern: game card Feed (Fatal check included).
/// "Elite/boss": the fight is in an Elite or Boss room and the target is a primary enemy (not a minion).
/// Lore: with this sword Sigurd killed the dragon Fafnir.
/// </summary>
public sealed class GramFafnirSlayer : GramCard
{
    public GramFafnirSlayer() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(16);
        WithDamagePerLevel(1);
        WithVar("EliteBonus", 8);
        WithVar("MaxHp", 3);
        WithKeywords(CardKeyword.Exhaust);
        WithTip(StaticHoverTip.Fatal);
    }

    public override bool CanBeGeneratedInCombat => false;

    protected override int TargetDamageBonus(Creature? target)
    {
        if (target == null || !target.IsPrimaryEnemy || !IsMutable || Owner == null) return 0;
        var room = Owner.RunState.CurrentRoom;
        if (room == null || (room.RoomType != RoomType.Elite && room.RoomType != RoomType.Boss)) return 0;
        return DynamicVars["EliteBonus"].IntValue;
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;
        var fatal = target.Powers.All((PowerModel p) => p.ShouldOwnerDeathTriggerFatal());
        var attack = await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_giant_horizontal_slash")
            .Execute(choiceContext);
        if (fatal && attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
            await CreatureCmd.GainMaxHp(Owner.Creature, DynamicVars["MaxHp"].IntValue);
    }
}
