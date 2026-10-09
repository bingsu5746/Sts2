using MagicSwordsman.MagicSwordsmanCode.Cards.Durandal;
using MagicSwordsman.MagicSwordsmanCode.Cards.Gram;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Events.Legends;

// Six sword legend events (see SwordLegendEvent for pool rules). Every scene below uses only well-known parts of the
// sword's legend; the game effects are this mod's own reading of them. Numbers are [Claude] proposals.

/// <summary>
/// "파프니르의 심장" — 그람 (owners). Völsunga saga: Sigurd kills Fafnir with Gram from a pit; roasting the heart for Regin
/// he burns his finger, tastes the blood and understands the birds, who warn that Regin will betray him and tell him to
/// eat the heart himself; he takes the hoard (the cursed Nibelung gold, research report #20).
///   TASTE  — lose 4 HP, Gram +1 (locked at level 5)
///   EAT    — card 파프니르의 심장
///   HOARD  — 120 gold + curse 니벨룽의 보물; Gram's own level-5 Nibelung curse will not come again (same treasure)
/// </summary>
public sealed class FafnirsHeartEvent : SwordLegendEvent
{
    protected override SwordId Sword => SwordId.Gram;
    protected override string Portrait => "gram_fafnirs_heart.jpg";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("TasteHpLoss", 4m),
        new GoldVar("HoardGold", 120),
    ];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice(CanUpgradeSword ? Taste : null, "TASTE").ThatDoesDamage(DynamicVars["TasteHpLoss"].BaseValue),
        Choice(Eat, "EAT", CardTip(ModelDb.Card<GramFafnirsHeart>())),
        Choice(Hoard, "HOARD", CardTip(ModelDb.Card<NibelungTreasure>())),
    ];

    private async Task Taste()
    {
        if (!await LoseHp(DynamicVars["TasteHpLoss"].BaseValue)) return;
        await ChangeSwordLevel(+1);
        Finish("TASTE", AddSwordVars);
    }

    private async Task Eat()
    {
        await AddCardToDeck(ModelDb.Card<GramFafnirsHeart>());
        Finish("EAT");
    }

    private async Task Hoard()
    {
        await PlayerCmd.GainGold(DynamicVars["HoardGold"].BaseValue, Owner!);
        // The treasure is already on the cart: Gram reaching level 5 later does not bring a second one.
        Tomb?.SetRunCounter(GramBehavior.NibelungCounter, 1);
        await AddCurseToDeck(ModelDb.Card<NibelungTreasure>());
        Finish("HOARD");
    }
}

/// <summary>
/// "삼쇠의 무덤불" — 티르빙 (players who do NOT own it, act 2+). Hervararkviða ("The Waking of Angantyr"): Hervor goes to
/// the barrows on Samsø where her father Angantyr and his brothers, the sons of Arngrim, lie among barrow fires, wakes
/// him and demands Tyrfing; he warns that it will destroy her whole line, and gives it to her.
///   DEMAND — Tyrfing (release a sword if 만검총 is full; cancelling returns), then lose 7 HP
///   HEED   — leave the sword; remove a card from the deck
/// </summary>
public sealed class BarrowFiresEvent : SwordLegendEvent
{
    protected override SwordId Sword => SwordId.Tyrfing;
    protected override bool ForOwners => false;
    protected override int MinActIndex => 1;
    protected override string Portrait => "tyrfing_barrow_fires.jpg";

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("FireHpLoss", 7m)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var canDemand = Tomb != null && !OwnsSword;
        return
        [
            Choice(canDemand ? Demand : null, "DEMAND").ThatDoesDamage(DynamicVars["FireHpLoss"].BaseValue),
            Choice(HasRemovable() ? Heed : null, "HEED"),
        ];
    }

    private void ShowStart() => SetEventState(InitialDescription, GenerateInitialOptionsWrapper());

    private async Task Demand()
    {
        if (!await SwordEventHelper.Acquire(Owner!, Sword, new BlockingPlayerChoiceContext()))
        {
            ShowStart(); // release cancelled: nothing paid
            return;
        }

        if (!await LoseHp(DynamicVars["FireHpLoss"].BaseValue)) return;
        Finish("DEMAND");
    }

    private async Task Heed()
    {
        await RemoveCards(1);
        Finish("HEED");
    }
}

