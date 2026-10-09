using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;
using MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;
using MagicSwordsman.MagicSwordsmanCode.Cards.Kusanagi;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MagicSwordsman.MagicSwordsmanCode.Visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Union;

// 【조합】 cards (user request 2026-10-09). 12 cards, every sword used at least twice:
//   Gram 3 · Tyrfing 3 · Durandal 3 · Kusanagi 3 · Dainsleif 2 · Skofnung 2 · Onimaru 2 · Solais 2 · Caladbolg 2 ·
//   Ganjiang/Moye 2.  Rarity: Uncommon 9 · Rare 3.  Numbers grow with the SUM of the two swords' levels (UnionLevel).
// Lore links are noted per card; "thematic" = a link we drew, not a story the two swords share.

/// <summary>
/// 용을 벤 칼 — Gram + Kusanagi, Attack, Rare, 2. 14 damage (+UnionLevel); +8 against elites/bosses (primary enemy).
/// Then remove one of your debuffs (Kusanagi's Rare-strength purify). Gram current -> its +L applies too.
/// Lore (high confidence): Gram killed the dragon Fafnir; Kusanagi came out of the tail of the serpent Yamata no
/// Orochi that Susanoo killed. Two serpent/dragon-slaying swords.
/// </summary>
public sealed class UnionSerpentSlayers : UnionCard
{
    public UnionSerpentSlayers() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(14);
        WithVar("EliteBonus", 8);
    }

    public override SwordId Primary => SwordId.Gram;
    public override SwordId Partner => SwordId.Kusanagi;
    public override UnionMotionKind Motion => UnionMotionKind.Coil;
    public override string BodyClip => "Attack_Overhead";
    public override float Impact => 0.42f;
    protected override int UnionDamageDivisor => 1;

    protected override decimal BonusDamage(Creature? target)
    {
        if (target == null || !target.IsPrimaryEnemy || !IsMutable || Owner == null) return 0m;
        var room = Owner.RunState.CurrentRoom;
        if (room == null || (room.RoomType != RoomType.Elite && room.RoomType != RoomType.Boss)) return 0m;
        return DynamicVars["EliteBonus"].IntValue;
    }

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await UnionAttack(cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        await KusanagiPurify.RemoveOneDebuff(Owner.Creature);
    }
}

/// <summary>
/// 난쟁이의 솜씨 — Tyrfing + Gram, Attack, Uncommon, 1. 4 damage twice (+⌊UnionLevel/3⌋ per hit). Switches to Tyrfing
/// (its attacks ignore Block) but also carries Gram's effect: +Gram level per hit.
/// Lore: Tyrfing was forged by the dwarves Dvalin and Durin (Hervarar saga, high confidence); Gram was re-forged by
/// Regin, a smith who is a dwarf in the Eddic Reginsmál / "dwarf in stature" in Völsunga saga (medium-high).
/// </summary>
public sealed class UnionDwarvenCraft : UnionCard
{
    public UnionDwarvenCraft() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(4);
        WithVar("Hits", 2);
        WithCalculatedVar("GramBonus", 0, 1,
            static (card, _) => card.IsMutable && card.Owner is { } o ? SwordCombat.LevelOf(o, SwordId.Gram) : 0);
    }

    public override SwordId Primary => SwordId.Tyrfing;
    public override SwordId Partner => SwordId.Gram;
    public override UnionMotionKind Motion => UnionMotionKind.Sequence;
    public override string BodyClip => "Attack_Double";
    public override float Impact => 0.32f;
    protected override int UnionDamageDivisor => 3;

    protected override decimal BonusDamage(Creature? target) =>
        IsMutable && Owner != null ? SwordCombat.LevelOf(Owner, SwordId.Gram) : 0;

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await UnionAttack(cardPlay, DynamicVars["Hits"].IntValue).WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}

