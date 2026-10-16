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

/// <summary>
/// 황금 자루 (Common): Retain. 8 damage (+1/L). Fatal: if this kills an enemy, gain 10 gold.
/// Reworked 2026-10-16 (near-clone pass): it was a plain Retain attack, the starter 불꽃 칼날 with Retain. Now the
/// golden hilt pays out (fatal check and gold: game HandOfGreed — ShouldOwnerDeathTriggerFatal, WasTargetKilled,
/// PlayerCmd.GainGold). Lore (high confidence, Hervarar saga): Tyrfing was forged by the dwarves Dvalin and Durin
/// for King Svafrlami with a hilt of gold.
/// </summary>
public sealed class TyrfingGoldenHilt : TyrfingCard
{
    public const int KillGold = 10;

    public TyrfingGoldenHilt() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(8);
        WithDamagePerLevel(1);
        WithVar("Gold", KillGold);
        WithKeywords(CardKeyword.Retain);
    }

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var fatal = cardPlay.Target != null && cardPlay.Target.Powers.All(p => p.ShouldOwnerDeathTriggerFatal());
        var attack = await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        if (fatal && attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
            await PlayerCmd.GainGold(KillGold, Owner);
    }
}