/// <summary>
/// "회그니의 대답" — 다인슬레이프 (owners). Snorri's Skáldskaparmál (research report #22): Heðinn offers Högni gold as
/// atonement; Högni answers that it is too late, he has drawn Dáinsleif, which the dwarves made, which must kill a man
/// whenever it is drawn, never misses, and whose wounds never heal. Each night Hildr wakes the fallen and the battle
/// begins again (Hjaðningavíg).
///   DRAW   — Dainsleif +1, curse 화해할 수 없음 (locked at level 5)
///   RANSOM — 100 gold, Dainsleif −1 (nothing at level 0)
///   HILD   — lose 5 Max HP, heal to full
/// </summary>
public sealed class HognisAnswerEvent : SwordLegendEvent
{
    protected override SwordId Sword => SwordId.Dainsleif;
    protected override string Portrait => "dainsleif_clash_at_haey.jpg";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new GoldVar("RansomGold", 100),
        new DynamicVar("HildMaxHpLoss", 5m),
    ];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice(CanUpgradeSword ? Draw : null, "DRAW", CardTip(ModelDb.Card<IrreconcilableCurse>())),
        Choice(Ransom, "RANSOM"),
        Choice(Hild, "HILD").ThatDecreasesMaxHp(DynamicVars["HildMaxHpLoss"].BaseValue),
    ];

    private async Task Draw()
    {
        await ChangeSwordLevel(+1);
        await AddCurseToDeck(ModelDb.Card<IrreconcilableCurse>());
        Finish("DRAW", AddSwordVars);
    }

    private async Task Ransom()
    {
        await PlayerCmd.GainGold(DynamicVars["RansomGold"].BaseValue, Owner!);
        await ChangeSwordLevel(-1);
        Finish("RANSOM", AddSwordVars);
    }

    private async Task Hild()
    {
        var creature = Owner!.Creature;
        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), creature,
            DynamicVars["HildMaxHpLoss"].BaseValue, isFromCard: false);
        if (creature.IsDead) return;
        await CreatureCmd.Heal(creature, creature.MaxHp - creature.CurrentHp);
        Finish("HILD");
    }
}

/// <summary>
/// "론세스바예스" — 뒤랑달 (owners). The Song of Roland: Oliver asks Roland three times to sound the Olifant and he
/// refuses; when he finally blows it, his temples burst. Dying, he strikes Durendal on the rock to break it so that no
/// enemy takes it, but it does not break (research report #29: ten blows, not a scratch) and he lies down upon it.
///   HORN — lose 8 HP, card 올리펀트
///   ROCK — Durandal +1, curse 롤랑의 바위 (locked at level 5)
///   REST — heal 12
/// </summary>
public sealed class RoncevauxEvent : SwordLegendEvent
{
    protected override SwordId Sword => SwordId.Durandal;
    protected override string Portrait => "durandal_roncevaux.jpg";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HornHpLoss", 8m),
        new HealVar(12m),
    ];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice(Horn, "HORN", CardTip(ModelDb.Card<DurandalOlifant>())).ThatDoesDamage(DynamicVars["HornHpLoss"].BaseValue),
        Choice(CanUpgradeSword ? Rock : null, "ROCK", CardTip(ModelDb.Card<RolandsRock>())),
        Choice(Rest, "REST"),
    ];

    private async Task Horn()
    {
        if (!await LoseHp(DynamicVars["HornHpLoss"].BaseValue)) return;
        await AddCardToDeck(ModelDb.Card<DurandalOlifant>());
        Finish("HORN");
    }

    private async Task Rock()
    {
        await ChangeSwordLevel(+1);
        await AddCurseToDeck(ModelDb.Card<RolandsRock>());
        Finish("ROCK", AddSwordVars);
    }

    private async Task Rest()
    {
        await CreatureCmd.Heal(Owner!.Creature, DynamicVars.Heal.IntValue);
        Finish("REST");
    }
}

