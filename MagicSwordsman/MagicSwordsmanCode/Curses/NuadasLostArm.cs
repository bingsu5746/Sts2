using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Curses;

/// <summary>
/// 누아다의 잃은 팔 — Claíomh Solais forge-failure curse (content doc §3.1). Unplayable.
/// At the end of your turn, if this is in your hand, draw 1 fewer card next turn (<see cref="NuadasLostArmPower"/>).
/// Turn-end-in-hand pattern: game curse Decay (HasTurnEndInHandEffect / OnTurnEndInHand).
/// Source line: 「누아다 본인은 전투에서 팔을 잃어 왕위를 내려놓는다」.
/// </summary>
public sealed class NuadasLostArm : SwordCurseCard
{
    public override SwordId Sword => SwordId.ClaiomhSolais;

    public NuadasLostArm()
    {
        WithPower<NuadasLostArmPower>(1);
    }

    public override bool HasTurnEndInHandEffect => true;

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        await PowerCmd.Apply<NuadasLostArmPower>(choiceContext, Owner.Creature,
            DynamicVars["NuadasLostArmPower"].BaseValue, Owner.Creature, this);
    }
}
