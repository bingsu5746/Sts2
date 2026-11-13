"""Builds Mumyeong/localization/<kor|eng>/*.json from the data in this folder.

Usage (from Mumyeong/):  python3 tools/build_loc.py
Card keys: MUMYEONG-<CLASS_NAME_IN_UPPER_SNAKE>. Run it again after editing any text in tools/.
"""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from loc_cards import CARDS  # noqa: E402
from loc_scenes import FIGS, SCENES  # noqa: E402
from loc_talk import TALK  # noqa: E402

P = "MUMYEONG-"
ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Mumyeong", "localization")


def snake(name):
    return re.sub(r"(?<!^)(?=[A-Z])", "_", name).upper()


# ---------------------------------------------------------------------------------------------- powers
# class -> (kor title, kor desc, eng title, eng desc)
POWERS = {
    "ApplausePower": ("객석의 갈채", "내 턴이 시작될 때마다 [gold]방어도[/gold]를 {Amount} 얻습니다.",
                      "Applause", "At the start of your turn, gain {Amount} [gold]Block[/gold]."),
    # Numbers in power / relic texts are fixed: keep them equal to the constants in the code (SoulCore, FigureRelics).
    "GrudgePower": ("한", "[gold]한 계약[/gold]: 위인의 카드가 아닌 카드를 쓸 때마다 쌓입니다. 5에 이르면 체력을 3 잃고, 한은 0이 됩니다.",
                    "Grudge", "[gold]Grudge Contract[/gold]: grows whenever you play a card that is not your figure's. At 5, lose 3 HP and it resets to 0."),
    "GuanyuAwePower": ("위진화하", "내 턴이 시작될 때마다 [gold]활력[/gold]을 {Amount} 얻습니다.",
                       "Awe over the Land", "At the start of your turn, gain {Amount} [gold]Vigor[/gold]."),
    "GuanyuWarGodPower": ("무신", "공격 카드를 쓸 때마다 [gold]활력[/gold]을 {Amount} 얻습니다.",
                          "God of War", "Whenever you play an Attack, gain {Amount} [gold]Vigor[/gold]."),
    "GuanyuRighteousPower": ("의절천추", "스킬 카드를 쓸 때마다 [gold]활력[/gold]을 {Amount} 얻습니다.",
                             "Righteous Through the Ages", "Whenever you play a Skill, gain {Amount} [gold]Vigor[/gold]."),
    "JoanFaithPower": ("신념", "내 턴이 끝날 때마다 [gold]방어도[/gold]를 {Amount} 얻습니다. 신념은 줄어들지 않습니다.",
                       "Faith", "At the end of your turn, gain {Amount} [gold]Block[/gold]. Faith does not decrease."),
    "JoanBannerPower": ("흔들리지 않는 깃발", "내 턴이 시작될 때마다 [gold]신념[/gold]을 {Amount} 얻습니다.",
                        "Unwavering Banner", "At the start of your turn, gain {Amount} [gold]Faith[/gold]."),
    "JoanPyrePower": ("화형대", "체력을 잃을 때마다 [gold]신념[/gold]을 {Amount} 얻습니다.",
                      "The Pyre", "Whenever you lose HP, gain {Amount} [gold]Faith[/gold]."),
    "JoanJudgmentPower": ("심판", "내 턴이 끝날 때 [gold]신념[/gold]×{Amount}만큼 모든 적에게 피해를 줍니다.",
                          "Judgment", "At the end of your turn, deal [gold]Faith[/gold] ×{Amount} damage to ALL enemies."),
    "LeonidasPhalanxPower": ("방진", "[gold]방어도[/gold]가 내 턴이 시작될 때 사라지지 않습니다.",
                             "Phalanx", "Your [gold]Block[/gold] is not removed at the start of your turn."),
    "LeonidasCounterPower": ("반격", "적에게 공격받을 때마다 그 적에게 피해를 {Amount} 줍니다.",
                             "Counter", "Whenever an enemy attacks you, deal {Amount} damage back."),
    "LeonidasMolonLabePower": ("와서 가져가라", "내 턴이 끝날 때 [gold]방어도[/gold]의 절반×{Amount}만큼 모든 적에게 피해를 줍니다.",
                               "Come and Take Them", "At the end of your turn, deal half your [gold]Block[/gold] ×{Amount} damage to ALL enemies."),
    "LeonidasThermopylaePower": ("테르모필레", "스킬 카드를 쓸 때마다 무작위 적에게 피해를 {Amount} 줍니다.",
                                 "Thermopylae", "Whenever you play a Skill, deal {Amount} damage to a random enemy."),
    "GenghisHordePower": ("기마 군단", "내 턴이 시작될 때마다 모든 적에게 피해를 {Amount} 주고, 이 수치가 1 늘어납니다.",
                          "Horde", "At the start of your turn, deal {Amount} damage to ALL enemies, then increase this by 1."),
    "GenghisConquestPower": ("정복", "적이 쓰러질 때마다 에너지를 {Amount} 얻고 카드를 {Amount}장 뽑습니다.",
                             "Conquest", "Whenever an enemy dies, gain {Amount} energy and draw {Amount} card(s)."),
    "GenghisEmpirePower": ("대몽골", "공격 카드를 쓸 때마다 모든 적에게 피해를 {Amount} 줍니다.",
                           "The Great Mongol Nation", "Whenever you play an Attack, deal {Amount} damage to ALL enemies."),
    "YiCannonPower": ("포격", "내 턴이 끝날 때마다 모든 적에게 피해를 {Amount} 줍니다. 포격은 줄어들지 않습니다.",
                      "Bombard", "At the end of your turn, deal {Amount} damage to ALL enemies. Bombard does not decrease."),
    "YiTurtleShipPower": ("거북선", "내 턴이 끝날 때마다 [gold]방어도[/gold]를 {Amount} 얻습니다.",
                          "Turtle Ship", "At the end of your turn, gain {Amount} [gold]Block[/gold]."),
    "YiCraneWingPower": ("학익진", "내 턴이 끝날 때 남은 에너지 1당 모든 적에게 피해를 {Amount} 줍니다.",
                         "Crane Wing Formation", "At the end of your turn, deal {Amount} damage to ALL enemies for each unspent energy."),
    "YiCommanderPower": ("통제사", "내 턴이 시작될 때마다 [gold]포격[/gold]을 {Amount} 얻습니다.",
                         "Naval Commander", "At the start of your turn, gain {Amount} [gold]Bombard[/gold]."),
    "NapoleonArtilleryPower": ("포병대", "내 턴이 시작될 때마다 무작위 적에게 피해를 {Amount} 줍니다.",
                               "Artillery Corps", "At the start of your turn, deal {Amount} damage to a random enemy."),
    "NapoleonBlitzPower": ("속전속결", "한 턴에 카드를 4장 쓸 때마다 에너지를 {Amount} 얻습니다.",
                           "Lightning Campaign", "Every 4th card you play in a turn gives {Amount} energy."),
    "NapoleonCoronationPower": ("대관식", "최대 에너지가 {Amount} 늘어납니다.",
                                "Coronation", "Your max energy is increased by {Amount}."),
}

