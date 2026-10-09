using MagicSwordsman.MagicSwordsmanCode.Cards.Caladbolg;
using MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;
using MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;
using MagicSwordsman.MagicSwordsmanCode.Cards.Skofnung;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace MagicSwordsman.MagicSwordsmanCode.Events.Legends;

// Four more sword legend events (user request 2026-10-09 "각각의 검의 전용 이벤트 다 만들어줘"), one for each sword that
// had none. Same pool rules as LegendEvents.cs (see SwordLegendEvent). Only well-known parts of each legend are used;
// the game effects are this mod's own reading of them. Numbers are [Claude] proposals.
// Text: MAGICSWORDSMAN-<EVENT_ID>.* in the events table (fragment loc_fragments/events4.<lang>.events.json).

/// <summary>
/// "스케기의 경고" — 스코프눙 (owners). Skofnung was the sword of the Danish king Hrólfr kraki (Hrólfs saga kraka), whose
/// champions included his berserkers; Skeggi of Miðfjörðr took it from Hrólfr's mound (Laxdæla saga). In Kormáks saga
/// Skeggi lends it to Kormákr for his duel with Bersi and warns: it must not be drawn where a woman is present, the sun
/// must not shine on the hilt, a little snake will come from under the hilt, and a wound it makes heals only when
/// rubbed with the Skofnung stone.
///   TABOO     — Skofnung +1, curse 스코프눙 돌 (locked at level 5)
///   CHAMPIONS — lose 7 HP, card 흐롤프의 용사들
///   STONE     — heal 14
/// </summary>
public sealed class SkeggisWarningEvent : SwordLegendEvent
{
    protected override SwordId Sword => SwordId.Skofnung;
    protected override string Portrait => "skofnung_keep_the_taboo.jpg";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ChampionsHpLoss", 7m),
        new HealVar(14m),
    ];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice(CanUpgradeSword ? Taboo : null, "TABOO", CardTip(ModelDb.Card<SkofnungStone>())),
        Choice(Champions, "CHAMPIONS", CardTip(ModelDb.Card<SkofnungHrolfsChampions>()))
            .ThatDoesDamage(DynamicVars["ChampionsHpLoss"].BaseValue),
        Choice(Stone, "STONE"),
    ];

    private async Task Taboo()
    {
        await ChangeSwordLevel(+1);
        await AddCurseToDeck(ModelDb.Card<SkofnungStone>());
        Finish("TABOO", AddSwordVars);
    }

    private async Task Champions()
    {
        if (!await LoseHp(DynamicVars["ChampionsHpLoss"].BaseValue)) return;
        await AddCardToDeck(ModelDb.Card<SkofnungHrolfsChampions>());
        Finish("CHAMPIONS");
    }

    private async Task Stone()
    {
        await CreatureCmd.Heal(Owner!.Creature, DynamicVars.Heal.IntValue);
        Finish("STONE");
    }
}

/// <summary>
/// "막야의 머리카락" — 간장·막야 (owners of the pair). Wuyue Chunqiu: the iron would not melt in Ganjiang's furnace
/// until Moye cut her hair and nails and threw them in (with boys and girls working the bellows); the two swords were
/// named after the couple. Soushenji: the king (of Chu) waited three years for the swords; Ganjiang gave him only the
/// female sword, hid the male one and was killed; his son Chi (Mei Jian Chi) found it, gave his own head and the sword
/// to a stranger, who used them to cut off the king's head in the cauldron, then his own.
///   HAIR      — lose 5 Max HP, Ganjiang·Moye +1 (locked at level 5)
///   HIDE      — card 숨긴 칼
///   VENGEANCE — lose 9 HP, card 복수
/// </summary>
public sealed class MoyesHairEvent : SwordLegendEvent
{
    protected override SwordId Sword => SwordId.Ganjiang;
    protected override string Portrait => "moye_hair_and_nails.jpg";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HairMaxHpLoss", 5m),
        new DynamicVar("VengeanceHpLoss", 9m),
    ];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice(CanUpgradeSword ? Hair : null, "HAIR").ThatDecreasesMaxHp(DynamicVars["HairMaxHpLoss"].BaseValue),
        Choice(Hide, "HIDE", CardTip(ModelDb.Card<GanjiangHiddenBlade>())),
        Choice(Vengeance, "VENGEANCE", CardTip(ModelDb.Card<GanjiangChisVengeance>()))
            .ThatDoesDamage(DynamicVars["VengeanceHpLoss"].BaseValue),
    ];

    private async Task Hair()
    {
        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner!.Creature,
            DynamicVars["HairMaxHpLoss"].BaseValue, isFromCard: false);
        if (Owner.Creature.IsDead) return;
        await ChangeSwordLevel(+1);
        Finish("HAIR", AddSwordVars);
    }

    private async Task Hide()
    {
        await AddCardToDeck(ModelDb.Card<GanjiangHiddenBlade>());
        Finish("HIDE");
    }

    private async Task Vengeance()
    {
        if (!await LoseHp(DynamicVars["VengeanceHpLoss"].BaseValue)) return;
        await AddCardToDeck(ModelDb.Card<GanjiangChisVengeance>());
        Finish("VENGEANCE");
    }
}

