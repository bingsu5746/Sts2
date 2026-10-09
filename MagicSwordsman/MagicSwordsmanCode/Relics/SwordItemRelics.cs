using MagicSwordsman.MagicSwordsmanCode.Cards.Union;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Dialogue;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Relics;

// Second batch of 마검사 relics (user request 2026-10-09 "포션 좋다 유물이나"): items tied to 친밀도
// (Dialogue/SwordAffinity.cs: 경계 0 / 익숙 1 / 신뢰 2 / 각별 3), switching, 【조합】 cards and the sword legends.
// Pool registration: MagicSwordsmanRelic carries [Pool(typeof(MagicSwordsmanRelicPool))].
// The game has no Boss relic rarity (RelicRarity: Starter, Common, Uncommon, Rare, Shop, Event, Ancient); its
// trade-off relics (Sozu, VelvetChoker…) are Ancient and only Ancients offer them, so the trade-off relic here is Rare.

/// <summary>
/// 검수 매듭 (Common): every affinity gain is 1 higher (<see cref="IAffinityGainModifier"/>). Upon pickup, every owned
/// sword gains 5 affinity. Small run-long accelerator (a card play gives 2 instead of 1, a victory 4 instead of 3).
/// </summary>
public sealed class SwordTassel : MagicSwordsmanRelic, IAffinityGainModifier
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Bonus", 1m), new DynamicVar("Affinity", 5m)];

    public int ModifyAffinityGain(Player player, SwordId sword, int amount) =>
        player == Owner ? DynamicVars["Bonus"].IntValue : 0;

    public override async Task AfterObtained()
    {
        await base.AfterObtained();
        if (Owner.GetRelic<Mangeomchong>() is not { } tomb) return;
        foreach (var sword in tomb.OwnedSwords)
            SwordAffinity.Add(Owner, sword, DynamicVars["Affinity"].IntValue); // raw: the pickup gift is not boosted
    }
}

/// <summary>
/// 손때 묻은 손잡이 (Uncommon): whenever you summon a sword at 신뢰 or higher, draw 1 card; at 각별 also gain 1 energy.
/// Summons happen once per sword per combat, so this is ~1-3 triggers a fight. Base comparison: Lantern (Common,
/// 1 energy on turn 1) for the energy part; 삼백 개의 풀무 (Uncommon) for the per-summon draw.
/// </summary>
public sealed class WornGrip : MagicSwordsmanRelic, ISwordListener
{
    private const int TrustTier = 2;
    private const int DevotedTier = 3;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new EnergyVar(1)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.ForEnergy(this)];

    public async Task AfterSwordSummoned(Player player, SwordId sword, PlayerChoiceContext choiceContext)
    {
        if (player != Owner) return;
        var tier = SwordAffinity.Tier(player, sword);
        if (tier < TrustTier) return;
        Flash();
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        if (tier >= DevotedTier) await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
    }
}

/// <summary>
/// 마음의 칼집 (Rare): during combat, swords at 신뢰 or higher count as 1 level higher (max 5)
/// (<see cref="ISwordLevelModifier"/>, read by SwordCombat.LevelOf). Raises the current-sword effect and every card of
/// that sword at once. Base comparison: Vajra (Common, Strength 1) — a permanent +1 to one sword's numbers that must
/// first be earned (신뢰 = 35 affinity), so Rare.
/// </summary>
public sealed class HeartboundScabbard : MagicSwordsmanRelic, ISwordLevelModifier
{
    private const int TrustTier = 2;

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Levels", 1m)];

    public int CombatLevelBonus(Player player, SwordId sword) =>
        player == Owner && SwordAffinity.Tier(player, sword) >= TrustTier ? DynamicVars["Levels"].IntValue : 0;
}

/// <summary>
/// 날밑 (Uncommon): whenever the current sword changes (no sword -> first sword counts, content doc §0.3), deal 3
/// damage to a random enemy (Unpowered). Random enemy: game ForgottenSoul (Rng.CombatTargets over HittableEnemies).
/// Base comparison: LetterOpener (Uncommon: every 3 Skills, 5 damage to ALL enemies).
/// </summary>
public sealed class Tsuba : MagicSwordsmanRelic, ISwordListener
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(3m, ValueProp.Unpowered)];

    public async Task AfterSwordSwitched(Player player, SwordId? from, SwordId to, PlayerChoiceContext choiceContext)
    {
        if (player != Owner || Owner.Creature.CombatState is not { } combat) return;
        var target = Owner.RunState.Rng.CombatTargets.NextItem(combat.HittableEnemies);
        if (target == null) return;
        Flash();
        await CreatureCmd.Damage(choiceContext, target, DynamicVars.Damage, Owner.Creature);
    }
}