/// <summary>
/// 칼집에 들기 전에 — Dainsleif + Tyrfing, Attack, Uncommon, 1. 9 damage (+⌊UnionLevel/2⌋) that ignores Block.
/// Kill: gain 1 energy. No kill: lose 2 HP. Dainsleif current: the card returns to the draw pile (cost 1).
/// Lore (high confidence): both swords must take a life whenever they are drawn (Tyrfing: Hervarar saga; Dáinsleif:
/// Skáldskaparmál).
/// </summary>
public sealed class UnionBeforeTheSheath : UnionCard
{
    public const int HpLoss = 2;
    private const ValueProp Unblockable = ValueProp.Move | ValueProp.Unblockable;

    public UnionBeforeTheSheath() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(9);
        WithEnergy(1);
        WithVar("HpLoss", HpLoss);
    }

    public override SwordId Primary => SwordId.Dainsleif;
    public override SwordId Partner => SwordId.Tyrfing;
    public override UnionMotionKind Motion => UnionMotionKind.Cross;
    public override string BodyClip => "Attack_Thrust";
    public override float Impact => 0.32f;
    protected override int UnionDamageDivisor => 2;

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var attack = await UnionAttack(cardPlay, DynamicVars.Damage.BaseValue, Unblockable)
            .WithHitFx("vfx/vfx_dramatic_stab").Execute(choiceContext);
        if (attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
            await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        else
            await CreatureCmd.Damage(choiceContext, Owner.Creature, HpLoss, DamageProps.cardHpLoss, this);
    }
}

/// <summary>
/// 아물지 않는 상처 — Dainsleif + Skofnung, Attack, Uncommon, 1. 4 damage (+⌊UnionLevel/2⌋), then 2 Scars; every play
/// adds 1 Scar to this card for the rest of the combat. Dainsleif current: the card keeps coming back.
/// Lore (high confidence): a wound from Dáinsleif never heals (Skáldskaparmál); a wound from Skofnung heals only
/// when rubbed with the Skofnung stone (Laxdæla saga).
/// </summary>
public sealed class UnionWoundsNeverClose : UnionCard
{
    public const int BaseScars = 2;
    private int _extraScars;

    public UnionWoundsNeverClose() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(4);
        WithCalculatedVar("Wounds", BaseScars, 1, static (card, _) => card is UnionWoundsNeverClose w ? w._extraScars : 0);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<SkofnungWoundPower>()));
    }

    public override SwordId Primary => SwordId.Dainsleif;
    public override SwordId Partner => SwordId.Skofnung;
    public override UnionMotionKind Motion => UnionMotionKind.Pincer;
    public override string BodyClip => "Attack_Flurry";
    public override float Impact => 0.3f;
    protected override int UnionDamageDivisor => 2;

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var scars = BaseScars + _extraScars;
        _extraScars++;
        await UnionAttack(cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        if (cardPlay.Target is { IsAlive: true } target)
            await PowerCmd.Apply<SkofnungWoundPower>(choiceContext, target, scars, Owner.Creature, this);
    }
}

/// <summary>
/// 깃든 혼 — Onimaru + Skofnung, Attack, Uncommon, 2. 【명령】 twice, then 2 (+⌊UnionLevel/3⌋) Scars on the target.
/// Thematic (not a shared legend): both blades carry a will of their own — Onimaru moved by itself to cut an oni
/// (Taiheiki), Skofnung holds the souls of King Hrólfr's twelve berserkers.
/// </summary>
public sealed class UnionEnsouledBlades : UnionCard
{
    public UnionEnsouledBlades() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithVar("Commands", 2);
        WithUnionVar("Wounds", 2, 3);
        var iai = OnimaruAttack.BaseNumbers(OnimaruKind.Iai, 0).PerHit;
        WithCalculatedVar("IaiDamage", iai, 1, (card, _) =>
            card.IsMutable && card.Owner is { } o ? OnimaruAttack.Compute(o, OnimaruKind.Iai, assumeCurrent: true).PerHit - iai : 0);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<SkofnungWoundPower>()));
    }

    public override SwordId Primary => SwordId.Onimaru;
    public override SwordId Partner => SwordId.Skofnung;
    public override UnionMotionKind Motion => UnionMotionKind.Orbit;
    public override string BodyClip => "Attack_Command";
    public override float Impact => 0.4f;

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        PlayMotion(cardPlay.Target);
        await Cmd.Wait(Impact);
        for (var i = 0; i < DynamicVars["Commands"].IntValue; i++)
            await OnimaruAttack.Command(Owner, cardPlay.Target, choiceContext, this);
        if (cardPlay.Target is { IsAlive: true } target)
            await PowerCmd.Apply<SkofnungWoundPower>(choiceContext, target, UnionValue(2, 3), Owner.Creature, this);
    }
}