/// <summary>
/// "들불" — 쿠사나기 (owners). Kojiki (research report #19): Yamato Takeru, lured into a grass field that is set on fire,
/// cuts the grass with the sword his aunt Yamatohime gave him and lights a counter-fire with the flint from the bag
/// she gave him too; from then on Ame-no-Murakumo is called Kusanagi, "the grass-cutter".
///   CUT     — lose 6 HP, Kusanagi +1 (locked at level 5)
///   COUNTER — remove a card from the deck
/// </summary>
public sealed class WildfireEvent : SwordLegendEvent
{
    protected override SwordId Sword => SwordId.Kusanagi;
    protected override string Portrait => "kusanagi_wildfire.jpg";

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("CutHpLoss", 6m)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice(CanUpgradeSword ? Cut : null, "CUT").ThatDoesDamage(DynamicVars["CutHpLoss"].BaseValue),
        Choice(HasRemovable() ? Counter : null, "COUNTER"),
    ];

    private async Task Cut()
    {
        if (!await LoseHp(DynamicVars["CutHpLoss"].BaseValue)) return;
        await ChangeSwordLevel(+1);
        Finish("CUT", AddSwordVars);
    }

    private async Task Counter()
    {
        await RemoveCards(1);
        Finish("COUNTER");
    }
}

/// <summary>
/// "도키마사의 꿈" — 오니마루 (owners). Taiheiki (research report #5: the sword moved by itself and cut an oni): Hōjō
/// Tokimasa falls ill, tormented every night by a small oni in his dreams; an old man in the dream says he is the sword,
/// too rusted to leave its scabbard; once the rust is cleaned off and the sword is left standing unsheathed, it falls
/// on its own and cuts the head off the oni carved on a brazier stand, and Tokimasa recovers.
///   POLISH — pay 60 gold, Onimaru +1 (locked at level 5 or without the gold)
///   STAND  — remove a curse from the deck (locked without a removable curse)
///   ENDURE — lose 6 HP, Max HP +4
/// </summary>
public sealed class TokimasasDreamEvent : SwordLegendEvent
{
    protected override SwordId Sword => SwordId.Onimaru;
    protected override string Portrait => "onimaru_taiheiki.jpg";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new GoldVar("PolishGold", 60),
        new DynamicVar("EndureHpLoss", 6m),
        new MaxHpVar(4m),
    ];

    private static bool IsCurse(CardModel card) => card.Type == CardType.Curse;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var canPolish = CanUpgradeSword && Owner != null && Owner.Gold >= DynamicVars["PolishGold"].IntValue;
        return
        [
            Choice(canPolish ? Polish : null, "POLISH"),
            Choice(HasRemovable(IsCurse) ? Stand : null, "STAND"),
            Choice(Endure, "ENDURE").ThatDoesDamage(DynamicVars["EndureHpLoss"].BaseValue),
        ];
    }

    private async Task Polish()
    {
        await PlayerCmd.LoseGold(DynamicVars["PolishGold"].BaseValue, Owner!, GoldLossType.Spent);
        await ChangeSwordLevel(+1);
        Finish("POLISH", AddSwordVars);
    }

    private async Task Stand()
    {
        await RemoveCards(1, IsCurse);
        Finish("STAND");
    }

    private async Task Endure()
    {
        if (!await LoseHp(DynamicVars["EndureHpLoss"].BaseValue)) return;
        await CreatureCmd.GainMaxHp(Owner!.Creature, DynamicVars.MaxHp.BaseValue);
        Finish("ENDURE");
    }
}
