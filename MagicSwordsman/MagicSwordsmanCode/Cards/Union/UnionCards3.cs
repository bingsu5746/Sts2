using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;
using MagicSwordsman.MagicSwordsmanCode.Cards.Kusanagi;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MagicSwordsman.MagicSwordsmanCode.Visuals;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Union;

// 【조합】 cards added 2026-10-15 (user request #39: more sword-pair cards, each pair with its own legend link or theme).
// New pairs: Tyrfing+Skofnung · Solais+Kusanagi · Gram+Durandal · Skofnung+Solais · Caladbolg+Dainsleif.
// Union totals after this file (17): Gram 4 · Tyrfing 4 · Durandal 4 · Kusanagi 4 · Skofnung 4 · Solais 4 ·
// Dainsleif 3 · Caladbolg 3 · Onimaru 2 · Ganjiang/Moye 2.

/// <summary>
/// 스물넷의 광전사 — Tyrfing + Skofnung, Attack, Rare, 2. Deal 2 (+⌊UnionLevel/4⌋) damage to the target once per
/// Spectral Scar on it (at least once; Scars burst at 12, so at most 11 hits). Tyrfing becomes current, so every hit
/// ignores Block; the Scars stay. A pay-off that reads the Scar count as a hit count instead of bonus damage.
/// Lore (high confidence): Tyrfing passed to the berserker Arngrim and his twelve berserker sons; Angantyr wielded it
/// at Samsø (Hervarar saga). Skofnung held the souls of King Hrólfr Kraki's twelve berserkers. Twelve and twelve.
/// </summary>
public sealed class UnionTwentyFourBerserkers : UnionCard
{
    public UnionTwentyFourBerserkers() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(2);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<SkofnungWoundPower>()));
    }

    public override SwordId Primary => SwordId.Tyrfing;
    public override SwordId Partner => SwordId.Skofnung;
    public override UnionMotionKind Motion => UnionMotionKind.Orbit;
    public override string BodyClip => "Attack_Flurry";
    public override float Impact => 0.35f;
    protected override int UnionDamageDivisor => 4;

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var scars = cardPlay.Target?.GetPowerAmount<SkofnungWoundPower>() ?? 0;
        await UnionAttack(cardPlay, Math.Max(1, scars)).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}

/// <summary>
/// 왕의 보물 — Claíomh Solais + Kusanagi, Skill, Uncommon, 1 (0 from combined level 6), Exhaust. Double your Light.
/// Reduce one of your debuffs by 1.
/// Lore (high confidence): Kusanagi is one of Japan's Three Sacred Treasures (with the mirror and the jewel — the
/// mirror that lured the sun goddess Amaterasu out of her cave); Claíomh Solais is one of the Four Treasures of the
/// Tuatha Dé Danann. Two royal regalia: the mirror throws the light back twice as bright.
/// </summary>
public sealed class UnionRoyalTreasures : UnionCard
{
    public const int CheaperFromUnionLevel = 6;

    public UnionRoyalTreasures() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithVar("CheaperAt", CheaperFromUnionLevel);
        WithTip(typeof(SolaisLightPower));
    }

    public override SwordId Primary => SwordId.ClaiomhSolais;
    public override SwordId Partner => SwordId.Kusanagi;
    public override UnionMotionKind Motion => UnionMotionKind.Halo;
    public override string BodyClip => "Cast_Ward";

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) || UnionLevel < CheaperFromUnionLevel || originalCost <= 0) return false;
        modifiedCost = 0;
        return true;
    }

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        PlayMotion(null);
        var light = SolaisLight.Get(Owner);
        if (light > 0) await SolaisLight.Gain(choiceContext, Owner, light, this);
        await KusanagiPurify.ReduceOneDebuff(choiceContext, Owner.Creature, this);
    }
}

/// <summary>
/// 모루와 바위 — Gram + Durandal, Attack, Uncommon, 1. Remove all of the target's Block and gain that much Block
/// (max 12 + combined level), then deal 7 (+⌊UnionLevel/2⌋) damage. Gram current: +Gram level.
/// Lore (high confidence): the re-forged Gram split Regin's anvil in two (Völsunga saga); the dying Roland struck
/// Durandal against the rock again and again to break it, and the blade would not even notch (Song of Roland).
/// One blade breaks what it hits, the other cannot be broken: the enemy's guard is split and ends up as yours.
/// </summary>
public sealed class UnionAnvilAndRock : UnionCard
{
    public const int BaseCap = 12;

