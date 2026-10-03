using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Caladbolg;

// 칼라드볼그 — 세 언덕 · 광역 · 10장 (content doc §2.10). Basic 2 · Common 3 · Uncommon 3 · Rare 2.

/// <summary>단단한 칼날 (Basic): 6 damage (+1/L). Source: 「'단단한 틈새' 또는 '단단한 칼날'」.</summary>
public sealed class CaladbolgHardBlade : CaladbolgCard
{
    public CaladbolgHardBlade() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithDamage(6);
        WithDamagePerLevel(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}
