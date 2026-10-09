using BaseLib.Abstracts;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Events.Legends;

/// <summary>
/// Base of the sword legend events (user request 2026-10-09 "특정 검들 전용 이벤트"): ? room events tied to one sword's
/// legend. Same registration as <see cref="TombEpitaphEvent"/>: BaseLib adds a CustomEventModel without Acts to the
/// shared ? room pool of every act; the game skips events whose <see cref="IsAllowed"/> is false and never repeats a
/// visited one (so each event happens at most once per run).
///
/// Allowed only when some player owns 만검총 (= this character) and, depending on <see cref="ForOwners"/>, owns the
/// sword (most events) or does not own it yet (acquisition events), from act index <see cref="MinActIndex"/> on.
/// Not shared in multiplayer: each player gets their own copy; options that need the sword are locked for a player
/// without it, and every event keeps at least one option that never needs it.
/// Picture: an existing card portrait of that sword (images/card_portraits/big), no new art.
/// Text: events table, MAGICSWORDSMAN-&lt;EVENT_ID&gt;.* (fragment loc_fragments/upgrade_events.&lt;lang&gt;.events.json).
/// </summary>
public abstract class SwordLegendEvent : CustomEventModel
{
    protected abstract SwordId Sword { get; }

    /// <summary>True: only for players who own the sword. False: only for players who do not own it (or its partner).</summary>
    protected virtual bool ForOwners => true;

    /// <summary>First act index (0 = act 1) in which the event may appear.</summary>
    protected virtual int MinActIndex => 0;

    /// <summary>File name under images/card_portraits/big/.</summary>
    protected abstract string Portrait { get; }

    public override string? CustomInitialPortraitPath => $"{MainFile.ResPath}/images/card_portraits/big/{Portrait}";

    public override bool IsAllowed(IRunState runState)
    {
        if (runState.CurrentActIndex < MinActIndex) return false;
        return runState.Players.Any(p => SwordEventHelper.Tomb(p) is { } tomb && Matches(tomb));
    }

    private bool Matches(Mangeomchong tomb) =>
        ForOwners ? tomb.Owns(Sword) : !SwordRegistry.WithPartners(Sword).Any(tomb.Owns);

    // ------------------------------------------------------------------ state of the owner

    protected Mangeomchong? Tomb => Owner != null ? SwordEventHelper.Tomb(Owner) : null;

    protected bool OwnsSword => Tomb?.Owns(Sword) == true;

    protected int SwordLevel => Tomb?.GetLevel(Sword) ?? 0;

    protected bool CanUpgradeSword => OwnsSword && SwordLevel < SwordRegistry.MaxLevel;

    // ------------------------------------------------------------------ text

    protected string PageKey(string page) => $"{Id.Entry}.pages.{page}";

    /// <summary>An option of the INITIAL page. <paramref name="onChosen"/> null = locked.</summary>
    protected EventOption Choice(Func<Task>? onChosen, string option, params IHoverTip[] tips) =>
        new(this, onChosen, $"{PageKey("INITIAL")}.options.{option}", tips);

    protected static IHoverTip CardTip(CardModel canonical) => HoverTipFactory.FromCard(canonical);

    /// <summary>Ends the event with the page's description; <paramref name="fill"/> adds variables.</summary>
    protected void Finish(string page, Action<LocString>? fill = null)
    {
        var text = L10NLookup($"{PageKey(page)}.description");
        fill?.Invoke(text);
        SetEventFinished(text);
    }

    /// <summary>{Sword}, {NewLevel}, {Stage} for a result page.</summary>
    protected void AddSwordVars(LocString text)
    {
        text.Add("Sword", SwordLore.Name(Sword));
        text.Add("NewLevel", SwordLevel);
        text.Add("Stage", SwordLore.StageName(Sword, SwordLevel));
    }

    // ------------------------------------------------------------------ effects

    /// <summary>HP loss that Block cannot stop (like TombEpitaphEvent). Returns false if the player died.</summary>
    protected async Task<bool> LoseHp(decimal amount)
    {
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner!.Creature, amount,
            ValueProp.Unblockable | ValueProp.Unpowered, (Creature?)null, (CardModel?)null);
        return !Owner.Creature.IsDead;
    }

    /// <summary>Changes the sword's level by <paramref name="delta"/> (clamped 0..5). Level 5 triggers SwordMastery.</summary>
    protected async Task ChangeSwordLevel(int delta)
    {
        if (Tomb is not { } tomb || !tomb.Owns(Sword)) return;
        await tomb.SetLevel(Sword, Math.Clamp(tomb.GetLevel(Sword) + delta, 0, SwordRegistry.MaxLevel));
    }

    protected async Task AddCardToDeck(CardModel canonical)
    {
        var card = Owner!.RunState.CreateCard(canonical, Owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 1.2f, CardPreviewStyle.EventLayout);
    }

    protected async Task AddCurseToDeck(CardModel canonicalCurse)
    {
        if (Tomb is { } tomb) await tomb.AddCurse(canonicalCurse);
        else await CardPileCmd.AddCursesToDeck([canonicalCurse], Owner!);
    }

    /// <summary>The player removes cards from the deck (only those matching <paramref name="filter"/>).</summary>
    protected async Task<int> RemoveCards(int count, Func<CardModel, bool>? filter = null)
    {
        var cards = (await CardSelectCmd.FromDeckForRemoval(Owner!,
            new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, count), filter)).ToList();
        if (cards.Count > 0) await CardPileCmd.RemoveFromDeck(cards);
        return cards.Count;
    }

    protected bool HasRemovable(Func<CardModel, bool>? filter = null) =>
        Owner != null && PileType.Deck.GetPile(Owner).Cards.Any(c => c.IsRemovable && (filter == null || filter(c)));
}
