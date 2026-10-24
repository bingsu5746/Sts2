# Card texts (kor, eng). Used by tools/build_loc.py. Class name -> (kor title, kor desc, eng title, eng desc).
# Keywords (Exhaust, Retain) are shown by the game itself, so they are not written here.

def dmg(k=True):
    return "피해를 {Damage:diff()} 줍니다." if k else "Deal {Damage:diff()} damage."

def dmg_all(k=True):
    return "모든 적에게 피해를 {Damage:diff()} 줍니다." if k else "Deal {Damage:diff()} damage to ALL enemies."

def dmg_hits(k=True):
    return "피해를 {Damage:diff()}만큼 {Hits}번 줍니다." if k else "Deal {Damage:diff()} damage {Hits} times."

def dmg_all_hits(k=True):
    return ("모든 적에게 피해를 {Damage:diff()}만큼 {Hits}번 줍니다." if k
            else "Deal {Damage:diff()} damage to ALL enemies {Hits} times.")

def block(k=True):
    return "[gold]방어도[/gold]를 {Block:diff()} 얻습니다." if k else "Gain {Block:diff()} [gold]Block[/gold]."

def draw(k=True):
    return "카드를 {Cards:diff()}장 뽑습니다." if k else "Draw {Cards:diff()} card(s)."

def energy(k=True):
    return "{Energy:energyIcons()}를 얻습니다." if k else "Gain {Energy:energyIcons()}."

def power(name_k, name_e, var, k=True):
    return f"[gold]{name_k}[/gold]을(를) {{{var}:diff()}} 얻습니다." if k else f"Gain {{{var}:diff()}} [gold]{name_e}[/gold]."

def vigor(k=True): return power("활력", "Vigor", "VigorPower", k)
def faith(k=True): return power("신념", "Faith", "JoanFaithPower", k)
def counter(k=True): return power("반격", "Counter", "LeonidasCounterPower", k)
def cannon(k=True): return power("포격", "Bombard", "YiCannonPower", k)
def strength(k=True): return power("힘", "Strength", "StrengthPower", k)

def weak_t(k=True):
    return "[gold]약화[/gold]를 {WeakPower:diff()} 부여합니다." if k else "Apply {WeakPower:diff()} [gold]Weak[/gold]."

def vuln_t(k=True):
    return "[gold]취약[/gold]을 {VulnerablePower:diff()} 부여합니다." if k else "Apply {VulnerablePower:diff()} [gold]Vulnerable[/gold]."

def weak_all(k=True):
    return ("모든 적에게 [gold]약화[/gold]를 {WeakPower:diff()} 부여합니다." if k
            else "Apply {WeakPower:diff()} [gold]Weak[/gold] to ALL enemies.")

def vuln_all(k=True):
    return ("모든 적에게 [gold]취약[/gold]을 {VulnerablePower:diff()} 부여합니다." if k
            else "Apply {VulnerablePower:diff()} [gold]Vulnerable[/gold] to ALL enemies.")

def j(*parts):
    return "\n".join(parts)

def both(fn_list):
    """fn_list: list of callables taking k -> returns (kor, eng)."""
    return j(*[f(True) for f in fn_list]), j(*[f(False) for f in fn_list])

def card(cls, kt, et, parts_or_pair):
    if isinstance(parts_or_pair, tuple):
        kd, ed = parts_or_pair
    else:
        kd, ed = both(parts_or_pair)
    return cls, kt, kd, et, ed

def T(k, e):
    """literal text line in both languages"""
    return lambda kor: k if kor else e