# ---------------------------------------------------------------------------------------------- relics
RELICS = {
    "SoulCore": ("코어",
                 "위인의 카드를 쓸 때마다 숙련이 1 오릅니다. 카드 보상과 상점에는 공용 카드와 그 위인의 카드만 나옵니다.{Status}",
                 "한 번 멈춘 심장 대신 뛰는 것.",
                 "Soul Core",
                 "Playing your figure's cards raises Mastery by 1. Card rewards and shops only offer shared cards and your figure's cards.{Status}",
                 "It beats where a stopped heart used to."),
    "GuanyuGreenDragonRelic": ("청룡언월도", "내 턴이 시작될 때마다 [gold]활력[/gold]을 3 얻습니다.", "푸른 용이 새겨진 언월도. 무겁다.",
                               "Green Dragon Crescent Blade", "At the start of your turn, gain 3 [gold]Vigor[/gold].", "A crescent blade carved with a green dragon. Heavy."),
    "JoanBannerRelic": ("오를레앙의 깃발", "내 턴이 시작될 때마다 [gold]신념[/gold]을 2 얻습니다.", "내려놓을 이유가 없었다.",
                        "Banner of Orléans", "At the start of your turn, gain 2 [gold]Faith[/gold].", "There was never a reason to lower it."),
    "LeonidasShieldRelic": ("스파르타의 방패", "전투를 시작할 때 [gold]방진[/gold]과 [gold]반격[/gold] 3을 얻습니다.", "방패를 들고 오라. 아니면 그 위에 실려 오라.",
                            "Spartan Shield", "At the start of each combat, gain [gold]Phalanx[/gold] and 3 [gold]Counter[/gold].", "Come back with it, or on it."),
    "GenghisSealRelic": ("대칸의 인장", "적이 쓰러질 때마다 에너지를 1 얻고 카드를 1장 뽑습니다.", "초원 끝에서 끝까지 통하는 인장.",
                         "Seal of the Great Khan", "Whenever an enemy dies, gain 1 energy and draw 1 card.", "A seal honoured from one end of the steppe to the other."),
    "YiTurtleShipRelic": ("조선 수군", "내 턴이 끝날 때마다 모든 적에게 피해를 5 주고 [gold]방어도[/gold]를 5 얻습니다.", "배는 적었지만 물러서지 않았다.",
                          "Joseon Navy", "At the end of your turn, deal 5 damage to ALL enemies and gain 5 [gold]Block[/gold].", "Few ships, and still no retreat."),
    "NapoleonCrownRelic": ("황제의 관", "최대 에너지가 1 늘어납니다.", "스스로 머리에 얹은 관.",
                           "Emperor's Crown", "Gain 1 max energy.", "A crown placed by one's own hands."),
}

