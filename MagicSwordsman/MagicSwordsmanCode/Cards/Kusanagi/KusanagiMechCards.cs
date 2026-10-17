using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Kusanagi;

// 쿠사나기 메커니즘 카드 4장 (mech2, 2026-10-17, user request "현재의 검들의 메커니즘을 제대로 반영하는 카드들"):
// a payoff that cares WHICH sword is inherited (its level), a card that changes the inheritance without spending a
// switch-in, a payoff for debuff purification (until now only exhausting Status/Curse cards had payoffs), and a
// finisher for the last switch-in.

/// <summary>Read-only helpers about Kusanagi's inheritance and switch-in limit (pure: also used for previews).</summary>
public static class KusanagiInherit
{
    /// <summary>Switch-ins into Kusanagi left this combat (2 per combat, 삼신기 adds 1).</summary>
    public static int RemainingSwitchIns(Player player) =>
        SwordCombat.Get(player) is { } state
            ? Math.Max(0, KusanagiBehavior.MaxSwitchInsPerCombat - state.KusanagiSwitchIns)
            : KusanagiBehavior.MaxSwitchInsPerCombat;

    /// <summary>Switch-ins left after a Kusanagi card in hand is played now (it switches first).</summary>
    public static int RemainingAfterPlay(Player player)
    {
        var remaining = RemainingSwitchIns(player);
        var current = SwordCombat.CurrentSword(player);
        if (current == null || current == SwordId.Kusanagi) return remaining;
        return SwordCombat.CanSwitchTo(player, SwordId.Kusanagi, SwitchReason.CardPlayed) ? Math.Max(0, remaining - 1) : remaining;
    }

    /// <summary>
    /// The sword Kusanagi inherits right now (Kusanagi current), or would inherit if a Kusanagi card were played now
    /// (it switches first, so the sword current now becomes the previous one). Null = nothing inherited.
    /// </summary>
    public static SwordId? InheritedFor(Player player)
    {
        var state = SwordCombat.Get(player);
        if (state == null) return null;
        SwordId? sword;
        if (state.Current == SwordId.Kusanagi)
            sword = SwordRegistry.Get(SwordId.Kusanagi).GetInheritedSword(SwordCombat.ContextFor(player, SwordId.Kusanagi));
        else if (state.Current is { } cur && SwordCombat.CanSwitchTo(player, SwordId.Kusanagi, SwitchReason.CardPlayed))
            sword = cur;
        else
            sword = null;
        return sword is { } s && s != SwordId.Kusanagi && SwordRegistry.Get(s).CanBeInherited ? s : null;
    }
}

/// <summary>
/// 야마토히메의 하사 — Kusanagi, Attack, Common, cost 1, one enemy. Deal 5 (+1/L) damage, +2 per upgrade level of the
/// sword Kusanagi inherits (nothing inherited: +0). The inherited sword's level counts (temporary combat bonuses
/// included), so bridging from a high-level sword pays.
/// Lore (Kojiki, high confidence): Yamatohime, priestess of Ise, gave her nephew Yamato Takeru the sword and a bag to
/// open in an emergency before his eastern campaign.
/// </summary>
public sealed class KusanagiYamatohime : KusanagiCard
{
    public const int PerLevel = 2;

    public KusanagiYamatohime() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(5);
        WithDamagePerLevel(1);
        WithVar("PerLevel", PerLevel);
        WithCalculatedVar("HeirBonus", 0, static (card, _) => card is KusanagiYamatohime k ? k.HeirBonus : 0);
        WithTip(SwordMechTips.Inherit);
    }

    private int HeirBonus
    {
        get
        {
            if (!IsMutable || Owner?.PlayerCombatState == null || LevelPreviewPatches.NeutralCombatInputs) return 0;
            return KusanagiInherit.InheritedFor(Owner) is { } s ? PerLevel * SwordCombat.LevelOf(Owner, s) : 0;
        }
    }

    protected override decimal BonusDamage(Creature? target) => HeirBonus;

    protected override bool ShouldGlowGoldInternal => HeirBonus > 0;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}

