using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

// 클라이브 솔라시 카드 묶음 4 (2026-10-16, user request: "Light gain and spend patterns are too samey"): three new cards,
// each a Light pattern no other Solais card has — Light from a kill, Light from Block, and a card whose effect is
// chosen by how much Light you hold. Names are descriptive (no lore claim beyond the torch/sheath of the sword).

/// <summary>
/// 승전의 횃불 (Common attack): 8 damage (+1/L). Fatal: if this kills an enemy, gain 3 Light.
/// Light gained from a kill (Fatal check as the game's HandOfGreed: target powers all ShouldOwnerDeathTriggerFatal
/// before the attack, so minions/summons do not count, + attack result WasTargetKilled).
/// </summary>
public sealed class SolaisVictoryTorch : SolaisCard
{
    public const int KillLight = 3;

    public SolaisVictoryTorch() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(8);
        WithDamagePerLevel(1);
        WithVar("KillLight", KillLight);
        WithTip(typeof(SolaisLightPower));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // Fatal = the game's rule (HandOfGreed / TyrfingGoldenHilt): checked before the attack, no minion/summon.
        var fatal = cardPlay.Target != null && cardPlay.Target.Powers.All(p => p.ShouldOwnerDeathTriggerFatal());
        var attack = await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (fatal && attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled) && !Owner.Creature.IsDead)
            await SolaisLight.Gain(choiceContext, Owner, KillLight, this);
    }
}

/// <summary>
/// 빛을 머금은 칼집 (Uncommon power): this combat, whenever you gain Block from a card, gain 1 Light (at most 2 times
/// per turn; more copies raise the cap: <see cref="SolaisLitSheathPower"/>). Light from defending.
/// </summary>
public sealed class SolaisLitSheath : SolaisCard
{
    public SolaisLitSheath() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<SolaisLitSheathPower>(SolaisLitSheathPower.CapPerCopy);
        WithTip(typeof(SolaisLightPower));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SolaisLitSheathPower>(choiceContext, Owner.Creature,
            DynamicVars["SolaisLitSheathPower"].BaseValue, Owner.Creature, this);
    }
}

/// <summary>
/// 빛 가늠 (Uncommon skill): the effect depends on your Light when played —
///  0 Light: gain 2 Light and draw 1 card;
///  1–2 Light: gain 9 Block (+1/L);
///  3+ Light: spend 3 Light and gain 3 Strength.
/// A mode switch read from the Light count (user request: spending should differ in kind, not only in numbers).
/// </summary>
public sealed class SolaisMeasureLight : SolaisCard
{
    public const int EmptyLight = 2;
    public const int HighThreshold = 3;
    public const int StrengthGain = 3;

    public SolaisMeasureLight() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("EmptyLight", EmptyLight);
        WithCards(1);
        WithBlock(9);
        WithBlockPerLevel(1);
        WithVar("HighLight", HighThreshold);
        WithVar("StrengthGain", StrengthGain);
        WithTip(typeof(SolaisLightPower));
        WithTip(typeof(StrengthPower));
    }

    protected override bool ShouldGlowGoldInternal => Light >= HighThreshold;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var light = Light;
        if (light <= 0)
        {
            await SolaisLight.Gain(choiceContext, Owner, EmptyLight, this);
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        }
        else if (light < HighThreshold)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        }
        else if (await SolaisLightSpend.TrySpend(choiceContext, Owner, HighThreshold, this))
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, StrengthGain, Owner.Creature, this);
        }
    }
}