# ---------------------------------------------------------------------------------------------- figures
FIGURES = {
    "GUANYU": {
        "NAME": ("관우", "Guan Yu"),
        "SHADOW": ("의리의 무신", "War God of Loyalty"),
        "INTRO": ("후한 말, 유비를 따라 싸운 장수.\n훗날 관제(關帝)로 모셔졌다.", "A general of the late Han who fought beside Liu Bei.\nLater worshipped as Guan Di."),
        "LAST_DEED": ("맥성에서 포위되자 성을 나와 길을 뚫으려 했고, 그 길에서 매복에 걸려 사로잡혔다.",
                      "Surrounded at Maicheng, he left the city to break through, and was ambushed and captured on the road."),
        "MEMORY_1": ("형님이라 부른 사람이 있었다. 그 이름 하나 때문에 먼 길을 돌아갔다.",
                     "There was a man I called elder brother. For that one name I took the long road back."),
        "MEMORY_2": ("칼은 무거웠고, 사람들은 나를 두려워하면서도 우러러보았다.",
                     "The blade was heavy. People feared me, and still looked up to me."),
        "MEMORY_3": ("그날 밤 나는 길을 뚫으려 했다. 이번에는 네 몸으로 그 길을 끝까지 간다.",
                     "That night I tried to break through. This time, in your body, I walk that road to its end."),
        "TASK": ("공격 카드 한 번의 타격으로 피해 30 이상", "Deal 30+ damage with a single hit of an Attack card"),
    },
    "JOAN": {
        "NAME": ("잔 다르크", "Joan of Arc"),
        "SHADOW": ("깃발을 든 소녀", "The Girl with the Banner"),
        "INTRO": ("백년전쟁 때의 프랑스 시골 소녀.\n오를레앙 포위를 풀고, 샤를 7세의 대관을 이끌었다.", "A French country girl of the Hundred Years' War.\nShe lifted the siege of Orléans and led Charles VII to his crowning."),
        "LAST_DEED": ("화형이 두려워 서명했던 철회서를 며칠 뒤 스스로 뒤집었다. 1431년 5월 30일, 루앙에서 화형당했다.",
                      "She took back the abjuration she had signed out of fear of the fire, days later. On 30 May 1431 she was burned at Rouen."),
        "MEMORY_1": ("아버지의 정원에서 처음 목소리를 들었다. 아무도 믿지 않았지만 나는 들었다.",
                     "I first heard the voices in my father's garden. No one believed me, but I heard them."),
        "MEMORY_2": ("깃발을 들고 성으로 들어갔다. 포위가 풀렸다.",
                     "I carried the banner into the city. The siege was lifted."),
        "MEMORY_3": ("불길 앞에서 나는 나의 말을 지켰다. 이번에도 거두지 않는다.",
                     "Before the fire I kept my word. I will not take it back this time either."),
        "TASK": ("턴 종료 시 [gold]신념[/gold] 10 이상", "End a turn with 10+ [gold]Faith[/gold]"),
    },
    "LEONIDAS": {
        "NAME": ("레오니다스", "Leonidas"),
        "SHADOW": ("협곡의 왕", "King of the Pass"),
        "INTRO": ("스파르타의 왕.\n기원전 480년, 테르모필레에서 페르시아 대군을 막아섰다.", "A king of Sparta.\nIn 480 BC he stood against the Persian host at Thermopylae."),
        "LAST_DEED": ("페르시아군이 산길로 뒤를 돌아 나오자 연합군 대부분을 돌려보내고, 남은 병사들과 함께 테르모필레에 남아 싸웠다(기원전 480년).",
                      "When the Persians came round behind by the mountain path, he sent most of the allies away and stayed at Thermopylae with those who remained (480 BC)."),
        "MEMORY_1": ("방패는 나를 위한 것이 아니었다. 옆에 선 사람을 위한 것이었다.",
                     "The shield was never for me. It was for the man standing beside me."),
        "MEMORY_2": ("좁은 고개에서는 수가 아니라 대열이 싸운다.",
                     "In a narrow pass, it is the line that fights, not the numbers."),
        "MEMORY_3": ("돌아가지 않았다. 지금도 돌아가지 않는다.",
                     "I did not turn back. I do not turn back now."),
        "TASK": ("한 전투에서 [gold]방어도[/gold]로 막은 피해 합계 100 이상", "Block a total of 100+ damage in one combat"),
    },
    "GENGHIS": {
        "NAME": ("칭기즈 칸", "Genghis Khan"),
        "SHADOW": ("초원의 대칸", "Great Khan of the Steppe"),
        "INTRO": ("본명 테무진. 흩어진 몽골 부족을 하나로 묶고,\n1206년 칭기즈 칸으로 추대되었다.", "Born Temüjin. He united the scattered Mongol tribes\nand was proclaimed Genghis Khan in 1206."),
        "LAST_DEED": ("서하 원정 중이던 1227년에 세상을 떠났다. 원정은 아직 끝나지 않았다.",
                      "He died in 1227, during the campaign against Western Xia. The campaign was not yet over."),
        "MEMORY_1": ("초원에서 모든 것을 잃은 아이가 있었다. 그 아이는 다시는 잃지 않기로 했다.",
                     "There was a child on the steppe who lost everything. That child decided never to lose again."),
        "MEMORY_2": ("흩어진 부족을 하나로 묶었다. 말은 쉬지 않았다.",
                     "I bound the scattered tribes into one. The horses never rested."),
        "MEMORY_3": ("원정은 끝나지 않았다. 네 발로 그 끝을 밟아라.",
                     "The campaign is not finished. Tread its end with your own feet."),
        "TASK": ("한 전투에서 적 2명 처치", "Kill 2 enemies in one combat"),
    },
    "YISUNSIN": {
        "NAME": ("이순신", "Yi Sun-sin"),
        "SHADOW": ("바다를 지킨 장군", "The Admiral Who Held the Sea"),
        "INTRO": ("조선의 수군 장수.\n임진왜란과 정유재란 때 바다에서 적을 막았다.", "A naval commander of Joseon.\nHe held the sea against the Japanese invasions of 1592–1598."),
        "LAST_DEED": ("노량(1598년)에서 총탄에 맞아 쓰러지며 \"싸움이 급하니 내 죽음을 알리지 말라\"고 했다고 전한다.",
                      "At Noryang (1598), struck by a bullet, he is said to have ordered: \"The battle is at its height. Do not announce my death.\""),
        "MEMORY_1": ("바다를 지키는 것은 배가 아니라 사람이었다. 나는 싸우는 동안 일기를 썼다.",
                     "It was people, not ships, who guarded the sea. Through the war I kept a diary."),
        "MEMORY_2": ("적은 많았고 우리 배는 적었다. 그래도 물러서지 않았다.",
                     "The enemy was many and our ships were few. Still, we did not give way."),
        "MEMORY_3": ("싸움이 끝나기 전에 쓰러졌다. 이번에는 끝을 본다.",
                     "I fell before the battle ended. This time I will see the end."),
        "TASK": ("턴 종료 시 [gold]포격[/gold] 15 이상", "End a turn with 15+ [gold]Bombard[/gold]"),
    },
    "NAPOLEON": {
        "NAME": ("나폴레옹", "Napoleon"),
        "SHADOW": ("섬에서 온 황제", "The Emperor from the Island"),
        "INTRO": ("코르시카섬에서 태어난 포병 장교.\n프랑스의 황제가 되어 유럽을 휩쓴 전쟁을 이끌었다.", "An artillery officer born on Corsica.\nHe became Emperor of the French and led wars across Europe."),
        "LAST_DEED": ("워털루 패배 뒤 세인트헬레나섬으로 유배되어, 1821년 5월 5일 그곳에서 숨을 거두었다.",
                      "After Waterloo he was exiled to Saint Helena, and died there on 5 May 1821."),
        "MEMORY_1": ("작은 섬에서 태어났다. 세상은 언제나 섬보다 컸다.",
                     "I was born on a small island. The world was always larger than the island."),
        "MEMORY_2": ("대포를 어디에 둘지 정하는 것이, 전투를 정하는 것이었다.",
                     "Deciding where to place the guns was deciding the battle."),
        "MEMORY_3": ("섬에서 끝났다. 이번에는 섬을 떠난다.",
                     "It ended on an island. This time, I leave the island."),
        "TASK": ("한 턴에 카드 8장 사용", "Play 8 cards in one turn"),
    },
}

