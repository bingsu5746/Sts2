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

P = "MUMYEONG-"
ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Mumyeong", "localization")


def snake(name):
    return re.sub(r"(?<!^)(?=[A-Z])", "_", name).upper()


# ---------------------------------------------------------------------------------------------- powers
# class -> (kor title, kor desc, eng title, eng desc)
POWERS = {
    "ApplausePower": ("객석의 갈채", "내 턴이 시작될 때마다 [gold]방어도[/gold]를 {Amount} 얻습니다.",
                      "Applause", "At the start of your turn, gain {Amount} [gold]Block[/gold]."),
    "GrudgePower": ("한", "[gold]한 계약[/gold]: 위인의 카드가 아닌 카드를 쓸 때마다 쌓입니다. 5가 되면 영혼이 몸을 할퀴어 체력을 4 잃고 사라집니다.",
                    "Grudge", "[gold]Grudge Contract[/gold]: grows whenever you play a card that is not your figure's. At 5, the soul lashes out: lose 4 HP and it resets."),
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
    "NapoleonBlitzPower": ("전격전", "한 턴에 카드를 4장 쓸 때마다 에너지를 {Amount} 얻습니다.",
                           "Lightning Campaign", "Every 4th card you play in a turn gives {Amount} energy."),
    "NapoleonCoronationPower": ("대관식", "최대 에너지가 {Amount} 늘어납니다.",
                                "Coronation", "Your max energy is increased by {Amount}."),
}

# ---------------------------------------------------------------------------------------------- relics
RELICS = {
    "SoulCore": ("코어",
                 "저승의 영사실에서 받은 위인의 영혼. 그 위인의 카드를 쓸 때마다 숙련이 1 오르고, 숙련과 막 진행으로 영혼과의 단계가 깊어집니다. 카드 보상과 상점에는 공용 카드와 계약한 위인의 카드만 나옵니다.{Status}",
                 "한 번 멈춘 심장 대신 뛰는 것.",
                 "Soul Core",
                 "The soul of a great figure, received in the underworld's projection room. Playing that figure's cards raises Mastery by 1; Mastery and the acts deepen your bond. Card rewards and shops only offer shared cards and your figure's cards.{Status}",
                 "It beats where a stopped heart used to."),
    "GuanyuGreenDragonRelic": ("청룡언월도", "전투를 시작할 때 [gold]활력[/gold]을 8 얻습니다.", "푸른 용이 새겨진 언월도. 무겁다.",
                               "Green Dragon Crescent Blade", "At the start of each combat, gain 8 [gold]Vigor[/gold].", "A crescent blade carved with a green dragon. Heavy."),
    "JoanBannerRelic": ("오를레앙의 깃발", "내 턴이 시작될 때마다 [gold]신념[/gold]을 2 얻습니다.", "불길 속에서도 내려지지 않았다.",
                        "Banner of Orléans", "At the start of your turn, gain 2 [gold]Faith[/gold].", "It was not lowered, even in the fire."),
    "LeonidasShieldRelic": ("스파르타의 방패", "전투를 시작할 때 [gold]방진[/gold]과 [gold]반격[/gold] 3을 얻습니다.", "방패를 들고 오라. 아니면 그 위에 실려 오라.",
                            "Spartan Shield", "At the start of each combat, gain [gold]Phalanx[/gold] and 3 [gold]Counter[/gold].", "Come back with it, or on it."),
    "GenghisSealRelic": ("대칸의 인장", "적이 쓰러질 때마다 에너지를 1 얻고 카드를 1장 뽑습니다.", "초원 끝에서 끝까지 통하는 인장.",
                         "Seal of the Great Khan", "Whenever an enemy dies, gain 1 energy and draw 1 card.", "A seal honoured from one end of the steppe to the other."),
    "YiTurtleShipRelic": ("거북선", "내 턴이 끝날 때마다 모든 적에게 피해를 5 주고 [gold]방어도[/gold]를 5 얻습니다.", "등에 돋은 철침이 바다를 지킨다.",
                          "Turtle Ship", "At the end of your turn, deal 5 damage to ALL enemies and gain 5 [gold]Block[/gold].", "The spikes on its back guard the sea."),
    "NapoleonCrownRelic": ("황제의 관", "최대 에너지가 1 늘어납니다.", "스스로 머리에 얹은 관.",
                           "Emperor's Crown", "Gain 1 max energy.", "A crown placed by one's own hands."),
}