/// <summary>
/// "은팔" — 클라이브 솔라시 (owners). Lebor Gabála Érenn / Cath Maige Tuired: the sword of Nuada was one of the four
/// treasures the Túatha Dé Danann brought, the one from Findias (no one escaped it once it was drawn from its sheath).
/// Nuada lost his arm in the first battle of Mag Tuired against the Fir Bolg and, no longer unblemished, gave up the
/// kingship; Dian Cécht, with Credne the brazier, made him a silver arm that moved like a real one. (Calling Nuada's
/// sword "Claíomh Solais" is a later identification, as elsewhere in this mod.)
///   SILVER — pay 80 gold, Claíomh Solais +1 (locked at level 5 or without the gold)
///   ARM    — card 은팔 + curse 누아다의 잃은 팔
///   THRONE — lose 4 Max HP, remove a card from the deck (locked without a removable card)
/// </summary>
public sealed class SilverArmEvent : SwordLegendEvent
{
    protected override SwordId Sword => SwordId.ClaiomhSolais;
    protected override string Portrait => "solais_silver_arm.jpg";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new GoldVar("SilverGold", 80),
        new DynamicVar("ThroneMaxHpLoss", 4m),
    ];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var canSilver = CanUpgradeSword && Owner != null && Owner.Gold >= DynamicVars["SilverGold"].IntValue;
        return
        [
            Choice(canSilver ? Silver : null, "SILVER"),
            Choice(Arm, "ARM", CardTip(ModelDb.Card<SolaisSilverArm>()), CardTip(ModelDb.Card<NuadasLostArm>())),
            Choice(HasRemovable() ? Throne : null, "THRONE")
                .ThatDecreasesMaxHp(DynamicVars["ThroneMaxHpLoss"].BaseValue),
        ];
    }

    private async Task Silver()
    {
        await PlayerCmd.LoseGold(DynamicVars["SilverGold"].BaseValue, Owner!, GoldLossType.Spent);
        await ChangeSwordLevel(+1);
        Finish("SILVER", AddSwordVars);
    }

    private async Task Arm()
    {
        await AddCardToDeck(ModelDb.Card<SolaisSilverArm>());
        await AddCurseToDeck(ModelDb.Card<NuadasLostArm>());
        Finish("ARM");
    }

    private async Task Throne()
    {
        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner!.Creature,
            DynamicVars["ThroneMaxHpLoss"].BaseValue, isFromCard: false);
        if (Owner.Creature.IsDead) return;
        await RemoveCards(1);
        Finish("THRONE");
    }
}

/// <summary>
/// "세 언덕" — 칼라드볼그 (owners). Táin Bó Cúailnge, the last battle: Fergus mac Róich, in exile with the men of
/// Connacht, gets his sword back and strikes at Conchobar, whose shield Ochain roars (and the shields of the Ulstermen
/// roar in answer); Cormac Cond Longas begs him not to kill his king, so Fergus turns the blow aside and cuts the tops
/// off three hills. When Cú Chulainn comes, Fergus keeps his promise to yield to him and leaves the field with his men,
/// and the battle turns against Connacht.
///   HILLS    — Caladbolg +1, curse 치지 못한 왕의 방패 (locked at level 5)
///   OCHAIN   — lose 6 HP, card 위압
///   WITHDRAW — transform a card (locked without a transformable card)
/// </summary>
public sealed class ThreeHillsEvent : SwordLegendEvent
{
    protected override SwordId Sword => SwordId.Caladbolg;
    protected override string Portrait => "caladbolg_three_hilltops.jpg";

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("OchainHpLoss", 6m)];

    private bool HasTransformable =>
        Owner != null && PileType.Deck.GetPile(Owner).Cards.Any(c => c.Type != CardType.Quest && c.IsTransformable);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice(CanUpgradeSword ? Hills : null, "HILLS", CardTip(ModelDb.Card<UnstruckKingsShield>())),
        Choice(Ochain, "OCHAIN", CardTip(ModelDb.Card<CaladbolgSparedKing>()))
            .ThatDoesDamage(DynamicVars["OchainHpLoss"].BaseValue),
        Choice(HasTransformable ? Withdraw : null, "WITHDRAW"),
    ];

    private async Task Hills()
    {
        await ChangeSwordLevel(+1);
        await AddCurseToDeck(ModelDb.Card<UnstruckKingsShield>());
        Finish("HILLS", AddSwordVars);
    }

    private async Task Ochain()
    {
        if (!await LoseHp(DynamicVars["OchainHpLoss"].BaseValue)) return;
        await AddCardToDeck(ModelDb.Card<CaladbolgSparedKing>());
        Finish("OCHAIN");
    }

    private async Task Withdraw()
    {
        var card = (await CardSelectCmd.FromDeckForTransformation(Owner!,
            new CardSelectorPrefs(CardSelectorPrefs.TransformSelectionPrompt, 1))).FirstOrDefault();
        if (card != null) await CardCmd.TransformToRandom(card, Rng, CardPreviewStyle.EventLayout);
        Finish("WITHDRAW");
    }
}