FIGURE_GENERIC = {
    "EMPTY": ("", ""),
    "STAGE.POSSESSION": ("빙의", "Possession"),
    "STAGE.SYNCHRONIZATION": ("동기화", "Synchronization"),
    "STAGE.FOCUS": ("집중", "Focus"),
    "STAGE.DESCENT": ("강림", "Descent"),
    "CONTRACT.NONE": ("계약 없음", "No contract"),
    "CONTRACT.RESONANCE": ("공명 계약", "Resonance Contract"),
    "CONTRACT.GRUDGE": ("한 계약", "Grudge Contract"),
    "CONTRACT_TEXT.RESONANCE": ("같은 길을 걸은 영혼.\n필요한 숙련: 동기화 {Sync}→{SyncResonance}, 집중 {Focus}→{FocusResonance}.",
                                "A soul that walked the same road.\nMastery needed: Synchronization {Sync}→{SyncResonance}, Focus {Focus}→{FocusResonance}."),
    "CONTRACT_TEXT.GRUDGE": ("결이 다른 영혼.\n전투 시작 시 [gold]힘[/gold] {Strength}.\n위인 카드가 아닌 카드를 쓰면 [gold]한[/gold]이 쌓이고, {Limit}에 이르면 체력을 {HpLoss} 잃는다.",
                             "A soul mixed with another grain.\nStart each combat with {Strength} [gold]Strength[/gold].\nCards that are not your figure's build [gold]Grudge[/gold]; at {Limit}, lose {HpLoss} HP."),
    "STATUS.NO_FIGURE": ("아직 아무도 응답하지 않았다.", "No one has answered yet."),
    "STATUS.HEAD": ("[gold]{Figure}[/gold] · {Contract} · {Stage}", "[gold]{Figure}[/gold] · {Contract} · {Stage}"),
    "STATUS.MASTERY": ("{Stage}까지: 숙련 {Mastery}/{Needed} ({Act}막부터)", "To {Stage}: Mastery {Mastery}/{Needed} (from act {Act})"),
    "STATUS.TASK": ("{Stage} 과제: {Task}", "{Stage} task: {Task}"),
    "STATUS.READY": ("[gold]{Stage}[/gold] 준비 완료 — 필름이 돌 때를 기다린다.", "[gold]{Stage}[/gold] is ready. The reel waits to turn."),
    "STATUS.COMPLETE": ("영혼과 완전히 하나가 되었다.", "You and the soul are one."),
}

THEATER_GENERIC = {
    "PROMPT.GAZE": ("[gold]시선[/gold] — 마지막 1초, 어디를 보는가.", "[gold]Gaze[/gold] — in the last second, where do you look?"),
    "PROMPT.ACTION": ("[gold]행동[/gold] — 멈춘 1초, 무엇을 하는가.", "[gold]Action[/gold] — in the frozen second, what do you do?"),
    "PROMPT.WORDS": ("[gold]유언[/gold] — 마지막 말.\n[aqua]공명이 셋으로 갈리면, 유언이 답한다.[/aqua]",
                     "[gold]Last words[/gold].\n[aqua]If resonance splits three ways, the last words decide.[/aqua]"),
    "REACTS": ("「{Shadow}」{Ga} 반응한다.", "\"{Shadow}\" stirs."),
    "REACTION": ("[gold]「{Shadow}」[/gold]{Ga} 한 걸음 다가온다.", "[gold]\"{Shadow}\"[/gold] steps closer."),
    "MISSING_LINE": ("...", "..."),
}

