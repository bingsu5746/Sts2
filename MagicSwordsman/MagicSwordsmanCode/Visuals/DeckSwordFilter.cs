using Godot;
using HarmonyLib;
using MagicSwordsman.MagicSwordsmanCode.Cards;
using MagicSwordsman.MagicSwordsmanCode.Cards.Union;
using MagicSwordsman.MagicSwordsmanCode.Character;
using MagicSwordsman.MagicSwordsmanCode.Events;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// "검별" button on the deck view (user request 2026-10-15 "덱 볼 때 마검 카드끼리 모아서 볼 수 있는 필터"): one more sort-style
/// button next to the game's four sorters. Each click cycles
///   전체 → 묶어 보기 (whole deck, ordered sword by sword) → each owned sword → 공용 → 조합 → 저주 → 전체
/// skipping groups with no card. 전체 is exactly the vanilla screen; in the single-group modes the vanilla sorters still
/// sort the shown cards; 묶어 보기 orders by group first and by the vanilla sorters inside each group.
///
/// Built by duplicating the game's alphabetical sort button (native look), shown only for Ensifer or a deck holding
/// sword cards. The filter lives on the screen instance; reopening the deck starts at 전체.
/// Hooks: NDeckViewScreen._Ready (add the button) and NDeckViewScreen.DisplayCards (apply the filter — every vanilla
/// path, sorters and deck changes, goes through it). Presentation only, never throws.
/// UNVERIFIED in game: the button's placement in the sorter row.
/// </summary>
internal static class DeckSwordFilter
{
    private const string Table = "card_selection";
    private const string Prefix = "MAGICSWORDSMAN-DECK_FILTER";

    private enum Kind { All, Grouped, Sword, Shared, Union, Curse }

    private readonly record struct Mode(Kind Kind, SwordId Sword = SwordId.Gram);

    private sealed class State
    {
        public Mode Current = new(Kind.All);
        public NCardViewSortButton? Button;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NDeckViewScreen, State> States = new();

    private static readonly AccessTools.FieldRef<NDeckViewScreen, Player> PlayerRef =
        AccessTools.FieldRefAccess<NDeckViewScreen, Player>("_player");
    private static readonly AccessTools.FieldRef<NDeckViewScreen, CardPile> PileRef =
        AccessTools.FieldRefAccess<NDeckViewScreen, CardPile>("_pile");
    private static readonly AccessTools.FieldRef<NDeckViewScreen, NCardViewSortButton> AlphabetRef =
        AccessTools.FieldRefAccess<NDeckViewScreen, NCardViewSortButton>("_alphabetSorter");
    private static readonly AccessTools.FieldRef<NDeckViewScreen, List<SortingOrders>> PriorityRef =
        AccessTools.FieldRefAccess<NDeckViewScreen, List<SortingOrders>>("_sortingPriority");
    private static readonly AccessTools.FieldRef<NCardsViewScreen, NCardGrid> GridRef =
        AccessTools.FieldRefAccess<NCardsViewScreen, NCardGrid>("_grid");
    private static readonly AccessTools.FieldRef<NCardsViewScreen, List<CardModel>> CardsRef =
        AccessTools.FieldRefAccess<NCardsViewScreen, List<CardModel>>("_cards");
    private static readonly System.Reflection.PropertyInfo? SortingAlgorithms =
        AccessTools.Property(typeof(NCardGrid), "SortingAlgorithms");
    private static readonly System.Reflection.MethodInfo? DisplayCardsMethod =
        AccessTools.Method(typeof(NDeckViewScreen), "DisplayCards");

    // ------------------------------------------------------------------ hooks

    [HarmonyPatch(typeof(NDeckViewScreen), nameof(NDeckViewScreen._Ready))]
    private static class ReadyPatch
    {
        [HarmonyPostfix]
        private static void Postfix(NDeckViewScreen __instance)
        {
            try { AddButton(__instance); }
            catch (Exception e) { MainFile.Logger.Warn($"[DeckSwordFilter] button: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(NDeckViewScreen), "DisplayCards")]
    private static class DisplayPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(NDeckViewScreen __instance)
        {
            try { return Apply(__instance); }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"[DeckSwordFilter] display: {e.Message}");
                return true;
            }
        }
    }

