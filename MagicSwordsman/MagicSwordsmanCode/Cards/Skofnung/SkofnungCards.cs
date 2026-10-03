using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Skofnung;

// 스코프눙 — 상처 · 10장 (content doc §2.7). Basic 2 · Common 3 · Uncommon 3 · Rare 2.
// Current-sword effect (1 wound per enemy damaged by an attack card) and the turn-1 ban are in SkofnungBehavior.
// Lore lines (research doc): 「덴마크 전설의 왕 흐롤프 크라키의 검」 「(무덤 도굴) 미드피르드의 스케기」
// 「'북방에서 든 모든 검 중 최고'」 「왕을 지킨 광전사 12명의 혼이 깃들었다」 「햇빛이 자루에 닿아서도 안 된다」.

internal static class SkofnungTips
{
    public static TooltipSource Wound => new(_ => HoverTipFactory.FromPower<SkofnungWoundPower>());
}

/// <summary>광전사의 혼 (Basic): 6 damage (+1/L). Apply 1 extra wound (2 with the current-sword effect).</summary>
public sealed class SkofnungBerserkerSoul : SkofnungCard
{
    public SkofnungBerserkerSoul() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithDamage(6);
        WithDamagePerLevel(1);
        WithVar("Wounds", 1);
        WithTip(SkofnungTips.Wound);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        if (cardPlay.Target is { IsAlive: true } target)
            await PowerCmd.Apply<SkofnungWoundPower>(choiceContext, target, DynamicVars["Wounds"].IntValue,
                Owner.Creature, this);
    }
}

/// <summary>그늘 속 자루 (Basic): 6 Block (+1/L). If any enemy has wounds, draw 1 card.</summary>
public sealed class SkofnungShadedHilt : SkofnungCard
{
    public SkofnungShadedHilt() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
        WithBlock(6);
        WithBlockPerLevel(1);
        WithCards(1);
        WithTip(SkofnungTips.Wound);
    }

    private bool AnyWoundedEnemy =>
        IsMutable && Owner?.Creature.CombatState is { } cs && cs.HittableEnemies.Any(e => WoundsOn(e) > 0);

    protected override bool ShouldGlowGoldInternal => AnyWoundedEnemy;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (AnyWoundedEnemy) await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}

/// <summary>흐롤프 크라키의 검 (Common): 10 damage (+2/L) + the target's wounds.</summary>
public sealed class SkofnungHrolfsBlade : SkofnungCard
{
    public SkofnungHrolfsBlade() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(10);
        WithDamagePerLevel(2);
        WithTip(SkofnungTips.Wound);
    }

    protected override decimal BonusDamage(Creature? target) => WoundsOn(target);

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}

/// <summary>무덤 도굴 (Common): 4 Block (+1/L). Put an Attack card from your discard pile into your hand.</summary>
public sealed class SkofnungBarrowRobbing : SkofnungCard
{
    public SkofnungBarrowRobbing() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(4);
        WithBlockPerLevel(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        var discard = PileType.Discard.GetPile(Owner);
        if (!discard.Cards.Any(IsAttack)) return;
        var picked = (await CardSelectCmd.FromCombatPile(choiceContext, discard, Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1), IsAttack)).FirstOrDefault();
        if (picked != null) await CardPileCmd.Add(picked, PileType.Hand);
    }

    private static bool IsAttack(CardModel card) => card.Type == CardType.Attack;
}

/// <summary>금기를 지키다 (Common): 5 Block (+1/L). Apply 1 wound to ALL enemies.</summary>
public sealed class SkofnungKeepTheTaboo : SkofnungCard
{
    public SkofnungKeepTheTaboo() : base(1, CardType.Skill, CardRarity.Common, TargetType.AllEnemies)
    {
        WithBlock(5);
        WithBlockPerLevel(1);
        WithVar("Wounds", 1);
        WithTip(SkofnungTips.Wound);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (Owner.Creature.CombatState is not { } cs) return;
        var enemies = cs.HittableEnemies.ToList();
        if (enemies.Count > 0)
            await PowerCmd.Apply<SkofnungWoundPower>(choiceContext, enemies, DynamicVars["Wounds"].IntValue,
                Owner.Creature, this);
    }
}