THEATER_EVENT = {
    "title": ("황천 영사실", "The Underworld Projection Room"),
    "pages.INITIAL.description": ("암전.\n\n[gold]1초 뒤, 당신은 죽는다.[/gold]", "Blackout.\n\n[gold]In one second, you will die.[/gold]"),
    "pages.INITIAL.options.ROLL.title": ("영사기 소리를 듣는다", "Listen to the projector"),
    "pages.INITIAL.options.ROLL.description": ("", ""),
    "pages.SHADOWS.description": ("어두운 객석에 그림자 셋이 앉아 있다.\n\n{Shadows}\n\n[aqua]한 그림자에게 세 번 모두 응답하면 공명 계약.[/aqua]",
                                  "Three shadows sit in the dark seats.\n\n{Shadows}\n\n[aqua]Answer one shadow all three times for a Resonance Contract.[/aqua]"),
    "pages.SHADOWS.options.START.title": ("상영을 시작한다", "Start the screening"),
    "pages.SHADOWS.options.START.description": ("당신의 마지막 1초가 상영된다.", "Your last second is screened."),
    "pages.SCENE.description": ("{Scene}", "{Scene}"),
    "pages.SCENE.options.FREEZE.title": ("1초가 멈춘다", "The second freezes"),
    "pages.SCENE.options.FREEZE.description": ("", ""),
    "pages.CUT.description": ("{Reaction}{Prompt}", "{Reaction}{Prompt}"),
    "pages.CUT.options.LINE.title": ("...", "..."),
    "pages.CUT.options.LINE.description": ("...", "..."),
    "pages.DESCENT.description": ("필름이 멈춘다.\n[gold]「{Shadow}」[/gold]{Ga} 스크린 속으로 걸어 들어온다.\n\n[gold]{Figure}[/gold]",
                                  "The film stops.\n[gold]\"{Shadow}\"[/gold] walks into the screen.\n\n[gold]{Figure}[/gold]"),
    "pages.DESCENT.options.WHO.title": ("얼굴을 본다", "Look at them"),
    "pages.DESCENT.options.WHO.description": ("", ""),
    "pages.WHO.description": ("[gold]{Figure}[/gold]\n\n{Intro}", "[gold]{Figure}[/gold]\n\n{Intro}"),
    "pages.WHO.options.DEED.title": ("마지막 1초를 본다", "Watch their last second"),
    "pages.WHO.options.DEED.description": ("", ""),
    "pages.DEED.description": ("[gold]{Figure}의 마지막 1초[/gold]\n{Deed}\n\n[gold]당신의 유언[/gold]\n{Words}",
                               "[gold]{Figure}'s last second[/gold]\n{Deed}\n\n[gold]Your last words[/gold]\n{Words}"),
    "pages.DEED.options.TERMS.title": ("계약을 듣는다", "Hear the contract"),
    "pages.DEED.options.TERMS.description": ("", ""),
    "pages.CONTRACT.description": ("공명 {Resonance}/3 — [gold]{Contract}[/gold]\n\n{ContractText}",
                                   "Resonance {Resonance}/3 — [gold]{Contract}[/gold]\n\n{ContractText}"),
    "pages.CONTRACT.options.TAKE.title": ("손을 잡는다", "Take their hand"),
    "pages.CONTRACT.options.TAKE.description": ("위인의 시작 카드 2장이 덱에 들어온다.", "Their 2 starting cards join your deck."),
    "pages.TEACH.description": ("심장이 다시 뛴다. 이제 그 박동은 둘의 것이다.\n\n[gold]{Figure}[/gold]{Ga} 기술 하나를 보여 준다.",
                                "Your heart beats again. Now the beat belongs to two.\n\n[gold]{Figure}[/gold] shows you one technique."),
    "pages.TEACH.options.LEARN.title": ("기술을 익힌다", "Learn it"),
    "pages.TEACH.options.LEARN.description": ("위인의 카드 3장 중 1장을 고른다.", "Choose 1 of 3 of their cards."),
    "pages.CONTRACTED.description": ("[gold]{Figure}[/gold] — {Contract}\n\n불이 켜진다. 객석은 비어 있다.",
                                     "[gold]{Figure}[/gold] — {Contract}\n\nThe lights come on. The seats are empty."),
    "pages.NO_CORE.description": ("필름이 끊겼다. 객석은 비어 있다.", "The film snapped. The seats are empty."),
    "pages.NO_CORE.options.LEAVE.title": ("떠난다", "Leave"),
    "pages.NO_CORE.options.LEAVE.description": ("", ""),
}