CARDS = [
    # ------------------------------------------------------------------ shared
    card("MumyeongStrike", "타격", "Strike", [dmg]),
    card("MumyeongDefend", "수비", "Defend", [block]),
    card("SoulFlame", "혼불", "Soul Flame", [draw]),
    card("LastBreath", "마지막 숨", "Last Breath", [block]),
    card("UnderworldCut", "황천 베기", "Underworld Cut", [dmg]),
    card("AudienceApplause", "객석의 갈채", "Applause from the Seats",
         [T("내 턴이 시작될 때마다 [gold]방어도[/gold]를 {ApplausePower:diff()} 얻습니다.",
            "At the start of your turn, gain {ApplausePower:diff()} [gold]Block[/gold].")]),

    # ------------------------------------------------------------------ 관우
    card("GuanyuCrescentBlade", "청룡언월", "Green Dragon Crescent", [dmg]),
    card("GuanyuLoneRider", "단기", "Lone Rider", [vigor]),
    card("GuanyuCleave", "일도양단", "Cleave in Two", [dmg]),
    card("GuanyuBeard", "미염공", "Lord of the Beautiful Beard", [block, vigor]),
    card("GuanyuWarmWine", "온주", "Warm Wine", [dmg, T("[gold]활력[/gold]이 있었다면 카드를 {Cards:diff()}장 뽑습니다.",
                                                      "If you had [gold]Vigor[/gold], draw {Cards:diff()} card(s).")]),
    card("GuanyuSweep", "청룡 휘두르기", "Dragon Sweep", [dmg_all]),
    card("GuanyuPresence", "위세", "Presence", [vigor]),
    card("GuanyuLoyalty", "의리", "Loyalty", [block]),
    card("GuanyuFivePasses", "오관돌파", "Through Five Passes", [dmg_hits]),
    card("GuanyuFlood", "수엄칠군", "Flooding the Seven Armies", [dmg_all, weak_all]),
    card("GuanyuBoneScrape", "괄골요독", "Scraping the Bone", [strength, block]),
    card("GuanyuRedHare", "적토마", "Red Hare", [draw, vigor]),
    card("GuanyuAwe", "위진화하", "Awe over the Land",
         [T("내 턴이 시작될 때마다 [gold]활력[/gold]을 {GuanyuAwePower:diff()} 얻습니다.",
            "At the start of your turn, gain {GuanyuAwePower:diff()} [gold]Vigor[/gold].")]),
    card("GuanyuHuarong", "화용도", "Huarong Trail", [weak_t, block]),
    card("GuanyuSingleBlade", "단도부회", "Lone Blade to the Feast", [dmg, vuln_t]),
    card("GuanyuThousand", "일기당천", "One Against a Thousand", [dmg]),
    card("GuanyuWarGod", "무신", "God of War",
         [T("공격 카드를 쓸 때마다 [gold]활력[/gold]을 {GuanyuWarGodPower:diff()} 얻습니다.",
            "Whenever you play an Attack, gain {GuanyuWarGodPower:diff()} [gold]Vigor[/gold].")]),
    card("GuanyuGreenDragonSlash", "언월참", "Crescent Execution", [dmg]),
    card("GuanyuPeachGardenOath", "도원의 맹세", "Oath of the Peach Garden", [strength, block]),
    card("GuanyuRighteous", "의절천추", "Righteous Through the Ages",
         [T("스킬 카드를 쓸 때마다 [gold]활력[/gold]을 {GuanyuRighteousPower:diff()} 얻습니다.",
            "Whenever you play a Skill, gain {GuanyuRighteousPower:diff()} [gold]Vigor[/gold].")]),

    # ------------------------------------------------------------------ 잔 다르크
    card("JoanRaiseBanner", "깃발 들기", "Raise the Banner", [faith]),
    card("JoanMaidensSword", "성녀의 검", "The Maid's Sword", [dmg, faith]),
    card("JoanCharge", "돌격", "Charge", [dmg]),
    card("JoanPrayer", "기도", "Prayer", [block, faith]),
    card("JoanVoices", "목소리", "The Voices", [draw, faith]),
    card("JoanLiftTheSiege", "포위 풀기", "Lift the Siege", [dmg_all]),
    card("JoanResolve", "결심", "Resolve", [block, T("[gold]신념[/gold]이 {FaithNeeded} 이상이면 카드를 {Cards:diff()}장 뽑습니다.",
                                                   "If you have {FaithNeeded} or more [gold]Faith[/gold], draw {Cards:diff()} card(s).")]),
    card("JoanRally", "집결", "Rally", [faith, block]),
    card("JoanOrleans", "오를레앙", "Orléans", [dmg_hits, faith]),
    card("JoanCoronation", "랭스의 대관식", "Coronation at Reims", [energy, faith]),
    card("JoanUnwaveringBanner", "흔들리지 않는 깃발", "Unwavering Banner",
         [T("내 턴이 시작될 때마다 [gold]신념[/gold]을 {JoanBannerPower:diff()} 얻습니다.",
            "At the start of your turn, gain {JoanBannerPower:diff()} [gold]Faith[/gold].")]),
    card("JoanTrial", "심문", "Interrogation", [vuln_t, weak_t]),
    card("JoanPyre", "화형대", "The Pyre",
         [T("체력을 잃을 때마다 [gold]신념[/gold]을 {JoanPyrePower:diff()} 얻습니다.",
            "Whenever you lose HP, gain {JoanPyrePower:diff()} [gold]Faith[/gold].")]),
    card("JoanZeal", "열의", "Zeal",
         [T("피해를 {CalculatedDamage:diff()} 줍니다.\n(기본 {CalculationBase:diff()}, [gold]신념[/gold] 1당 {ExtraDamage:diff()} 더)",
            "Deal {CalculatedDamage:diff()} damage.\n({CalculationBase:diff()}, plus {ExtraDamage:diff()} per [gold]Faith[/gold])")]),
    card("JoanHolyLight", "성광", "Holy Light", [dmg_all, weak_all]),
    card("JoanUnyielding", "굴하지 않음", "Unyielding", [block, faith]),
    card("JoanMartyrdom", "순교", "Martyrdom", [T("체력을 {HpLoss} 잃습니다.", "Lose {HpLoss} HP."), faith]),
    card("JoanJudgment", "심판", "Judgment",
         [T("내 턴이 끝날 때 [gold]신념[/gold]만큼 모든 적에게 피해를 줍니다.",
            "At the end of your turn, deal damage equal to your [gold]Faith[/gold] to ALL enemies.")]),
    card("JoanTearTheAbjuration", "철회서를 찢다", "Tear the Abjuration",
         [T("피해를 {CalculatedDamage:diff()} 줍니다.\n(기본 {CalculationBase:diff()}, [gold]신념[/gold] 1당 {ExtraDamage:diff()} 더)",
            "Deal {CalculatedDamage:diff()} damage.\n({CalculationBase:diff()}, plus {ExtraDamage:diff()} per [gold]Faith[/gold])")]),
    card("JoanSainthood", "성인", "Sainthood", [T("[gold]신념[/gold]을 2배로 늘립니다.", "Double your [gold]Faith[/gold].")]),

    # ------------------------------------------------------------------ 레오니다스
    card("LeonidasShieldWall", "방패벽", "Shield Wall", [block]),
    card("LeonidasSpearThrust", "창 찌르기", "Spear Thrust", [dmg]),
    card("LeonidasHoplite", "호플리테스", "Hoplite", [block, counter]),
    card("LeonidasShieldBash", "방패 밀치기", "Shield Bash",
         [T("피해를 {CalculatedDamage:diff()} 줍니다.\n(현재 [gold]방어도[/gold]만큼)",
            "Deal {CalculatedDamage:diff()} damage.\n(Equal to your [gold]Block[/gold])")]),
    card("LeonidasNarrowPass", "협곡", "The Narrow Pass", [block]),
    card("LeonidasAgoge", "아고게", "Agoge", [block, draw]),
    card("LeonidasMassedThrust", "밀집 찌르기", "Massed Thrust", [dmg_hits]),
    card("LeonidasBrace", "버티기", "Brace", [block]),
    card("LeonidasPhalanx", "팔랑크스", "Phalanx",
         [T("[gold]방진[/gold]: [gold]방어도[/gold]가 내 턴이 시작될 때 사라지지 않습니다.",
            "[gold]Phalanx[/gold]: your [gold]Block[/gold] is not removed at the start of your turn.")]),
    card("LeonidasBronzeSpikes", "청동 가시", "Bronze Spikes", [counter]),
    card("LeonidasCounterattack", "역습", "Counterattack",
         [dmg, T("[gold]방어도[/gold]가 {BlockNeeded} 이상이면 2번 줍니다.",
                 "If you have {BlockNeeded} or more [gold]Block[/gold], hit twice.")]),
    card("LeonidasThreeHundred", "300", "The Three Hundred", [dmg_all, block]),
    card("LeonidasWithYourShield", "방패를 들고 오라", "With Your Shield",
         [block, T("다음 턴에 [gold]방어도[/gold]를 {BlockNextTurnPower:diff()} 얻습니다.",
                   "Next turn, gain {BlockNextTurnPower:diff()} [gold]Block[/gold].")]),
    card("LeonidasHotGates", "뜨거운 문", "The Hot Gates", [weak_all, block]),
    card("LeonidasInTheShade", "그늘에서 싸우리라", "We Shall Fight in the Shade",
         [block, T("[gold]흐릿함[/gold]을 {BlurPower:diff()} 얻습니다.", "Gain {BlurPower:diff()} [gold]Blur[/gold].")]),
    card("LeonidasLastStand", "최후의 방진", "Last Stand",
         [T("현재 [gold]방어도[/gold]만큼 [gold]방어도[/gold]를 얻습니다.", "Gain [gold]Block[/gold] equal to your current [gold]Block[/gold].")]),
    card("LeonidasMolonLabe", "와서 가져가라", "Come and Take Them",
         [T("내 턴이 끝날 때 [gold]방어도[/gold]의 절반만큼 모든 적에게 피해를 줍니다.",
            "At the end of your turn, deal damage equal to half your [gold]Block[/gold] to ALL enemies.")]),
    card("LeonidasSacrifice", "희생", "Sacrifice", [T("체력을 {HpLoss} 잃습니다.", "Lose {HpLoss} HP."), block, counter]),
    card("LeonidasKingsSpear", "왕의 창", "The King's Spear",
         [T("피해를 {CalculatedDamage:diff()} 줍니다.\n(기본 {CalculationBase:diff()} + 현재 [gold]방어도[/gold])",
            "Deal {CalculatedDamage:diff()} damage.\n({CalculationBase:diff()} + your [gold]Block[/gold])")]),
    card("LeonidasThermopylae", "테르모필레", "Thermopylae",
         [T("스킬 카드를 쓸 때마다 무작위 적에게 피해를 {LeonidasThermopylaePower:diff()} 줍니다.",
            "Whenever you play a Skill, deal {LeonidasThermopylaePower:diff()} damage to a random enemy.")]),

    # ------------------------------------------------------------------ 칭기즈 칸
    card("GenghisMountedArcher", "기사", "Mounted Archer", [dmg_hits]),
    card("GenghisCavalryCharge", "기마 돌격", "Cavalry Charge", [dmg_all]),
    card("GenghisVolley", "일제 사격", "Volley", [dmg_all_hits]),
    card("GenghisSteppeWind", "초원의 바람", "Steppe Wind", [draw, block]),
    card("GenghisFeignedRetreat", "거짓 퇴각", "Feigned Retreat", [block, vuln_t]),
    card("GenghisLancer", "창기병", "Lancer", [dmg]),
    card("GenghisRemount", "갈아타기", "Remount", [energy]),
    card("GenghisScout", "척후", "Scout", [dmg, draw]),
    card("GenghisHorde", "기마 군단", "The Horde",
         [T("내 턴이 시작될 때마다 모든 적에게 피해를 {GenghisHordePower:diff()} 줍니다. 그 뒤 피해가 1 늘어납니다.",
            "At the start of your turn, deal {GenghisHordePower:diff()} damage to ALL enemies, then increase it by 1.")]),
    card("GenghisConquest", "정복", "Conquest",
         [T("적이 쓰러질 때마다 에너지를 {GenghisConquestPower:diff()} 얻고 카드를 {GenghisConquestPower:diff()}장 뽑습니다.",
            "Whenever an enemy dies, gain {GenghisConquestPower:diff()} energy and draw {GenghisConquestPower:diff()} card(s).")]),
    card("GenghisSiege", "공성", "Siege", [dmg]),
    card("GenghisArrowRain", "화살비", "Rain of Arrows", [dmg_all_hits]),
    card("GenghisPlunder", "약탈", "Plunder",
         [dmg, T("이 공격으로 적을 쓰러뜨리면 [gold]골드[/gold]를 {Gold} 얻습니다.", "If this kills the enemy, gain {Gold} [gold]Gold[/gold].")]),
    card("GenghisTerror", "공포", "Terror", [weak_all, vuln_all]),
    card("GenghisKhansDecree", "칸의 명령", "Khan's Decree", [strength]),
    card("GenghisFalconry", "매 사냥", "Falconry", [dmg_hits, draw]),
    card("GenghisGreatMongol", "대몽골", "The Great Mongol Nation",
         [T("공격 카드를 쓸 때마다 모든 적에게 피해를 {GenghisEmpirePower:diff()} 줍니다.",
            "Whenever you play an Attack, deal {GenghisEmpirePower:diff()} damage to ALL enemies.")]),
    card("GenghisGreatKhan", "대칸", "The Great Khan", [dmg_all_hits]),
    card("GenghisKurultai", "쿠릴타이", "Kurultai", [energy, draw]),
    card("GenghisEndlessCampaign", "끝없는 원정", "Endless Campaign",
         [T("[gold]기마 군단[/gold]을 {GenghisHordePower:diff()} 얻습니다.", "Gain {GenghisHordePower:diff()} [gold]Horde[/gold].")]),

    # ------------------------------------------------------------------ 이순신
    card("YiCheonjaCannon", "천자총통", "Cheonja Cannon", [cannon]),
    card("YiPanokseon", "판옥선", "Panokseon", [block]),
    card("YiBroadside", "일제 포격", "Broadside", [dmg_all]),
    card("YiFireArrow", "화전", "Fire Arrow", [dmg, vuln_t]),
    card("YiWarDiary", "난중일기", "War Diary", [draw]),
    card("YiWatch", "경계", "On Watch",
         [block, T("다음 턴에 카드를 {DrawCardsNextTurnPower:diff()}장 더 뽑습니다.", "Next turn, draw {DrawCardsNextTurnPower:diff()} more card(s).")]),
    card("YiPrepareToSail", "출정 준비", "Prepare to Sail",
         [block, T("다음 턴에 에너지를 {EnergyNextTurnPower:diff()} 얻습니다.", "Next turn, gain {EnergyNextTurnPower:diff()} energy.")]),
    card("YiReload", "장전", "Reload", [cannon]),
    card("YiTurtleShip", "거북선", "Turtle Ship",
         [T("내 턴이 끝날 때마다 [gold]방어도[/gold]를 {YiTurtleShipPower:diff()} 얻습니다.",
            "At the end of your turn, gain {YiTurtleShipPower:diff()} [gold]Block[/gold].")]),
    card("YiCraneWing", "학익진", "Crane Wing Formation",
         [T("내 턴이 끝날 때 남은 에너지 1당 모든 적에게 피해를 {YiCraneWingPower:diff()} 줍니다.",
            "At the end of your turn, deal {YiCraneWingPower:diff()} damage to ALL enemies for each unspent energy.")]),
    card("YiMyeongnyang", "명량", "Myeongnyang",
         [T("피해를 {CalculatedDamage:diff()} 줍니다.\n(기본 {CalculationBase:diff()}, 적 1명당 {ExtraDamage:diff()} 더)",
            "Deal {CalculatedDamage:diff()} damage.\n({CalculationBase:diff()}, plus {ExtraDamage:diff()} per enemy)")]),
    card("YiOkpo", "옥포", "Okpo", [dmg, cannon]),
    card("YiHansando", "한산도", "Hansando", [cannon, block]),
    card("YiServeAsCommoner", "백의종군", "Serving as a Common Soldier", [block]),
    card("YiIronChain", "쇠사슬", "Iron Chain", [weak_all, cannon]),
    card("YiResolveToDie", "필사즉생", "Resolve to Die", [strength]),
    card("YiNoryang", "노량", "Noryang", [dmg_all, cannon]),
    card("YiTwelveShips", "열두 척", "Twelve Ships",
         [T("[gold]방어도[/gold]를 {CalculatedBlock:diff()} 얻습니다.\n(적 1명당 {CalculationExtra:diff()})",
            "Gain {CalculatedBlock:diff()} [gold]Block[/gold].\n({CalculationExtra:diff()} per enemy)"), cannon]),
    card("YiCommander", "통제사", "Naval Commander",
         [T("내 턴이 시작될 때마다 [gold]포격[/gold]을 {YiCommanderPower:diff()} 얻습니다.",
            "At the start of your turn, gain {YiCommanderPower:diff()} [gold]Bombard[/gold].")]),
    card("YiDoNotAnnounce", "나의 죽음을 알리지 마라", "Do Not Announce My Death",
         [T("[gold]포격[/gold]을 2배로 늘립니다.", "Double your [gold]Bombard[/gold].")]),

    # ------------------------------------------------------------------ 나폴레옹
    card("NapoleonCannon", "대포", "Cannon", [dmg]),
    card("NapoleonOrders", "명령", "Orders",
         [draw, T("다음 턴에 에너지를 {EnergyNextTurnPower:diff()} 얻습니다.", "Next turn, gain {EnergyNextTurnPower:diff()} energy.")]),
    card("NapoleonBayonet", "총검", "Bayonet", [dmg]),
    card("NapoleonGrapeshot", "산탄", "Grapeshot", [dmg_all]),
    card("NapoleonForcedMarch", "강행군", "Forced March", [draw]),
    card("NapoleonInfantrySquare", "방진", "Infantry Square", [block]),
    card("NapoleonCivilCode", "법전", "Civil Code", [block, draw]),
    card("NapoleonCuirassiers", "흉갑 기병", "Cuirassiers", [dmg_hits]),
    card("NapoleonArtillery", "포병대", "Artillery Corps",
         [T("내 턴이 시작될 때마다 무작위 적에게 피해를 {NapoleonArtilleryPower:diff()} 줍니다.",
            "At the start of your turn, deal {NapoleonArtilleryPower:diff()} damage to a random enemy.")]),
    card("NapoleonBlitz", "전격전", "Lightning Campaign",
         [T("한 턴에 카드를 4장 쓸 때마다 에너지를 {NapoleonBlitzPower:diff()} 얻습니다.",
            "Every 4th card you play in a turn gives {NapoleonBlitzPower:diff()} energy.")]),
    card("NapoleonAusterlitz", "아우스터리츠", "Austerlitz", [dmg, weak_t]),
    card("NapoleonGrandBattery", "대포대", "Grand Battery", [dmg_all]),
    card("NapoleonManeuver", "기동", "Maneuver", [block, draw]),
    card("NapoleonImperialGuard", "근위대", "Imperial Guard", [block]),
    card("NapoleonAmbition", "야망", "Ambition", [energy]),
    card("NapoleonConscription", "징병", "Conscription", [strength, draw]),
    card("NapoleonCoronation", "대관식", "Coronation",
         [T("최대 에너지가 {NapoleonCoronationPower:diff()} 늘어납니다.", "Gain {NapoleonCoronationPower:diff()} max energy.")]),
    card("NapoleonHundredDays", "백일천하", "The Hundred Days", [energy, draw]),
    card("NapoleonGrandeArmee", "대육군", "La Grande Armée", [dmg_hits]),
    card("NapoleonNothingImpossible", "불가능은 없다", "Nothing Is Impossible",
         [T("이번 턴에 다음에 쓰는 카드가 한 번 더 발동합니다.", "This turn, your next card is played twice.")]),
]