    // ------------------------------------------------------------------ button

    private static bool IsRelevant(Player player, CardPile pile) =>
        player.Character is MagicSwordsmanCharacter || pile.Cards.Any(c => c is MagicSwordCard { IsSwordCard: true });

    private static void AddButton(NDeckViewScreen screen)
    {
        var player = PlayerRef(screen);
        var pile = PileRef(screen);
        var template = AlphabetRef(screen);
        if (player == null || pile == null || template == null || !IsRelevant(player, pile)) return;
        if (template.GetParent() is not { } parent) return;

        if (template.Duplicate() is not NCardViewSortButton button) return;
        button.Name = "MagicSwordsmanSwordFilter";
        var state = States.GetOrCreateValue(screen);
        state.Button = button;
        parent.AddChild(button);
        parent.MoveChild(button, template.GetIndex() + 1);
        if (parent is not Container)
            button.Position = template.Position + new Vector2(template.Size.X + 12f, 0f);

        button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => Cycle(screen)));
        if (screen.GetTree() is { } tree)
        {
            tree.CreateTimer(0.0).Timeout += () =>
            {
                try
                {
                    if (!GodotObject.IsInstanceValid(button)) return;
                    button.GetNodeOrNull<TextureRect>("%Image")?.Hide(); // no up/down arrow: this is a cycle, not a sort
                    if (template.GetParent() is { } && GodotObject.IsInstanceValid(template))
                    {
                        // same hue as the other sorters (the character's card frame colour)
                        if (player.Character.CardPool.FrameMaterial is ShaderMaterial mat) button.SetHue(mat);
                        button.FocusNeighborLeft = template.GetPath();
                        template.FocusNeighborRight = button.GetPath();
                        button.FocusNeighborRight = button.GetPath();
                        button.FocusNeighborTop = button.GetPath();
                        button.FocusNeighborBottom = template.FocusNeighborBottom;
                    }

                    UpdateLabel(screen, state);
                }
                catch (Exception e)
                {
                    MainFile.Logger.Warn($"[DeckSwordFilter] setup: {e.Message}");
                }
            };
        }
    }

    private static void Cycle(NDeckViewScreen screen)
    {
        try
        {
            if (!States.TryGetValue(screen, out var state)) return;
            var modes = AvailableModes(PileRef(screen).Cards);
            var i = modes.IndexOf(state.Current);
            state.Current = modes[(i + 1) % modes.Count];
            UpdateLabel(screen, state);
            DisplayCardsMethod?.Invoke(screen, null);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[DeckSwordFilter] cycle: {e.Message}");
        }
    }

    private static void UpdateLabel(NDeckViewScreen screen, State state)
    {
        if (state.Button is not { } button || !GodotObject.IsInstanceValid(button)) return;
        var cards = PileRef(screen).Cards;
        var name = state.Current.Kind switch
        {
            Kind.All => Text("ALL"),
            Kind.Grouped => Text("GROUPED"),
            Kind.Sword => SwordLore.NameText(state.Current.Sword),
            Kind.Shared => Text("SHARED"),
            Kind.Union => Text("UNION"),
            _ => Text("CURSE"),
        };
        string label;
        if (state.Current.Kind is Kind.All or Kind.Grouped)
        {
            label = Format("LABEL", ("Name", name));
        }
        else
        {
            var count = cards.Count(c => Matches(c, state.Current));
            label = Format("LABEL_COUNT", ("Name", name), ("Count", count));
        }

        button.SetLabel(label);
    }

    private static string Text(string part)
    {
        try { return new LocString(Table, $"{Prefix}.{part}").GetFormattedText(); }
        catch (Exception) { return part; }
    }

    private static string Format(string part, params (string Name, object Value)[] vars)
    {
        try
        {
            var loc = new LocString(Table, $"{Prefix}.{part}");
            foreach (var (n, v) in vars)
            {
                if (v is int i) loc.Add(n, (decimal)i);
                else loc.Add(n, v.ToString() ?? "");
            }

            return loc.GetFormattedText();
        }
        catch (Exception)
        {
            return part;
        }
    }