    public UnionAnvilAndRock() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(7);
        WithUnionVar("Cap", BaseCap, 1);
        WithTip(StaticHoverTip.Block);
    }

    public override SwordId Primary => SwordId.Gram;
    public override SwordId Partner => SwordId.Durandal;
    public override UnionMotionKind Motion => UnionMotionKind.Sequence;
    public override string BodyClip => "Attack_Heavy";
    public override float Impact => 0.4f;
    protected override int UnionDamageDivisor => 2;

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is { } target && target.Block > 0)
        {
            var taken = target.Block;
            await CreatureCmd.LoseBlock(target, taken);
            await CreatureCmd.GainBlock(Owner.Creature, Math.Min(taken, UnionValue(BaseCap, 1)),
                MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered, null);
        }

        await UnionAttack(cardPlay).WithHitFx("vfx/vfx_attack_blunt").Execute(choiceContext);
    }
}

/// <summary>
/// 햇빛 닿은 자루 — Skofnung + Claíomh Solais, Skill, Uncommon, 1, all enemies. Consume all Light: apply 1 Spectral
/// Scar per Light consumed to ALL enemies, twice as many to enemies that already had Scars. (Not 【발도】.)
/// Skofnung becomes current, so the turn-1 sunlight taboo applies and — unless you switch back — Solais' remaining
/// Light would fade anyway: spend it here.
/// Lore (high confidence for each): Skofnung must never be drawn with sunlight falling on its hilt (Laxdæla /
/// Kormáks saga); Claíomh Solais is "the sword of light". The broken taboo makes the ghost-wounds flare.
/// </summary>
public sealed class UnionSunlitHilt : UnionCard
{
    public UnionSunlitHilt() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithTip(typeof(SolaisLightPower));
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<SkofnungWoundPower>()));
    }

    public override SwordId Primary => SwordId.Skofnung;
    public override SwordId Partner => SwordId.ClaiomhSolais;
    public override UnionMotionKind Motion => UnionMotionKind.BeamArc;
    public override string BodyClip => "Cast_Trace";

    protected override bool ShouldGlowGoldInternal =>
        IsMutable && Owner?.PlayerCombatState != null && SolaisLight.Get(Owner) > 0;

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState is not { } cs) return;
        var enemies = cs.HittableEnemies.ToList();
        PlayMotion(enemies.FirstOrDefault());
        var light = await SolaisLight.ConsumeAll(Owner);
        if (light <= 0) return;
        foreach (var enemy in enemies)
        {
            if (!enemy.IsAlive) continue;
            var amount = enemy.GetPowerAmount<SkofnungWoundPower>() > 0 ? light * 2 : light;
            await PowerCmd.Apply<SkofnungWoundPower>(choiceContext, enemy, amount, Owner.Creature, this);
        }
    }
}

/// <summary>
/// 돌린 칼끝 — Caladbolg + Dáinsleif, Attack, Uncommon, 1. Deal 6 (+⌊UnionLevel/2⌋) damage (Caladbolg current: up to
/// 3 enemies). For each enemy hit, you may put a card from your discard pile on top of your draw pile.
/// Lore (high confidence for each): in the Táin, Fergus is held back from killing King Conchobar and turns his blow
/// aside, cutting the tops off three hills instead; in the Hjaðningavíg, once Dáinsleif is drawn the fight can never
/// end — every night Hildr wakes the fallen and the battle begins again (Skáldskaparmál). The blow turned onto many
/// brings the fallen (cards) back to fight again.
/// </summary>
public sealed class UnionTurnedBlow : UnionCard
{
    public UnionTurnedBlow() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(6);
    }

    public override SwordId Primary => SwordId.Caladbolg;
    public override SwordId Partner => SwordId.Dainsleif;
    public override UnionMotionKind Motion => UnionMotionKind.Cross;
    public override string BodyClip => "Attack_Sweep";
    public override float Impact => 0.36f;
    protected override int UnionDamageDivisor => 2;

    protected override async Task OnUnionPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var attack = await UnionAttack(cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        var hit = attack.Results.SelectMany(r => r).Where(r => r.TotalDamage > 0).Select(r => r.Receiver).Distinct()
            .Count();
        var discard = PileType.Discard.GetPile(Owner);
        if (hit <= 0 || discard.Cards.Count == 0) return;
        var picked = (await CardSelectCmd.FromCombatPile(choiceContext, discard, Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 0, hit))).ToList();
        foreach (var card in picked) await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top);
    }
}