# character select: the system in five short pages (CharSelectInfo); numbers come from the code constants
INFO = {
    "BUTTON": ("시스템 설명", "How it works"),
    "PREV": ("이전", "Back"),
    "NEXT": ("다음", "Next"),
    "CLOSE": ("닫기", "Close"),
    "TITLE.0": ("무명", "Mumyeong"),
    "BODY.0": ("한 번 죽었다 깨어난 저승사자.\n\n위인 한 명의 [gold]코어[/gold]와 계약하고,\n그 영혼과 함께 싸우며 하나가 되어 간다.",
               "A reaper who died once and woke again.\n\nBound to the [gold]core[/gold] of one great figure,\nthey fight together and slowly become one."),
    "TITLE.1": ("황천 영사실", "The Projection Room"),
    "BODY.1": ("런을 시작하면 당신의 마지막 1초가 상영된다.\n\n[gold]시선 · 행동 · 유언[/gold] — 세 컷에서 하나씩 고른다.\n가장 많이 공명한 위인이 당신과 계약한다.\n\n[aqua]셋으로 갈리면 유언이 답한다.[/aqua]",
               "When the run begins, your last second is screened.\n\nPick one line in each of three cuts: [gold]gaze · action · last words[/gold].\nThe figure you resonate with most makes the contract.\n\n[aqua]If it splits three ways, the last words decide.[/aqua]"),
    "TITLE.2": ("계약", "The Contract"),
    "BODY.2": ("[gold]공명 계약[/gold] (3컷 모두 같은 위인)\n단계에 필요한 숙련이 줄어든다 ({Sync} / {Focus}).\n\n[gold]한 계약[/gold] (그 밖의 경우)\n전투를 힘 {Strength}로 시작한다.\n위인 카드가 아닌 카드를 쓰면 한이 쌓이고,\n한이 {Limit}에 이르면 체력을 {HpLoss} 잃는다.",
               "[gold]Resonance[/gold] (all three cuts, one figure)\nStages need less Mastery ({Sync} / {Focus}).\n\n[gold]Grudge[/gold] (otherwise)\nStart each combat with {Strength} Strength.\nCards that are not your figure's build Grudge; at {Limit} you lose {HpLoss} HP."),
    "TITLE.3": ("단계", "Stages"),
    "BODY.3": ("위인 카드를 쓸 때마다 [gold]숙련[/gold]이 쌓인다.\n\n[gold]동기화[/gold]  {SyncAct}막부터 · 숙련 {Sync} → 위인 카드 1장\n[gold]집중[/gold]  {FocusAct}막부터 · 숙련 {Focus} → 제거 또는 강화\n[gold]강림[/gold]  {DescentAct}막 · 위인의 과제 → 전용 유물\n\n영혼은 단계가 오를수록 선명해진다.",
               "Every figure card you play adds [gold]Mastery[/gold].\n\n[gold]Synchronization[/gold]  act {SyncAct}+ · Mastery {Sync} → a figure card\n[gold]Focus[/gold]  act {FocusAct}+ · Mastery {Focus} → remove or upgrade\n[gold]Descent[/gold]  act {DescentAct} · the figure's task → their relic\n\nThe spirit grows clearer with each stage."),
    "TITLE.4": ("위인", "The Figures"),
    "BODY.4": ("영사실에 앉아 있는 여섯 그림자.", "Six shadows sit in the projection room."),
}

INTRO = {
    "TITLE": ("무명", "Mumyeong"),
    "LINE": ("저승의 영사실이 문을 연다.", "The projection room of the dead opens its doors."),
}

STAGE_EVENT = {
    "title": ("영사실의 다음 릴", "The Next Reel"),
    "pages.INITIAL.description": (
        "[gold]{Figure}[/gold]의 기억이 비친다.\n\n「{Memory}」{Words}\n\n— [gold]{Stage}[/gold]",
        "[gold]{Figure}[/gold]'s memory flickers on the screen.\n\n\"{Memory}\"{Words}\n\n— [gold]{Stage}[/gold]"),
    "pages.INITIAL.words": ("[gold]당신의 유언[/gold] {Words}", "[gold]Your last words[/gold] {Words}"),
    "pages.INITIAL.options.SYNC.title": ("기억을 받아들인다", "Accept the memory"),
    "pages.INITIAL.options.SYNC.description": ("[gold]동기화[/gold]. 위인의 카드 3장 중 1장을 고릅니다.", "[gold]Synchronization[/gold]. Choose 1 of 3 of their cards."),
    "pages.INITIAL.options.FOCUS_REMOVE.title": ("하나를 버린다", "Let one go"),
    "pages.INITIAL.options.FOCUS_REMOVE.description": ("[gold]집중[/gold]. 덱에서 카드 1장을 제거합니다.", "[gold]Focus[/gold]. Remove 1 card from your deck."),
    "pages.INITIAL.options.FOCUS_UPGRADE.title": ("둘을 벼린다", "Hone two"),
    "pages.INITIAL.options.FOCUS_UPGRADE.description": ("[gold]집중[/gold]. 카드를 최대 2장 강화합니다.", "[gold]Focus[/gold]. Upgrade up to 2 cards."),
    "pages.INITIAL.options.DESCENT.title": ("하나가 된다", "Become one"),
    "pages.INITIAL.options.DESCENT.description": ("[gold]강림[/gold]. 위인의 유물을 얻습니다.", "[gold]Descent[/gold]. Obtain their relic."),
    "pages.NOTHING.description": ("영사기는 조용하다. 아직 다음 릴의 때가 아니다.", "The projector is silent. It is not yet time for the next reel."),
    "pages.NOTHING.options.LEAVE.title": ("떠난다", "Leave"),
    "pages.NOTHING.options.LEAVE.description": ("", ""),
    "pages.DONE.description": ("필름이 끝까지 돌았다. [gold]{Figure}[/gold]{Wa} 당신은 [gold]{Stage}[/gold]에 이르렀다.",
                               "The reel ran to its end. You and [gold]{Figure}[/gold] reached [gold]{Stage}[/gold]."),
}

CHARACTER = {
    "title": ("무명", "Mumyeong"),
    "titleObject": ("무명", "Mumyeong"),
    "description": ("한 번 죽었다 깨어난 이름 없는 자.\n저승의 영사실에서 위인 한 명의 코어를 받아,\n그 영혼과 함께 싸우며 하나가 되어 갑니다.",
                    "A nameless one who died once and woke again.\nReceiving the core of one great figure in the underworld's projection room,\nthey fight beside that soul and slowly become one."),
    "aromaPrinciple": ("[sine]이름은 없다. 대신 함께 걷는 자가 있다...[/sine]", "[sine]No name. But someone walks with me...[/sine]"),
    "banter.alive.endTurnPing": ("[sine]영사기 소리가 들린다.[/sine]", "[sine]I can hear the projector.[/sine]"),
    "banter.dead.endTurnPing": ("...", "..."),
    "cardsModifierTitle": ("무명 카드", "Mumyeong Cards"),
    "cardsModifierDescription": ("이제 무명 카드가 보상과 상점에 등장합니다.", "Mumyeong cards now appear in rewards and shops."),
    "eventDeathPrevention": ("한 번 죽었다. 두 번은 아직이다.", "I died once. Not twice, not yet."),
    "goldMonologue": ("[sine]저승 노잣돈이 생겼군.[/sine]", "[sine]Fare for the ferryman.[/sine]"),
    "possessiveAdjective": ("그의", "their"),
    "pronounObject": ("그를", "them"),
    "pronounPossessive": ("그의 것", "theirs"),
    "pronounSubject": ("그", "they"),
}