/// <summary>
/// 두 자루 칼집 (Uncommon): the first 【조합】 card you play each turn gives back 1 energy. 【조합】 cards cost 1-2 and need
/// two owned swords, so this is a build-around for the combo cards. Base comparison: Nunchaku (Uncommon: 1 energy
/// every 10 Attacks).
/// </summary>
public sealed class TwinScabbard : MagicSwordsmanRelic
{
    private int _lastTurn;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(1)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.ForEnergy(this)];

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner || cardPlay.Card is not UnionCard || !cardPlay.IsFirstInSeries) return;
        if (!CombatManager.Instance.IsInProgress) return;
        var turn = Owner.PlayerCombatState?.TurnNumber ?? 0;
        if (turn == 0 || turn == _lastTurn) return;
        _lastTurn = turn;
        Flash();
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _lastTurn = 0;
        return Task.CompletedTask;
    }
}

/// <summary>
/// 올리판트 (Uncommon): the first time each combat that taking damage leaves you at 50% HP or less, gain 10 Block and
/// draw 2 cards. Roland blew his horn Olifant at Roncevaux when the rear guard was overwhelmed (Chanson de Roland).
/// Base comparison: CentennialPuzzle (Common: first unblocked damage each combat, draw 3) and MeatOnTheBone (Rare,
/// 50% HP threshold, heal 12). Trigger hook as CentennialPuzzle (AfterDamageReceived, once per combat).
/// </summary>
public sealed class Olifant : MagicSwordsmanRelic
{
    private bool _usedThisCombat;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpThreshold", 50m),
        new BlockVar(10m, ValueProp.Unpowered),
        new CardsVar(2),
    ];

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (_usedThisCombat || !CombatManager.Instance.IsInProgress) return;
        if (target != Owner.Creature || result.UnblockedDamage <= 0 || Owner.Creature.IsDead) return;
        if (Owner.Creature.CurrentHp * 100 > Owner.Creature.MaxHp * DynamicVars["HpThreshold"].IntValue) return;
        _usedThisCombat = true;
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _usedThisCombat = false;
        return Task.CompletedTask;
    }
}

/// <summary>
/// 은의 손 (Uncommon): whenever you draw a Curse, draw 1 card. Nuada lost his arm in the First Battle of Mag Tuired and
/// Dian Cécht made him an arm of silver (Airgetlám) — a replacement for what was lost. Pairs with the mod's many sword
/// curses (incl. 솔라시's "누아다의 잃은 팔"). Pattern: game IterationPower (draw when a Status is drawn).
/// </summary>
public sealed class SilverHand : MagicSwordsmanRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Owner != Owner || card.Type != CardType.Curse) return;
        Flash();
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}

/// <summary>
/// 식은 용광로 (Rare, trade-off): gain 1 energy at the start of each turn; you can no longer use 마검 강화 at rest sites
/// (Mangeomchong.TryModifyRestSiteOptions skips the option while this relic is held; events that raise levels still
/// work). Legend: Ganjiang's furnace in which the iron would not melt. Base comparison: Sozu (Ancient: +1 energy,
/// no more potions) — the game's "boss relic" trade-offs are Ancient, which the mod cannot put in a drop pool.
/// </summary>
public sealed class CooledFurnace : MagicSwordsmanRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(1)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.ForEnergy(this)];

    public override decimal ModifyMaxEnergy(Player player, decimal amount) =>
        player != Owner ? amount : amount + DynamicVars.Energy.BaseValue;
}

/// <summary>
/// 검총의 등불 (Shop): at the start of each combat (your first turn), the owned sword with the highest affinity that is
/// not out yet is summoned (not made current). As a summon it fires 만검총's first-summon bonus right away and every
/// "when summoned" effect. Ties: lowest SwordId (deterministic on every client).
/// </summary>
public sealed class TombLantern : MagicSwordsmanRelic
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || Owner.PlayerCombatState is not { TurnNumber: 1 }) return;
        if (Owner.GetRelic<Mangeomchong>() is not { } tomb || SwordCombat.Get(Owner) is not { } state) return;
        var pick = tomb.OwnedSwords
            .Where(s => !state.Present.Contains(s) && !state.Summoned.Contains(s) && !state.ReturnedToVault.Contains(s))
            .OrderByDescending(s => SwordAffinity.Points(Owner, s))
            .ThenBy(s => (int)s)
            .Cast<SwordId?>()
            .FirstOrDefault();
        if (pick is not { } sword) return;
        Flash();
        await SwordCombat.Summon(Owner, sword, choiceContext);
    }
}
