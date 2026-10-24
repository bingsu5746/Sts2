using Mumyeong.MumyeongCode.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Mumyeong.MumyeongCode.Relics;

// The figures' own relics, given at 강림 (spec §5.2). Event rarity: never offered by the game's random relic pulls.
// Numbers are written in the relic text (localization), so they are constants here.

/// <summary>청룡언월도 (관우): at the start of each combat, gain 8 Vigor.</summary>
public sealed class GuanyuGreenDragonRelic : MumyeongRelic
{
    public const int Vigor = 8;
    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != Owner || Owner.PlayerCombatState is not { TurnNumber: 1 }) return;
        Flash();
        await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, Vigor, Owner.Creature, null);
    }
}

/// <summary>오를레앙의 깃발 (잔 다르크): at the start of your turn, gain 2 Faith.</summary>
public sealed class JoanBannerRelic : MumyeongRelic
{
    public const int Faith = 2;
    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || !Owner.Creature.IsAlive) return;
        Flash();
        await PowerCmd.Apply<JoanFaithPower>(choiceContext, Owner.Creature, Faith, Owner.Creature, null);
    }
}

/// <summary>스파르타의 방패 (레오니다스): at the start of each combat, gain 방진 and 3 반격.</summary>
public sealed class LeonidasShieldRelic : MumyeongRelic
{
    public const int Counter = 3;
    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != Owner || Owner.PlayerCombatState is not { TurnNumber: 1 }) return;
        Flash();
        await PowerCmd.Apply<LeonidasPhalanxPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, null);
        await PowerCmd.Apply<LeonidasCounterPower>(choiceContext, Owner.Creature, Counter, Owner.Creature, null);
    }
}

/// <summary>대칸의 인장 (칭기즈 칸): whenever an enemy dies, gain 1 energy and draw 1 card.</summary>
public sealed class GenghisSealRelic : MumyeongRelic
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || !creature.IsEnemy || !Owner.Creature.IsAlive || Owner.PlayerCombatState == null) return;
        Flash();
        await PlayerCmd.GainEnergy(1m, Owner);
        await CardPileCmd.Draw(choiceContext, 1m, Owner);
    }
}

/// <summary>거북선 (이순신): at the end of your turn, deal 5 damage to ALL enemies and gain 5 Block.</summary>
public sealed class YiTurtleShipRelic : MumyeongRelic
{
    public const int Damage = 5;
    public const int Block = 5;
    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner.Creature) || !Owner.Creature.IsAlive) return;
        Flash();
        if (Owner.Creature.CombatState is { } combat && combat.HittableEnemies.Count > 0)
            await CreatureCmd.Damage(choiceContext, combat.HittableEnemies.ToList(), Damage, ValueProp.Unpowered, Owner.Creature, null);
        await CreatureCmd.GainBlock(Owner.Creature, Block, ValueProp.Unpowered, null);
    }
}

/// <summary>황제의 관 (나폴레옹): +1 max energy.</summary>
public sealed class NapoleonCrownRelic : MumyeongRelic
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player != Owner ? amount : amount + 1m;
}