ANCIENTS = {
    "NEOW.talk.{C}.0-0.ancient": ("...심장이 멈춘 냄새가 나는구나.", "...You smell of a heart that stopped."),
    "NEOW.talk.{C}.0-0.next": ("계속", "Continue"),
    "NEOW.talk.{C}.0-1.char": ("멈췄었지. 지금은 다시 뛰어.", "It stopped. It beats again now."),
    "NEOW.talk.{C}.0-1.next": ("계속", "Continue"),
    "NEOW.talk.{C}.0-2.ancient": ("그럼 내 축복도 하나 가져가렴. 저승에서 누가 기다리는지는 모르겠지만.",
                                  "Then take a blessing of mine. I do not know who waits for you below."),
    "NEOW.talk.{C}.1-0.ancient": ("또 왔구나. 이번엔 누구의 영혼과 함께니?", "Back again. Whose soul walks with you this time?"),
    "NEOW.talk.{C}.1-0.next": ("계속", "Continue"),
    "NEOW.talk.{C}.1-1.char": ("필름을 돌려 봐야 알아.", "I won't know until the film rolls."),
    "NEOW.talk.{C}.2-0.ancient": ("몇 번을 죽어도 돌아오는구나, 이름 없는 아이야.", "However many times you die, you come back, nameless child."),
    "NEOW.talk.{C}.2-0.next": ("계속", "Continue"),
    "NEOW.talk.{C}.2-1.char": ("객석에 아직 사람이 남아 있으니까.", "There are still people in the seats."),
    "NEOW.talk.{C}.2-1.next": ("계속", "Continue"),
    "NEOW.talk.{C}.2-2.ancient": ("...그렇다면, 또 일어나렴.", "...Then rise again."),
}


# THE_ARCHITECT dialogue: same key structure as 엔시페르 (the localization analyzer expects it for a character).
ARCHITECT_LINES = [
    ("0-0r.char", "이 탑 꼭대기에도 객석이 있나?", "Are there seats at the top of this spire too?"),
    ("0-1r.ancient", "이름 없는 자여. 탑은 관객을 두지 않는다. 오르는 자만 있을 뿐.", "Nameless one. My spire keeps no audience. Only climbers."),
    ("1-0.char", "다시 올라왔어. 이번엔 다른 영혼과 함께야.", "I climbed again. With a different soul this time."),
    ("1-1.ancient", "영혼을 갈아입으며 오르는 자라. 기묘하군.", "One who climbs while changing souls. Strange."),
    ("1-2.char", "갈아입는 게 아니야. 함께 걷는 거지.", "Not changing. Walking together."),
    ("2-0.ancient", "네 심장 소리가 둘로 들린다.", "I hear your heart beating twice."),
    ("2-1.char", "맞아. 하나는 내 것이 아니야.", "Yes. One of them is not mine."),
    ("3-0.char", "이번 필름은 끝까지 돌릴 거야.", "This time I'll run the reel to the end."),
    ("3-1.ancient", "끝을 본 필름은 다시 돌지 않는다.", "A reel that reaches its end does not turn again."),
    ("3-2.char", "그래서 끝까지 가는 거야.", "That's why I'm going to the end."),
    ("4-0.ancient", "죽었다 돌아온 자. 이 탑 위에서 또 죽을 셈이냐.", "One who died and returned. Do you mean to die again atop my spire?"),
    ("4-1.char", "한 번이면 충분했어.", "Once was enough."),
    ("4-2.ancient", "...그렇다면 버텨 보아라.", "...Then endure."),
    ("5-0.char", "꼭대기에 오니까 내 안의 영혼이 조용해졌어.", "At the summit, the soul inside me went quiet."),
    ("5-1.ancient", "두려워서겠지.", "Out of fear, no doubt."),
    ("5-2.char", "아니. 자기 마지막 싸움이 생각나서야.", "No. It remembers its own last battle."),
    ("6-0r.ancient", "다시 왔군. 이번엔 누구의 영혼이냐.", "Back again. Whose soul is it this time?"),
    ("6-1r.char", "누구든, 마지막 1초는 같이 넘을 거야.", "Whoever it is, we'll cross the last second together."),
    ("6-2r.ancient", "그렇다면 증명해 보아라.", "Then prove it."),
    ("7-0r.char", "이 탑에도 필름 한 롤쯤 남겨 두고 갈까?", "Should I leave a reel of film in this spire?"),
    ("7-1r.ancient", "내 탑은 영사실이 아니다.", "My spire is no projection room."),
    ("7-2r.char", "저승도 그렇게 말했어.", "The underworld said the same."),
    ("8-0r.ancient", "아무리 많은 영혼을 빌려도, 탑의 끝은 하나다.", "Borrow as many souls as you like; the spire still has one summit."),
    ("8-1r.char", "그럼 그 끝까지 둘이서 갈게.", "Then the two of us will go to that summit."),
    ("8-2r.ancient", "...오너라.", "...Come."),
]
ARCHITECT_EXTRA = {
    "0-0r.next": None, "1-0.next": None, "1-1.next": None, "2-0.next": None, "3-0.next": None, "3-1.next": None,
    "4-0.next": None, "4-1.next": None, "5-0.next": None, "5-1.next": None, "6-0r.next": None, "6-1r.next": None,
    "7-0r.next": None, "7-1r.next": None, "8-0r.next": None, "8-1r.next": None,
}

