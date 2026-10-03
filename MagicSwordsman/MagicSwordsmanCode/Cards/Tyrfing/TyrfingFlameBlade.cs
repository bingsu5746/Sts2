using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Tyrfing;

// 티르빙 — 방어도 무시 · 마무리 · 10장 (content doc §2.4). Basic 2 · Common 3 · Uncommon 3 · Rare 2.
// The current-sword effect (attacks ignore block) is in TyrfingBehavior; the run curses in TyrfingCard.

/// <summary>불꽃 칼날 (Basic): 7 damage (+1/L).</summary>
public sealed class TyrfingFlameBlade : TyrfingCard
{
    public TyrfingFlameBlade() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithDamage(7);
        WithDamagePerLevel(1);
    }

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}
