using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

// 간장·막야 【짝】 카드 2장 더 (user request 2026-10-15, #35): Ganjiang 1 (Common) · Moye 1 (Uncommon).

/// <summary>
/// 거북등 무늬 — Ganjiang, Attack, Common, cost 1, one enemy. Deal 7 (+1/L) damage. 【짝】 gain 5 (+1/L) Block.
/// Lore (Wuyue Chunqiu, high confidence): "the yang sword bore a tortoise-shell pattern, the yin sword a flowing
/// pattern" (陽作龜文，陰作漫理) — the counterpart of 물결무늬 (MoyeRipplePattern): the male blade's shell pattern
/// becomes a shell of Block when it follows Moye.
/// </summary>
public sealed class GanjiangTortoiseShell : GanjiangMoyeCard
{
    public GanjiangTortoiseShell() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(7);
        WithDamagePerLevel(1);
        WithBlock(5);
        WithBlockPerLevel(1);
    }

    protected override SwordId Side => SwordId.Ganjiang;

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        if (pair) await CommonActions.CardBlock(this, cardPlay);
    }
}

/// <summary>
/// 돌 위의 소나무 — Moye, Skill, Uncommon, cost 1, self. Choose a Ganjiang card (twin cards included) in your draw
/// pile and put it into your hand. 【짝】 it costs 0 this turn. No Ganjiang card in the draw pile: draw 1 card instead.
/// A tutor for the other half of the pair, so the Moye -> Ganjiang alternation keeps going.
/// Lore (Soushen ji, high confidence): before going to the king, Ganjiang told his wife "Look at the southern
/// mountain from the door: a pine grows on a stone, the sword is in its back." Their son Chi later found the hidden
/// male sword in the pine pillar standing on its stone base.
/// </summary>
public sealed class MoyePineOnStone : GanjiangMoyeCard
{
    public MoyePineOnStone() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithCards(1);
    }

    protected override SwordId Side => SwordId.Moye;

    private static bool IsGanjiangCard(CardModel card) => card is MagicSwordCard { Sword: SwordId.Ganjiang };

    protected override async Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair)
    {
        var options = PileType.Draw.GetPile(Owner).Cards.Where(IsGanjiangCard).ToList();
        if (options.Count == 0)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
            return;
        }

        var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, options, Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();
        if (chosen == null) return;
        await CardPileCmd.Add(chosen, PileType.Hand);
        if (pair) chosen.EnergyCost.SetThisTurn(0, reduceOnly: true);
    }
}