/// <summary>
/// 요괴 퇴치 — Kusanagi + Onimaru, Attack, Uncommon, 1. Onimaru strikes twice with 퇴마 (damage + Weak 1) whatever its
/// current kind, then reduce one of your debuffs by 1. Kusanagi current: if Onimaru was current right before, Kusanagi
/// inherits Onimaru and the strikes get half of Onimaru's current-sword bonus.
/// Lore (high confidence for each sword): Kusanagi came from the slain serpent Orochi; Onimaru cut down an oni on its
/// own. Both are monster-slaying blades (the pairing itself is ours).
/// </summary>
public sealed class UnionYokaiSlayers : UnionCard
{
    public UnionYokaiSlayers() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithVar("Strikes", 2);
        WithTip(typeof(OnimaruTaimaPower));
    }

    public override SwordId Primary => SwordId.Kusanagi;
    public override SwordId Partner => SwordId.Onimaru;
    public override UnionMotionKind Motion => UnionMotionKind.Ward;
    public override string BodyClip => "Cast_Trace";
    public override float Impact => 0.35f;

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        PlayMotion(cardPlay.Target);
        await Cmd.Wait(Impact);
        for (var i = 0; i < DynamicVars["Strikes"].IntValue; i++)
            await OnimaruAttack.Perform(Owner, OnimaruKind.Taima, cardPlay.Target, choiceContext, this,
                assumeCurrent: false);
        await KusanagiPurify.ReduceOneDebuff(choiceContext, Owner.Creature, this);
    }
}

/// <summary>
/// 산을 가른 칼 — Caladbolg + Durandal, Attack, Rare, 2. Gain 8 (+⌊UnionLevel/2⌋) Block, then deal damage equal to
/// your Block. Caladbolg current: the hit spreads to up to 3 enemies.
/// Lore (high confidence): Fergus cut the tops off three hills with Caladbolg (Táin Bó Cúailnge); Roland's Durandal
/// cleft the Brèche de Roland in the Pyrenees (French legend, later than the Song of Roland).
/// </summary>
public sealed class UnionMountainCleavers : UnionCard
{
    public UnionMountainCleavers() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithBlock(8);
        WithCalculatedDamage(0, 1, static (card, _) => card.Owner?.Creature.Block ?? 0);
        WithTip(StaticHoverTip.Block);
    }

    public override SwordId Primary => SwordId.Caladbolg;
    public override SwordId Partner => SwordId.Durandal;
    public override UnionMotionKind Motion => UnionMotionKind.Drop;
    public override string BodyClip => "Attack_Heavy";
    public override float Impact => 0.45f;
    protected override int UnionBlockDivisor => 2;

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await UnionAttack(cardPlay).WithHitFx("vfx/vfx_attack_blunt").Execute(choiceContext);
    }
}

/// <summary>
/// 시드의 빛 — Caladbolg + Claíomh Solais, Attack, Rare, 2. 【발도】: consume all Light; 6 (+⌊UnionLevel/2⌋) damage +
/// K per Light (K by Solais level, 4..7), ignoring Block. Caladbolg current: up to 3 enemies.
/// Lore (medium): Claíomh Solais belongs to Nuada of the Tuatha Dé Danann, who withdrew into the síde (fairy mounds);
/// Caladbolg is called "the sword of Leite from the fairy mounds". The "light from the mounds" link is ours.
/// </summary>
public sealed class UnionSidheLight : UnionCard
{
    private const ValueProp DrawCutProps = ValueProp.Move | ValueProp.Unblockable;