# ---------------------------------------------------------------------------------------------- figures
FIGURES = {
    "GUANYU": {
        "NAME": ("관우", "Guan Yu"),
        "SHADOW": ("돌아가지 못한 맹세", "An oath never returned to"),
        "LAST_DEED": ("맥성에서 포위되자 성을 나와 길을 뚫으려 했고, 그 길에서 매복에 걸려 사로잡혔다.",
                      "Surrounded at Maicheng, he left the city to break through, and was ambushed and captured on the road."),
        "MEMORY_1": ("형님이라 부른 사람이 있었다. 그 이름 하나 때문에 먼 길을 돌아갔다.",
                     "There was a man I called elder brother. For that one name I took the long road back."),
        "MEMORY_2": ("칼은 무거웠고, 사람들은 나를 두려워하면서도 우러러보았다.",
                     "The blade was heavy. People feared me, and still looked up to me."),
        "MEMORY_3": ("그날 밤 나는 길을 뚫으려 했다. 이번에는 네 몸으로 그 길을 끝까지 간다.",
                     "That night I tried to break through. This time, in your body, I walk that road to its end."),
        "TASK": ("공격 한 번으로 적에게 피해 30 이상", "Deal 30+ damage to an enemy with a single attack"),
    },
    "JOAN": {
        "NAME": ("잔 다르크", "Joan of Arc"),
        "SHADOW": ("불길 속에서도 거두지 않은 말", "A word not taken back, even in the fire"),
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
        "SHADOW": ("지켜 낸 고개, 돌아가지 못한 집", "The pass held, the home never reached"),
        "LAST_DEED": ("페르시아군이 산길로 뒤를 돌아 나오자 연합군 대부분을 돌려보내고, 남은 병사들과 함께 테르모필레에 남아 싸웠다(기원전 480년).",
                      "When the Persians came round behind by the mountain path, he sent most of the allies away and stayed at Thermopylae with those who remained (480 BC)."),
        "MEMORY_1": ("방패는 나를 위한 것이 아니었다. 옆에 선 사람을 위한 것이었다.",
                     "The shield was never for me. It was for the man standing beside me."),
        "MEMORY_2": ("좁은 고개에서는 수가 아니라 대열이 싸운다.",
                     "In a narrow pass, it is the line that fights, not the numbers."),
        "MEMORY_3": ("돌아가지 않았다. 지금도 돌아가지 않는다.",
                     "I did not turn back. I do not turn back now."),
        "TASK": ("한 전투에서 [gold]방어도[/gold]로 막은 피해 합계 50 이상", "Block a total of 50+ damage in one combat"),
    },
    "GENGHIS": {
        "NAME": ("칭기즈 칸", "Genghis Khan"),
        "SHADOW": ("끝나지 않은 원정", "A campaign never finished"),
        "LAST_DEED": ("서하 원정 중이던 1227년에 세상을 떠났다. 원정은 아직 끝나지 않았다.",
                      "He died in 1227, during the campaign against Western Xia. The campaign was not yet over."),
        "MEMORY_1": ("초원에서 모든 것을 잃은 아이가 있었다. 그 아이는 다시는 잃지 않기로 했다.",
                     "There was a child on the steppe who lost everything. That child decided never to lose again."),
        "MEMORY_2": ("흩어진 부족을 하나로 묶었다. 말은 쉬지 않았다.",
                     "I bound the scattered tribes into one. The horses never rested."),
        "MEMORY_3": ("원정은 끝나지 않았다. 네 발로 그 끝을 밟아라.",
                     "The campaign is not finished. Tread its end with your own feet."),
        "TASK": ("한 전투에서 적 3명 처치", "Kill 3 enemies in one combat"),
    },
    "YISUNSIN": {
        "NAME": ("이순신", "Yi Sun-sin"),
        "SHADOW": ("끝나지 않은 바다", "A sea not yet at peace"),
        "LAST_DEED": ("노량(1598년)에서 총탄에 맞아 쓰러지며 \"싸움이 급하니 내 죽음을 알리지 말라\"고 했다고 전한다.",
                      "At Noryang (1598), struck by a bullet, he is said to have ordered: \"The battle is at its height. Do not announce my death.\""),
        "MEMORY_1": ("바다를 지키는 것은 배가 아니라 사람이었다. 나는 날마다 일기를 썼다.",
                     "It was people, not ships, who guarded the sea. Every day I kept a diary."),
        "MEMORY_2": ("적은 많았고 우리 배는 적었다. 그래도 물러서지 않았다.",
                     "The enemy was many and our ships were few. Still, we did not give way."),
        "MEMORY_3": ("싸움이 끝나기 전에 쓰러졌다. 이번에는 끝을 본다.",
                     "I fell before the battle ended. This time I will see the end."),
        "TASK": ("체력 50% 이하로 엘리트·보스 전투 승리", "Win an Elite or Boss combat at 50% HP or less"),
    },
    "NAPOLEON": {
        "NAME": ("나폴레옹", "Napoleon"),
        "SHADOW": ("섬에 갇힌 야망", "Ambition caged on an island"),
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
    "CONTRACT_TEXT.RESONANCE": ("같은 길을 걸은 영혼. 동기화와 집중에 필요한 숙련이 줄어듭니다.",
                                "A soul that walked the same road. Synchronization and Focus need less Mastery."),
    "CONTRACT_TEXT.GRUDGE": ("다른 결이 섞인 영혼. 전투를 [gold]힘[/gold] 1로 시작하지만, 위인의 카드가 아닌 카드를 쓰면 [gold]한[/gold]이 쌓입니다.",
                             "A soul mixed with another grain. Start each combat with 1 [gold]Strength[/gold], but cards that are not your figure's build [gold]Grudge[/gold]."),
    "STATUS.NO_FIGURE": ("아직 아무도 응답하지 않았다.", "No one has answered yet."),
    "STATUS.HEAD": ("[gold]{Figure}[/gold] · {Contract} · {Stage}", "[gold]{Figure}[/gold] · {Contract} · {Stage}"),
    "STATUS.MASTERY": ("{Stage}까지: 숙련 {Mastery}/{Needed} ({Act}막부터)", "To {Stage}: Mastery {Mastery}/{Needed} (from act {Act})"),
    "STATUS.TASK": ("{Stage} 과제: {Task}", "{Stage} task: {Task}"),
    "STATUS.READY": ("[gold]{Stage}[/gold] 준비 완료 — 필름이 돌 때를 기다린다.", "[gold]{Stage}[/gold] is ready. The reel waits to turn."),
    "STATUS.COMPLETE": ("영혼과 완전히 하나가 되었다.", "You and the soul are one."),
}

THEATER_GENERIC = {
    "PROMPT.GAZE": ("마지막 1초. 당신의 [gold]시선[/gold]은 어디로 향하는가.", "The last second. Where does your [gold]gaze[/gold] go?"),
    "PROMPT.ACTION": ("정지된 1초. 당신의 [gold]행동[/gold]은.", "A frozen second. What do you [gold]do[/gold]?"),
    "PROMPT.WORDS": ("마지막 말. 한 번도 소리 내지 못했던 [gold]유언[/gold].", "Last words. The [gold]words[/gold] you never said aloud."),
    "REACTS": ("「{Shadow}」의 그림자가 반응한다.", "The shadow of \"{Shadow}\" stirs."),
    "REACTION": ("[gold]「{Shadow}」[/gold]의 그림자가 한 걸음 다가온다.", "The shadow of [gold]\"{Shadow}\"[/gold] steps closer."),
    "MISSING_LINE": ("...", "..."),
}

THEATER_EVENT = {
    "title": ("황천 영사실", "The Underworld Projection Room"),
    "pages.INITIAL.description": (
        "암전.\n심장이 두 번 뛴다.\n\n[gold]당신은 1초 뒤에 죽는다.[/gold]\n\n...영사기 돌아가는 소리. 어두운 객석에 그림자 셋이 앉아 있다. 이름은 보이지 않는다. 머리 위에 한 줄씩, 그들이 품은 것만 떠 있다.\n{Shadows}",
        "Blackout.\nYour heart beats twice.\n\n[gold]You will die in one second.[/gold]\n\n...The whir of a projector. Three shadows sit in the dark seats. Their names cannot be seen; above each floats only what they carry.\n{Shadows}"),
    "pages.INITIAL.options.ROLL.title": ("필름을 돌린다", "Roll the film"),
    "pages.INITIAL.options.ROLL.description": ("당신의 마지막 1초가 상영된다.", "Your last second is screened."),
    "pages.CUT.description": ("{Reaction}{Scene}{Prompt}", "{Reaction}{Scene}{Prompt}"),
    "pages.CUT.options.LINE.title": ("...", "..."),
    "pages.CUT.options.LINE.description": ("...", "..."),
    "pages.DESCENT.description": (
        "{Reaction}필름이 멈춘다. 객석에서 [gold]「{Shadow}」[/gold]의 그림자가 일어선다.\n그림자가 스크린 속으로 걸어 들어오며, 이름이 드러난다.\n\n[gold]{Figure}[/gold]",
        "{Reaction}The film stops. The shadow of [gold]\"{Shadow}\"[/gold] rises from its seat.\nIt walks into the screen, and its name is revealed.\n\n[gold]{Figure}[/gold]"),
    "pages.DESCENT.options.WATCH.title": ("두 개의 필름을 본다", "Watch both reels"),
    "pages.DESCENT.options.WATCH.description": ("당신의 마지막 1초와 그의 마지막 1초가 나란히 상영된다.", "Your last second and theirs are screened side by side."),
    "pages.REVEAL.description": (
        "[gold]당신의 마지막 1초[/gold]\n{Mine}\n\n[gold]{Figure}의 마지막 1초[/gold]\n{Deed}\n\n공명 {Resonance}/3 — [gold]{Contract}[/gold]\n{ContractText}",
        "[gold]Your last second[/gold]\n{Mine}\n\n[gold]{Figure}'s last second[/gold]\n{Deed}\n\nResonance {Resonance}/3 — [gold]{Contract}[/gold]\n{ContractText}"),
    "pages.REVEAL.options.CONTRACT.title": ("그의 손을 잡는다", "Take their hand"),
    "pages.REVEAL.options.CONTRACT.description": ("계약을 맺는다. 그의 카드 2장이 덱에 들어오고, 그의 기술 하나를 익힌다.",
                                                  "Make the contract. Their 2 cards join your deck, and you learn one of their techniques."),
    "pages.CONTRACTED.description": ("심장이 다시 뛴다. 하지만 이제 그 박동은 둘의 것이다.\n[gold]{Figure}[/gold] — {Contract}",
                                     "Your heart beats again. But now the beat belongs to two.\n[gold]{Figure}[/gold] — {Contract}"),
    "pages.NO_CORE.description": ("필름이 끊겼다. 객석은 비어 있다.", "The film snapped. The seats are empty."),
}

STAGE_EVENT = {
    "title": ("영사실의 다음 릴", "The Next Reel"),
    "pages.INITIAL.description": (
        "어디선가 영사기가 다시 돌기 시작한다. 스크린에 [gold]{Figure}[/gold]의 기억 한 컷이 비친다.\n\n「{Memory}」\n\n영혼이 한 걸음 더 깊이 들어온다 — [gold]{Stage}[/gold].",
        "Somewhere, a projector starts again. One cut of [gold]{Figure}[/gold]'s memory appears on the screen.\n\n\"{Memory}\"\n\nThe soul steps one stage deeper — [gold]{Stage}[/gold]."),
    "pages.INITIAL.options.SYNC.title": ("기억을 받아들인다", "Accept the memory"),
    "pages.INITIAL.options.SYNC.description": ("[gold]동기화[/gold]. 그의 카드 3장 중 1장을 고릅니다.", "[gold]Synchronization[/gold]. Choose 1 of 3 of their cards."),
    "pages.INITIAL.options.FOCUS_REMOVE.title": ("하나를 버린다", "Let one go"),
    "pages.INITIAL.options.FOCUS_REMOVE.description": ("[gold]집중[/gold]. 덱에서 카드 1장을 제거합니다.", "[gold]Focus[/gold]. Remove 1 card from your deck."),
    "pages.INITIAL.options.FOCUS_UPGRADE.title": ("둘을 벼린다", "Hone two"),
    "pages.INITIAL.options.FOCUS_UPGRADE.description": ("[gold]집중[/gold]. 카드를 최대 2장 강화합니다.", "[gold]Focus[/gold]. Upgrade up to 2 cards."),
    "pages.INITIAL.options.DESCENT.title": ("하나가 된다", "Become one"),
    "pages.INITIAL.options.DESCENT.description": ("[gold]강림[/gold]. 그의 유물을 얻습니다.", "[gold]Descent[/gold]. Obtain their relic."),
    "pages.NOTHING.description": ("영사기는 조용하다. 아직 다음 릴의 때가 아니다.", "The projector is silent. It is not yet time for the next reel."),
    "pages.NOTHING.options.LEAVE.title": ("떠난다", "Leave"),
    "pages.NOTHING.options.LEAVE.description": ("", ""),
    "pages.DONE.description": ("필름이 끝까지 돌았다. [gold]{Figure}[/gold]과(와) 당신은 [gold]{Stage}[/gold]에 이르렀다.",
                               "The reel ran to its end. You and [gold]{Figure}[/gold] reached [gold]{Stage}[/gold]."),
}

CHARACTER = {
    "title": ("무명", "Mumyeong"),
    "titleObject": ("무명", "Mumyeong"),
    "description": ("한 번 죽었다 깨어난 이름 없는 자.\n저승의 영사실에서 위인 한 명의 코어를 받아,\n그 영혼과 함께 깊어지며 싸웁니다.",
                    "A nameless one who died once and woke again.\nReceiving the core of one great figure in the underworld's projection room,\nthey fight as that soul grows deeper within."),
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

CARD_SELECTION = {
    P + "UPGRADE_PROMPT": ("강화할 카드를 최대 2장 고르세요", "Choose up to 2 cards to upgrade"),
}

REST_SITE = {
    "OPTION_MUMYEONG_FILM_REEL.name": ("필름을 돌린다", "Roll the Reel"),
    "OPTION_MUMYEONG_FILM_REEL.description": ("영혼과의 다음 단계로 들어갑니다(동기화: 카드 고르기 · 집중: 카드 2장 강화 · 강림: 위인의 유물).",
                                             "Enter the next stage with the soul (Synchronization: pick a card · Focus: upgrade 2 cards · Descent: their relic)."),
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
    out["events"] = events

    out["characters"] = {f"{P}MUMYEONG_CHARACTER.{part}": pair[i] for part, pair in CHARACTER.items()}
    out["ancients"] = {key.replace("{C}", f"{P}MUMYEONG_CHARACTER"): pair[i] for key, pair in ANCIENTS.items()}
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