    // ------------------------------------------------------------------ groups

    /// <summary>The group a card is shown in. Pairs use their leader (막야 → 간장·막야).</summary>
    private static Mode GroupOf(CardModel card)
    {
        if (card.Type == CardType.Curse) return new Mode(Kind.Curse);
        if (card is UnionCard) return new Mode(Kind.Union);
        if (card is MagicSwordCard { Sword: { } sword }) return new Mode(Kind.Sword, SwordRegistry.GroupLeader(sword));
        return new Mode(Kind.Shared);
    }

    private static bool Matches(CardModel card, Mode mode) => mode.Kind switch
    {
        Kind.All or Kind.Grouped => true,
        _ => GroupOf(card) == mode,
    };

    /// <summary>Sort key of a group in 묶어 보기: swords in registry order, then 조합, 공용, 저주.</summary>
    private static int GroupOrder(Mode mode) => mode.Kind switch
    {
        Kind.Sword => (int)mode.Sword,
        Kind.Union => 100,
        Kind.Shared => 101,
        _ => 102,
    };

    private static List<Mode> AvailableModes(IReadOnlyList<CardModel> cards)
    {
        var present = cards.Select(GroupOf).Distinct().OrderBy(GroupOrder).ToList();
        var modes = new List<Mode> { new(Kind.All), new(Kind.Grouped) };
        modes.AddRange(present);
        return modes;
    }

    // ------------------------------------------------------------------ display

    /// <summary>DisplayCards prefix: false = handled here (묶어 보기), true = let the game display _cards.</summary>
    private static bool Apply(NDeckViewScreen screen)
    {
        var pile = PileRef(screen);
        if (pile == null) return true;
        var all = pile.Cards.ToList();
        if (!States.TryGetValue(screen, out var state)) return true;

        // a group that emptied (card removed) falls back to the whole deck
        if (state.Current.Kind is not (Kind.All or Kind.Grouped) && !all.Any(c => Matches(c, state.Current)))
        {
            state.Current = new Mode(Kind.All);
        }

        UpdateLabel(screen, state);

        switch (state.Current.Kind)
        {
            case Kind.All:
                CardsRef(screen) = all;
                return true;
            case Kind.Grouped:
            {
                CardsRef(screen) = all;
                var grid = GridRef(screen);
                grid.YOffset = 100;
                grid.SetCards(Grouped(all, grid, PriorityRef(screen)), pile.Type, [SortingOrders.Ascending]);
                return false;
            }
            default:
                CardsRef(screen) = all.Where(c => Matches(c, state.Current)).ToList();
                return true;
        }
    }

    /// <summary>Whole deck ordered by group, and inside a group by the vanilla sorters the player picked.</summary>
    private static List<CardModel> Grouped(List<CardModel> all, NCardGrid grid, List<SortingOrders>? priority)
    {
        var obtained = all.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);
        var algos = SortingAlgorithms?.GetValue(grid) as Dictionary<SortingOrders, Func<CardModel, CardModel, int>>;
        var order = priority ?? [];

        int Inner(CardModel x, CardModel y)
        {
            if (order.Count > 0 && order[0] == SortingOrders.Descending) return obtained[y].CompareTo(obtained[x]);
            if (order.Count == 0 || order[0] == SortingOrders.Ascending || algos == null)
                return obtained[x].CompareTo(obtained[y]);
            foreach (var o in order)
            {
                if (o is SortingOrders.Ascending or SortingOrders.Descending) break;
                if (!algos.TryGetValue(o, out var f)) continue;
                var r = f(x, y);
                if (r != 0) return r;
            }

            return obtained[x].CompareTo(obtained[y]);
        }

        var list = all.ToList();
        list.Sort((x, y) =>
        {
            var g = GroupOrder(GroupOf(x)).CompareTo(GroupOrder(GroupOf(y)));
            return g != 0 ? g : Inner(x, y);
        });
        return list;
    }
}