    public UnionSidheLight() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(6);
        WithCalculatedVar("PerLight", 4, 1, static (card, _) =>
            card.IsMutable && card.Owner is { } o ? SolaisLight.LevelBonus(SwordCombat.LevelOf(o, SwordId.ClaiomhSolais)) : 0);
        WithTip(typeof(SolaisLightPower));
        WithTip(new TooltipSource(_ => HoverTipFactory.FromKeyword(MagicSwordsmanKeywords.DrawCut)));
    }

    public override SwordId Primary => SwordId.Caladbolg;
    public override SwordId Partner => SwordId.ClaiomhSolais;
    public override UnionMotionKind Motion => UnionMotionKind.BeamArc;
    public override string BodyClip => "Attack_Sweep";
    public override float Impact => 0.42f;
    protected override int UnionDamageDivisor => 2;

    private int Light => IsMutable && Owner?.PlayerCombatState != null ? SolaisLight.Get(Owner) : 0;

    protected override decimal BonusDamage(Creature? target) =>
        IsMutable && Owner != null ? Light * SolaisLight.DamagePerLight(SwordCombat.LevelOf(Owner, SwordId.ClaiomhSolais)) : 0;

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await UnionAttack(cardPlay, DynamicVars.Damage.BaseValue, DrawCutProps).WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        await SolaisLight.ConsumeAll(Owner);
        var silverArm = Owner.Creature.GetPower<SolaisSilverArmPower>();
        if (silverArm != null) await silverArm.OnDrawCut(choiceContext);
    }
}

/// <summary>
/// 성유물의 빛 — Claíomh Solais + Durandal, Skill, Uncommon, 1. 6 (+⌊UnionLevel/2⌋) Block, 1 Light, and until your next
/// turn enemy attack hits deal Durandal's reduction (2..5 by Durandal level) less — so you can end the turn on Solais
/// (charging Light) and still have Durandal's guard.
/// Thematic only: Durandal's hilt holds holy relics (Song of Roland); Claíomh Solais is the "sword of light".
/// </summary>
public sealed class UnionReliquaryLight : UnionCard
{
    public UnionReliquaryLight() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(6);
        WithPower<SolaisLightPower>(1);
        WithCalculatedVar("Reduce", 2, 1, static (card, _) =>
            card.IsMutable && card.Owner is { } o ? DurandalBehavior.ReductionFor(SwordCombat.LevelOf(o, SwordId.Durandal)) - 2 : 0);
        WithTip(typeof(UnionRelicGracePower));
    }

    public override SwordId Primary => SwordId.ClaiomhSolais;
    public override SwordId Partner => SwordId.Durandal;
    public override UnionMotionKind Motion => UnionMotionKind.Halo;
    public override string BodyClip => "Cast_Ward";
    protected override int UnionBlockDivisor => 2;

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        PlayMotion(null);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await SolaisLight.Gain(choiceContext, Owner, DynamicVars["SolaisLightPower"].IntValue, this);
        var reduce = DurandalBehavior.ReductionFor(SwordCombat.LevelOf(Owner, SwordId.Durandal));
        await PowerCmd.Apply<UnionRelicGracePower>(choiceContext, Owner.Creature, reduce, Owner.Creature, this);
    }
}

/// <summary>
/// 두 대장간 — Gram + Ganjiang/Moye, Skill, Uncommon, 1, Exhaust. This combat Gram and Ganjiang/Moye each count as 1
/// level higher (max 5; same per-combat bonus as 레긴의 재단조). Counts as a twin card for 【짝】: it triggers 【짝】
/// effects and the next Ganjiang/Moye card triggers 【짝】 too.
/// Thematic (each legend: high confidence): the two great forging stories — Regin re-forging Gram from its shards,
/// and the smith couple Ganjiang and Moye, whose iron would only melt when hair and nails went into the furnace.
/// </summary>
public sealed class UnionTwoForges : UnionCard
{
    public UnionTwoForges() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("Levels", 1);
        WithKeywords(CardKeyword.Exhaust);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromKeyword(MagicSwordsmanKeywords.Pair)));
    }

    public override SwordId Primary => SwordId.Gram;
    public override SwordId Partner => SwordId.Ganjiang;
    public override UnionMotionKind Motion => UnionMotionKind.Forge;
    public override string BodyClip => "Power_Focus";

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        PlayMotion(null);
        var state = SwordCombat.Get(Owner);
        if (state != null)
        {
            foreach (var sword in new[] { SwordId.Gram, SwordId.Ganjiang })
            {
                var before = SwordCombat.LevelOf(Owner, sword);
                var gained = Math.Min(SwordRegistry.MaxLevel, before + DynamicVars["Levels"].IntValue) - before;
                if (gained > 0)
                    state.AddCounter(SwordRegistry.GetDefinition(sword).LevelOwner, SwordCombat.CombatLevelBonusKey, gained);
            }

            await SwordCombat.EnsureCurrentSwordPower(Owner, choiceContext);
            SwordVisuals.Sync(Owner); // redraws the swords at their new level
        }

        await PairRules.Resolve(Owner, null, isTwin: true, null);
    }
}