/// <summary>
/// 도교의 도둑질 — Kusanagi, Skill, Uncommon, cost 1 (0 from level 3), self. If Kusanagi is the current sword, choose
/// another sword in Mangeomchong: Kusanagi now inherits that sword's effect (half strength, as usual) instead — no
/// switch-in is used. Draw 1 card. Implemented by setting the combat state's previous sword (what Kusanagi inherits
/// from, KusanagiBehavior.GetInheritedSword); 삼신기's lingering source is updated too.
/// Lore (Nihon Shoki, Tenji 7 = 668, medium-high confidence): the monk Dōgyō stole the Kusanagi sword and tried to
/// flee to Silla; the sword was recovered.
/// </summary>
public sealed class KusanagiDogyoTheft : KusanagiCard
{
    public KusanagiDogyoTheft() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithCards(1);
        WithTip(SwordMechTips.Inherit);
    }

    protected override int? CostAtLevel(int level) => level >= 3 ? 0 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (SwordCombat.Get(Owner) is { Current: SwordId.Kusanagi } state && Owner.GetRelic<Mangeomchong>() is { } relic)
        {
            var options = relic.OwnedSwords
                .Where(s => s != SwordId.Kusanagi && s != state.Previous && SwordRegistry.Get(s).CanBeInherited)
                .ToList();
            if (options.Count > 0)
            {
                var chosen = await SwordAcquisition.PickSword(Owner, options, choiceContext, canSkip: true,
                    new LocString("card_selection", "MAGICSWORDSMAN-CHOOSE_SWORD_TO_INHERIT"));
                if (chosen is { } sword && state.Current == SwordId.Kusanagi)
                {
                    state.Previous = sword;
                    Owner.Creature.GetPower<KusanagiRegaliaPower>()?.SetInheritSource(sword);
                    await SwordCombat.EnsureCurrentSwordPower(Owner, choiceContext); // refresh the "현재 검" text
                    MainFile.Logger.Info($"[Kusanagi] Dōgyō's theft: now inherits {sword}");
                }
            }
        }

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}

/// <summary>
/// 미소기 — Kusanagi, Power, Uncommon, cost 1, self. This combat, whenever purification reduces or removes one of your
/// debuffs, draw 1 card and gain 3 (+⌊L/2⌋) Block (<see cref="KusanagiMisogiPower"/>, triggered from KusanagiPurify).
/// Lore (Kojiki, high confidence): back from Yomi, Izanagi purified himself in a river (misogi); Susanoo — who later
/// found this sword in Orochi's tail — was born when he washed his nose.
/// </summary>
public sealed class KusanagiMisogi : KusanagiCard
{
    public KusanagiMisogi() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithCalculatedVar("MisogiBlock", 3, 1, static (card, _) => card is MagicSwordCard m ? m.SwordLevel / 2 : 0);
        WithVar("Draw", KusanagiMisogiPower.Draw);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<KusanagiMisogiPower>()));
        WithTip(SwordMechTips.Purify);
    }

    private int MisogiBlock => DynamicVars["MisogiBlockBase"].IntValue + SwordLevel / 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<KusanagiMisogiPower>(choiceContext, Owner.Creature, MisogiBlock, Owner.Creature, this);
    }
}

/// <summary>
/// 단노우라의 바다 — Kusanagi, Attack, Uncommon, cost 2, one enemy. Deal 10 (+2/L) damage. If, after this card,
/// Kusanagi has no switch-ins left this combat (this was its last one, or they were already used up), deal it again.
/// A finisher for the switch-in limit: Kusanagi goes back into Mangeomchong when it leaves, so its last turn hits hard.
/// Lore (Heike monogatari, high confidence): at Dan-no-ura (1185) the child emperor Antoku's grandmother leapt into
/// the sea with him, the jewel and the sword; the sword was lost in the waves.
/// </summary>
public sealed class KusanagiDannoura : KusanagiCard
{
    public KusanagiDannoura() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(10);
        WithDamagePerLevel(2);
        WithCalculatedVar("Remaining", 0, static (card, _) =>
            card is KusanagiDannoura { IsMutable: true, Owner: { PlayerCombatState: not null } owner }
                ? KusanagiInherit.RemainingSwitchIns(owner)
                : 0);
        WithTip(SwordMechTips.KusanagiLimit);
    }

    protected override bool ShouldGlowGoldInternal =>
        IsMutable && Owner?.PlayerCombatState != null && KusanagiInherit.RemainingAfterPlay(Owner) == 0;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var lastSwitch = KusanagiInherit.RemainingSwitchIns(Owner) == 0;
        await CommonActions.CardAttack(this, cardPlay, lastSwitch ? 2 : 1)
            .WithHitFx("vfx/vfx_giant_horizontal_slash")
            .Execute(choiceContext);
    }
}
