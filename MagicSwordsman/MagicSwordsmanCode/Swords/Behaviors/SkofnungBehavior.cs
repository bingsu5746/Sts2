using MagicSwordsman.MagicSwordsmanCode.Cards;
using MagicSwordsman.MagicSwordsmanCode.Cards.Skofnung;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 스코프눙. Spec §7 [확정]: current effect = attacks apply "상처" (Wound, <see cref="SkofnungWoundPower"/>): every
/// enemy turn the enemy loses HP equal to its wounds (never decreases); at 12 wounds it bursts and resets to 0.
/// Cards stack wounds.
/// Content doc 0.4 #15: while current, each ATTACK CARD's attack gives 1 wound to every enemy it damaged (independent
/// of the hit count). Kusanagi inherits "1 wound every 2 attack cards" (to the enemies the 2nd one damaged); the burst
/// damage still uses Skofnung's level.
/// Cost: cannot switch to it or play its cards on the first turn of combat (sunlight taboo, 햇빛 금기).
/// FRAMEWORK PARTS: CanBecomeCurrent + CanPlayCard (turn-1 taboo).
/// </summary>
public sealed class SkofnungBehavior : SwordBehavior
{
    public const int WoundsPerAttack = 1;
    private const string InheritedAttackCounter = "inherited_attacks";

    public override SwordId Id => SwordId.Skofnung;

    public override IEnumerable<CardModel> StarterCards =>
        [ModelDb.Card<SkofnungBerserkerSoul>(), ModelDb.Card<SkofnungShadedHilt>()];

    /// <summary>스코프눙 돌 (content doc §3.1).</summary>
    public override CardModel? FailureCurse => ModelDb.Card<SkofnungStone>();

    public override bool CanBecomeCurrent(SwordContext ctx, SwitchReason reason) => ctx.TurnNumber != 1;

    public override bool CanPlayCard(SwordContext ctx, MagicSwordCard card) => ctx.TurnNumber != 1;

    /// <summary>
    /// After an attack of one of the owner's ATTACK cards: 1 wound to every distinct living enemy it damaged.
    /// Inherited: only every 2nd such attack (counter per combat).
    /// TODO(content: Skofnung): counted per AttackCommand, so a card that issues two separate attack commands counts
    /// twice; no game hook gives "per card play" damage results directly.
    /// </summary>
    public override async Task AfterAttack(SwordContext ctx, PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (command.Attacker != ctx.Creature) return;
        if (command.ModelSource is not CardModel { Type: CardType.Attack } card || card.Owner != ctx.Player) return;

        var damaged = DamagedEnemies(ctx.Creature, command);
        if (damaged.Count == 0) return;

        if (ctx.IsInherited && ctx.AddCounter(InheritedAttackCounter, 1) % 2 != 0) return;

        await PowerCmd.Apply<SkofnungWoundPower>(choiceContext, damaged, WoundsPerAttack, ctx.Creature, card);
    }

    /// <summary>Distinct living opponents that took damage from <paramref name="command"/>.</summary>
    public static List<Creature> DamagedEnemies(Creature attacker, AttackCommand command) =>
        command.Results.SelectMany(hit => hit)
            .Where(r => r.TotalDamage > 0 && r.Receiver.IsAlive && r.Receiver.Side != attacker.Side)
            .Select(r => r.Receiver)
            .Distinct()
            .ToList();
}