/// <summary>
/// 숨긴 칼 — Durandal + Ganjiang/Moye, Skill, Uncommon, 1, Retain. 7 (+⌊UnionLevel/2⌋) Block; every time it is retained
/// over a turn end, +3 Block for the rest of the combat. Counts as a twin card for 【짝】 (next Ganjiang/Moye card
/// triggers it).
/// Lore (medium-high): Ganjiang hid the male sword before going to the king (Soushen ji); the dying Roland lay down
/// over Durandal so the enemy would not take it (Song of Roland).
/// </summary>
public sealed class UnionHiddenBlades : UnionCard
{
    private int _retainBonus;

    public UnionHiddenBlades() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(7);
        WithVar("RetainBonus", 3);
        WithKeywords(CardKeyword.Retain);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromKeyword(MagicSwordsmanKeywords.Pair)));
    }

    public override SwordId Primary => SwordId.Durandal;
    public override SwordId Partner => SwordId.Ganjiang;
    public override UnionMotionKind Motion => UnionMotionKind.Guard;
    public override string BodyClip => "Block_Cross";
    protected override int UnionBlockDivisor => 2;

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource,
        CardPlay? cardPlay)
    {
        var bonus = base.ModifyBlockAdditive(target, block, props, cardSource, cardPlay);
        return ReferenceEquals(cardSource, this) ? bonus + _retainBonus : bonus;
    }

    public override Task AfterFlush(PlayerChoiceContext choiceContext, Player player,
        IReadOnlyCollection<CardModel> flushedCards, IReadOnlyCollection<CardModel> retainedCards)
    {
        if (IsMutable && player == Owner && retainedCards.Contains(this))
            _retainBonus += DynamicVars["RetainBonus"].IntValue;
        return base.AfterFlush(choiceContext, player, flushedCards, retainedCards);
    }

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        PlayMotion(null);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PairRules.Resolve(Owner, null, isTwin: true, null);
    }
}

/// <summary>
/// 들불의 칼날 — Tyrfing + Kusanagi, Attack, Uncommon, 1. Exhaust every Status/Curse in hand, then deal 5
/// (+⌊UnionLevel/2⌋) damage to a random enemy 1 + (cards exhausted) times. Tyrfing current: ignores Block — and
/// Tyrfing's own deed curses are fuel.
/// Lore (medium): Kusanagi cut the burning grass to escape a wildfire; Tyrfing's blade shone like flame
/// (Hervarar saga). The "fire" link is ours.
/// </summary>
public sealed class UnionWildfireBlade : UnionCard
{
    public UnionWildfireBlade() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.RandomEnemy)
    {
        WithDamage(5);
        WithCalculatedVar("Hits", 1, 1, static (card, _) =>
            card.IsMutable && card.Owner is { PlayerCombatState: not null } o
                ? PileType.Hand.GetPile(o).Cards.Count(KusanagiPurify.IsStatusOrCurse)
                : 0);
    }

    public override SwordId Primary => SwordId.Tyrfing;
    public override SwordId Partner => SwordId.Kusanagi;
    public override UnionMotionKind Motion => UnionMotionKind.FireRing;
    public override string BodyClip => "Attack_Backhand";
    public override float Impact => 0.38f;
    protected override int UnionDamageDivisor => 2;

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var burned = await KusanagiPurify.ExhaustAllFromHand(choiceContext, Owner);
        await UnionAttack(cardPlay, 1 + burned).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}