CARD_SELECTION = {
    P + "UPGRADE_PROMPT": ("강화할 카드를 최대 2장 고르세요", "Choose up to 2 cards to upgrade"),
}

REST_SITE = {
    "OPTION_MUMYEONG_FILM_REEL.name": ("필름을 돌린다", "Roll the Reel"),
    "OPTION_MUMYEONG_FILM_REEL.description": ("영혼과의 다음 단계로 들어갑니다.", "Enter the next stage with the soul."),
    # Shown instead of .description: only the stage that opens now (FilmReelRestSiteOption.Description).
    "OPTION_MUMYEONG_FILM_REEL.SYNCHRONIZATION": ("[gold]동기화[/gold]: 위인의 카드 3장 중 1장을 고릅니다.",
                                                 "[gold]Synchronization[/gold]: choose 1 of 3 of their cards."),
    "OPTION_MUMYEONG_FILM_REEL.FOCUS": ("[gold]집중[/gold]: 카드를 최대 2장 강화합니다.", "[gold]Focus[/gold]: upgrade up to 2 cards."),
    "OPTION_MUMYEONG_FILM_REEL.DESCENT": ("[gold]강림[/gold]: 위인의 유물을 얻습니다.", "[gold]Descent[/gold]: obtain their relic."),
}


def build(lang):
    k = lang == "kor"
    i = 0 if k else 1
    out = {}

    cards = {}
    for cls, kt, kd, et, ed in CARDS:
        key = P + snake(cls)
        cards[key + ".title"] = kt if k else et
        cards[key + ".description"] = kd if k else ed
    out["cards"] = cards

    powers = {}
    for cls, (kt, kd, et, ed) in POWERS.items():
        key = P + snake(cls)
        powers[key + ".title"] = kt if k else et
        powers[key + ".description"] = kd if k else ed
        powers[key + ".smartDescription"] = kd if k else ed
    out["powers"] = powers

    relics = {}
    for cls, (kt, kd, kf, et, ed, ef) in RELICS.items():
        key = P + snake(cls)
        relics[key + ".title"] = kt if k else et
        relics[key + ".description"] = kd if k else ed
        relics[key + ".flavor"] = kf if k else ef
    out["relics"] = relics

    events = {}
    for fig, parts in FIGURES.items():
        for part, pair in parts.items():
            events[f"{P}FIGURE.{fig}.{part}"] = pair[i]
    for part, pair in FIGURE_GENERIC.items():
        events[f"{P}FIGURE.{part}"] = pair[i]
    for part, pair in THEATER_GENERIC.items():
        events[f"{P}THEATER.{part}"] = pair[i]
    for scene, data in SCENES.items():
        events[f"{P}THEATER.{scene}.DESC"] = data["DESC"][i]
        for cut in ("GAZE", "ACTION", "WORDS"):
            for fig in FIGS:
                events[f"{P}THEATER.{scene}.{cut}.{fig}"] = data[cut][fig][i]
    for part, pair in THEATER_EVENT.items():
        events[f"{P}THEATER_EVENT.{part}"] = pair[i]
    for part, pair in STAGE_EVENT.items():
        events[f"{P}STAGE_UP_EVENT.{part}"] = pair[i]
    events[f"{P}CINEMA.CONTINUE"] = "계속" if k else "Continue"
    for part, pair in INFO.items():
        events[f"{P}INFO.{part}"] = pair[i]
    for part, pair in INTRO.items():
        events[f"{P}INTRO.{part}"] = pair[i]
    for fig, sits in TALK.items():
        for sit, lines in sits.items():
            for n, (sk, rk, se, re_) in enumerate(lines):
                events[f"{P}SPIRIT_TALK.{fig}.{sit}.{n}.S"] = sk if k else se
                if rk:
                    events[f"{P}SPIRIT_TALK.{fig}.{sit}.{n}.R"] = rk if k else re_
    out["events"] = events

    out["characters"] = {f"{P}MUMYEONG_CHARACTER.{part}": pair[i] for part, pair in CHARACTER.items()}
    anc = {key.replace("{C}", f"{P}MUMYEONG_CHARACTER"): pair[i] for key, pair in ANCIENTS.items()}
    arch = f"THE_ARCHITECT.talk.{P}MUMYEONG_CHARACTER."
    for key, kt, et in ARCHITECT_LINES:
        anc[arch + key] = kt if k else et
    for key in ARCHITECT_EXTRA:
        anc[arch + key] = "계속" if k else "Continue"
    for n in range(9):
        anc[arch + f"{n}-attack"] = "Both"
    for n in (6, 7, 8):
        anc[arch + f"{n}-visit"] = "6"
    out["ancients"] = anc
    out["card_selection"] = {key: pair[i] for key, pair in CARD_SELECTION.items()}
    out["rest_site_ui"] = {key: pair[i] for key, pair in REST_SITE.items()}
    out["card_keywords"] = {}
    out["static_hover_tips"] = {}
    return out


def main():
    for lang in ("kor", "eng"):
        folder = os.path.join(ROOT, lang)
        os.makedirs(folder, exist_ok=True)
        for table, data in build(lang).items():
            with open(os.path.join(folder, table + ".json"), "w", encoding="utf-8") as f:
                json.dump(data, f, ensure_ascii=False, indent=2)
                f.write("\n")
        print(lang, {t: len(d) for t, d in build(lang).items()})


if __name__ == "__main__":
    main()
