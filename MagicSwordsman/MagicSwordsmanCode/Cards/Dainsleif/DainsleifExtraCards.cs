using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Dainsleif;

// 다인슬레이프 추가 카드 4장 (Common 2 · Uncommon 1 · Rare 1). Lore (Skáldskaparmál): Heðinn and Högni meet on the
// island of Háey; Hildr's peace offer comes too late because Dáinsleif, made by dwarves, is already drawn and must
// kill a man; every night Hildr wakes the slain with her spells and the battle starts again.

/// <summary>하에이 결전 (Common): 5 damage (+1/L), +2 for each card Dainsleif returned to the draw pile this turn.</summary>
public sealed class DainsleifClashAtHaey : DainsleifCard
{
    public const int PerReturn = 2;

    public DainsleifClashAtHaey() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(5);
        WithDamagePerLevel(1);
        WithVar("PerReturn", PerReturn);
    }

    private int Returned => IsMutable && Owner != null ? DainsleifReturn.ReturnedThisTurn(Owner) : 0;

    protected override bool ShouldGlowGoldInternal => Returned > 0;

    protected override decimal BonusDamage(Creature? target) => PerReturn * Returned;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}

/// <summary>힐드의 밤 (Common skill): put up to 2 cards from the discard pile into the draw pile at random. Draw 1 (2 from level 3).</summary>
public sealed class DainsleifHildsNight : DainsleifCard
{
    public DainsleifHildsNight() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithVar("Recycle", 2);
        WithCards(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var discard = PileType.Discard.GetPile(Owner);
        if (discard.Cards.Count > 0)
        {
            var picked = (await CardSelectCmd.FromCombatPile(choiceContext, discard, Owner,
                new CardSelectorPrefs(SelectionScreenPrompt, 0, DynamicVars["Recycle"].IntValue))).ToList();
            foreach (var card in picked) await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Random);
        }

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue + (SwordLevel >= 3 ? 1 : 0), Owner);
    }
}

/// <summary>
/// 난쟁이의 담금질 (Uncommon power): whenever Dainsleif returns one of your Attack cards to the draw pile, that card
/// deals 1 more damage (2 from level 4) for the rest of this combat.
/// </summary>
public sealed class DainsleifDwarfForged : DainsleifCard
{
    public DainsleifDwarfForged() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithCalculatedVar("Growth", 1, static (card, _) => card is MagicSwordCard { SwordLevel: >= 4 } ? 1 : 0);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<DainsleifDwarfForgedPower>()));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<DainsleifDwarfForgedPower>(choiceContext, Owner.Creature, SwordLevel >= 4 ? 2 : 1,
            Owner.Creature, this);
    }
}

/// <summary>
/// 이미 뽑힌 칼 (Rare): cost 2. 18 damage (+2/L). If this kills the enemy, gain 2 energy and draw 2 cards;
/// otherwise lose 4 HP.
/// </summary>
public sealed class DainsleifAlreadyDrawn : DainsleifCard
{
    public const int KillEnergy = 2;
    public const int HpLoss = 4;

    public DainsleifAlreadyDrawn() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(18);
        WithDamagePerLevel(2);
        WithVar("KillEnergy", KillEnergy);
        WithCards(2);
        WithVar("HpLoss", HpLoss);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var attack = await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        if (attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
        {
            await PlayerCmd.GainEnergy(KillEnergy, Owner);
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        }
        else
        {
            await CreatureCmd.Damage(choiceContext, Owner.Creature, HpLoss, DamageProps.cardHpLoss, this);
        }
    }
}