/// <summary>
/// 열두 광전사 (Uncommon): deal 3 (+⌊L/2⌋ per hit) damage to a random enemy 4 times. EVERY hit applies 1 wound to
/// the enemy it damaged (on top of the current-sword effect). Ricochet 3×4.
/// </summary>
public sealed class SkofnungTwelveBerserkers : SkofnungCard
{
    public SkofnungTwelveBerserkers() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.RandomEnemy)
    {
        WithDamage(3);
        WithVar("Hits", 4);
        WithTip(SkofnungTips.Wound);
    }

    protected override decimal BonusDamage(Creature? target) => SwordLevel / 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var command = await CommonActions.CardAttack(this, cardPlay, DynamicVars["Hits"].IntValue)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        // Wounds per hit, applied after the volley (AttackCommand.Results holds one list per hit).
        foreach (var hit in command.Results.ToList())
        {
            foreach (var result in hit)
            {
                if (result.TotalDamage <= 0 || !result.Receiver.IsAlive) continue;
                await PowerCmd.Apply<SkofnungWoundPower>(choiceContext, result.Receiver, 1, Owner.Creature, this);
            }
        }
    }
}

/// <summary>상처 벌리기 (Uncommon): double the target's wounds (bursts at once at 12+). Exhaust. Cost 0 from level 3.</summary>
public sealed class SkofnungOpenWounds : SkofnungCard
{
    public SkofnungOpenWounds() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithTip(SkofnungTips.Wound);
    }

    protected override int? CostAtLevel(int level) => level >= 3 ? 0 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is not { IsAlive: true } target) return;
        var wounds = WoundsOn(target);
        if (wounds <= 0) return;
        // Adding the same amount again doubles it; SkofnungWoundPower bursts by itself when this reaches 12.
        await PowerCmd.Apply<SkofnungWoundPower>(choiceContext, target, wounds, Owner.Creature, this);
    }
}

/// <summary>북방 최고의 검 (Uncommon power): whenever a wound bursts, gain 1 energy and draw 2 cards (3 from level 4).</summary>
public sealed class SkofnungFinestInNorth : SkofnungCard
{
    public SkofnungFinestInNorth() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithCalculatedVar("BurstCards", 2, static (card, _) => card is MagicSwordCard { SwordLevel: >= 4 } ? 1 : 0);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<SkofnungFinestInNorthPower>()));
        WithTip(SkofnungTips.Wound);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SkofnungFinestInNorthPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }
}

/// <summary>
/// 열두 혼의 해방 (Rare): the target loses 3 HP per wound (4 from level 3, ignores Block), then its wounds become 0.
/// </summary>
public sealed class SkofnungTwelveSouls : SkofnungCard
{
    public SkofnungTwelveSouls() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithCalculatedVar("PerWound", 3, static (card, _) => card is MagicSwordCard { SwordLevel: >= 3 } ? 1 : 0);
        WithTip(SkofnungTips.Wound);
    }

    private int PerWound => SwordLevel >= 3 ? 4 : 3;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is not { IsAlive: true } target) return;
        var wounds = WoundsOn(target);
        if (wounds <= 0) return;
        await PowerCmd.Remove(target.GetPower<SkofnungWoundPower>());
        VfxCmd.PlayOnCreature(target, "vfx/vfx_attack_slash");
        await CreatureCmd.Damage(choiceContext, target, wounds * PerWound, ValueProp.Unblockable | ValueProp.Unpowered,
            Owner.Creature, this);
    }
}

/// <summary>
/// 왕을 지킨 열두 혼 (Rare power): at the start of your turn apply 1 wound to ALL enemies (2 at level 5).
/// Cost 1 from level 3.
/// </summary>
public sealed class SkofnungKingsGuard : SkofnungCard
{
    public SkofnungKingsGuard() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithCalculatedVar("TurnWounds", 1, static (card, _) => card is MagicSwordCard { SwordLevel: >= 5 } ? 1 : 0);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<SkofnungKingsGuardPower>()));
        WithTip(SkofnungTips.Wound);
    }

    protected override int? CostAtLevel(int level) => level >= 3 ? 1 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SkofnungKingsGuardPower>(choiceContext, Owner.Creature, SwordLevel >= 5 ? 2 : 1,
            Owner.Creature, this);
    }
}
