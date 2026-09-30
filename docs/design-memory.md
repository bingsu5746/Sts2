# Slay the Spire 2 모드 프로젝트 — 메모리 전체 내보내기 (최종본)

> 모든 항목은 저장된 메모리 파일 원문 기준. 메모리에는 항목별 날짜가 기록되어 있지 않아 날짜는 모두 [unknown] (파일 최종 수정: 2026-09-14 ~ 2026-09-16).
> 원본 파일: index.md, overview.md, design-decisions.md, wuxia-character.md, existing-mod-analysis.md, progress-power-pattern.md, sourcecode-examples.md, additional-mod-analysis-3.md

---

# PART 1. 프로젝트 개요

- [unknown] - Slay The Spire — 그리스 신화 "만신전" 사제 캐릭터 + 무협지 테마 캐릭터 + (예정) 레골라스풍 궁수 캐릭터, 세 개의 플레이어블 캐릭터를 만드는 STS2 모드 프로젝트

---

# PART 2. 캐릭터 1 — 만신전(Pantheon) 사제

## 컨셉
- [unknown] - 성연우 is developing a Slay the Spire (STS) mod adding a new playable character built around a priest/cleric concept themed on a "Pantheon" (만신전) — a character who channels multiple gods rather than serving a single deity
- [unknown] - The project is in the active design/planning phase; the focus is nailing down the core mechanical identity before implementation
- [unknown] - Setting is Greek mythology only

## God roster — 확정
- [unknown] - 확정된 5명 신: 제우스(Zeus), 포세이돈(Poseidon), 하데스(Hades), 아테나(Athena), 아르테미스(Artemis)

## Core mechanic — Affection Meter (애정도)
- [unknown] - Each of the five gods has an Affection Meter (애정도) ranging from 0–10
- [unknown] - Playing a god's card increases that god's affection by +1 per card played
- [unknown] - At the start of each turn, all gods' affection decreases by -1
- [unknown] - Affection reaching 10 triggers a blessing state; affection hitting 0 triggers a curse state
- [unknown] - The specific mechanical effects of blessing and curse states are deferred for later design work
- [unknown] - A special condition — one god at 10 while all others are at 0 — was flagged as potentially deserving unique additional effects; also deferred

## Card structure
- [unknown] - Shared "common cards" cover basic attack/defense/utility across all gods
- [unknown] - Each god has unique cards providing stronger, specialized versions aligned to their mythological traits
- [unknown] - All gods are treated as having attack/utility cards rather than strict role archetypes

## 각 신의 특징 및 무기
- [unknown] - 제우스: 주관 하늘, 번개, 권력, 신들의 왕 / 무기: 아스트라페(번개 - 최강의 무기), 아이기스(방패)
- [unknown] - 포세이돈: 주관 바다, 지진, 물 / 무기: 트라이던트(삼지창 - 파도와 지진 일으킴)
- [unknown] - 하데스: 주관 저승, 죽음 / 무기: 퀘네에(투명 투구 - 투명 효과)
- [unknown] - 아테나: 주관 지혜, 전쟁(전략/방어형), 직물, 문명 / 무기: 창, 방패(아이기스), 메두사의 머리가 달린 방패 / 특징: 총명하고 이성적이며 순결함, 방어적인 전쟁 주관, 영웅들의 수호신
- [unknown] - 아르테미스: 주관 달, 사냥, 야생동물, 처녀성 / 무기: 활과 화살 / 특징: 야생적이고 냉철한 사냥의 여신, 결혼하지 않기를 택함, 아폴론의 쌍둥이 누나

## Development workflow
- [unknown] - Plan: analyze existing famous STS mods (StS1, StS2) first to understand structure and best practices
- [unknown] - 성연우 will provide actual mod files for analysis
- [unknown] - Then collaborate on building the Pantheon mod using those learnings

## Current state — next design steps
- [unknown] - 축복/저주의 구체적 효과 결정 필요 (퍼센트 아닌 구체적 숫자로)
- [unknown] - 한 신만 10, 나머지 모두 0일 때 추가 버프/디버프 여부 결정
- [unknown] - 각 신별 카드 풀 설계
- [unknown] - 캐릭터 이름/배경/HP/시작 유물 설계
- [unknown] - 신 애정도 UI 구현 방식 확정

## 설계 결정 (기각된 방향과 이유)
- [unknown] - Stance-switching was rejected due to excessive similarity to the Watcher — the character needs a distinct identity
- [unknown] - A divine mandate system (gods issuing turn commands) was rejected as unfun — player agency should not be constrained by randomized god instructions
- [unknown] - The Affection Meter emerged as the preferred core system because it rewards focused god-synergy play while creating natural tension through the per-turn decay mechanic
- [unknown] - Greek-only mythology was chosen for thematic coherence over a broader multi-mythology approach; a multi-mythology approach was considered and rejected
- [unknown] - All gods being attack/utility capable — rather than siloed into roles like "tank god" or "damage god" — was a deliberate design choice to avoid over-constraining deck-building

---

# PART 3. 캐릭터 2 — 무협(Wuxia)

## 컨셉
- [unknown] - 그리스 신화 만신전 캐릭터와는 별개의, 같은 STS2 모드 프로젝트 내 두 번째 캐릭터
- [unknown] - 컨셉: 모든 무공의 이치는 알지만 아직 완전히 깨우치지 못한 존재. 여러 무공 루트(가문/문파)를 자유롭게 탈 수 있는 잠재력형 캐릭터
- [unknown] - 시작 시점엔 삼류 검법/도법 등 기초 카드만 보유

## 핵심 자원: 내공 (Energy와 별개의 리젠트 스타일 자원)
- [unknown] - 내공 코스트가 있는 고급 무공 카드는 내공 총량 자체가 늘어야 사용 가능
- [unknown] - 내공 요구량이 높은 카드일수록 강력함 (비용-파워 스케일링)
- [unknown] - 내공은 자동 회복 없음 — 전투 중에도, 전투 후에도 자연 회복 안 되는 방향 확정
- [unknown] - 회복 수단은 "운기조식" 카드로만 — 자원 관리의 핵심 축
- [unknown] - 총량 증가 수단: 무협 상점의 영약, 특정 조건 클리어, 특정 유물(무협 영약류) 섭취
- [unknown] - 포션 아이디어: 전투 중 임시로 내공을 채워주는 포션 / 내공 총량을 이번 전투에서만 임시로 늘려주는 포션

## 비급서(무공서) 획득 시스템 — 3가지 방식 (모두 채택 예정, 코딩은 나중에)
- [unknown] - 방식 1 즉시 세트 지급: "소림 비급서" 카드를 얻으면 즉시 소림 관련 카드들이 전부 한 장씩 손에 들어옴. 확정적이고 즉각적인 파워업 — 상점의 "확정 구매" 고가 아이템으로 적합
- [unknown] - 방식 2 카드팩 선택창(3택1): 몬스터 처치 후 카드 3장 중 1장 고르는 것과 같은 방식의 선택 화면. "비급서 = 그 문파 입문, 그 중 하나를 골라 익힘" 컨셉. 저렴한 랜덤 비급이나 이벤트 보상에 적합
- [unknown] - 방식 3 숙련도 기반 승급: 특정 카드를 정해진 횟수(예: 10번) 사용하면 전투 종료 후 보상 화면에 그 무공의 상위 무공이 추가로 등장. 무협지의 "숙련을 통한 깨달음/초월" 컨셉. 카드 자체의 성장 시스템 — 상점/이벤트와 별개로 항상 백그라운드에서 작동

## 무협 전용 상점 개조
- [unknown] - 상점을 무협 테마로 완전히 교체: 상인 비주얼, 배경, 판매 카드 모두 무협 테마
- [unknown] - 일반(비무협) 카드는 상점에서 안 팔리게 — 캐릭터 전용 슬롯(Atk/Atk/Skill/Skill/Power)은 원래 그 캐릭터의 카드풀에서만 뽑히므로 무협 카드로 자동 채워짐
- [unknown] - 컬러리스 카드 2슬롯(Uncommon 1 + Rare 1)을 무협 비급서 전용 슬롯으로 교체하는 방향으로 결정 — 여기서 비급서 획득 3방식 중 하나(또는 조합)가 나오게 함
- [unknown] - 유물 슬롯도 더 많이 팔리도록(현재 기본 2~3개에서 증설) 조정, 대부분 영약/법보 계열로 채움

## 3막 구조 개편 아이디어
- [unknown] - 3막 에인션트(선조) 존재 만남 이벤트를 "무림맹 대서고" 이벤트로 교체하는 아이디어
- [unknown] - STS2의 다리 이벤트(체력 잃는 대신 위 선택지가 바뀌고, 그 선택지를 누르면 카드가 사라지는 방식) 오마주 — 랜덤 비급서의 선택지가 바뀌는 이벤트 버전
- [unknown] - 또는 니오우 이벤트처럼 3가지 비급서 중 선택하는 방식도 후보
- [unknown] - 또는 나침반 유물처럼 "가문을 선택하면 3막이 그 가문 루트로 고정되어 변화"하는 방식도 후보 — 아직 미확정

## 콘텐츠 확장 여지 / 진행 상태
- [unknown] - 무협지에 나오는 다양한 가문/문파와 다양한 기술, 유물들을 카드/유물 소스로 활용 예정
- [unknown] - 현재 모든 내용은 아이디어 단계이며, "이렇게 하겠다"는 확정이 아니라 방향성 브레인스토밍. 실제 코딩/상세 설계는 추후 진행 예정

## 문파 목록 (최종 확정) — 10개
- [unknown] - 1. 소림사(少林寺) — 정파·불가, 방어/탱킹. 금강불괴신공, 소림 칠십이예, 항마 계열(사술·마공 대항). 메커닉: 방어/반격, 디버프 면역
- [unknown] - 2. 무당파(武當派) — 정파·도가, 내공 순환. 태극권/태극혜검, 이유극강. 메커닉: 내공 자원을 직접 다루는 것 자체가 정체성("내공 시스템의 본가")
- [unknown] - 3. 화산파(華山派) — 정파·도가, 쾌검. 매화검법, 자하신공. 연타/다단히트, 저비용 다회 사용, 상태이상(도트) 축적
- [unknown] - 4. 종남파(終南派) — 정파·도가, 중검. 천하삼십육검. 화산과 대비. 고비용 고데미지 단발, 혹은 "다음 턴 예약/준비형" 메커닉과 결합 가능
- [unknown] - 5. 사천당가(四川唐家) — 세가, 암기/독. 만천화우(암기 난사), 칠보추혼(독). 독/암기 투척형 스택 디버프
- [unknown] - 6. 제갈세가(諸葛世家) — 세가, 진법/기관. 신기제갈, 기관진식·진법·용인술·전술에 도통. 무공 수위 자체는 낮은 편이나 편법 무공 발달(지법·곤법 등), 암기술에도 해박. 대표 무공: 대천성신공, 현원전단신공, 소천성검법, 천기미리보(보법). 메커닉: 장판/지속효과, 트리거형 함정, 저비용 다회용 카드
- [unknown] - 7. 남궁세가(南宮世家) — 세가, 순수 검술. 창궁검법, 제왕검형. 단일 대상 고데미지 극대화
- [unknown] - 8. 하북팽가(河北彭家) — 세가, 도법. 도(칼)를 쓰는 대표 가문, 타고난 신력과 근골, 호쾌·우직·패도적. 남궁세가와 구분 위해 광역 또는 저확률 고배율(패도적 도박성 딜)
- [unknown] - 9. 천마신교(天魔神敎) — 사교, 흡수형. 흡성대법, 천마신법, 천마군림보, 천마검법, 혈마도. 상대 자원(체력/버프/내공)을 빼앗아 자신의 내공으로 전환
- [unknown] - 10. 북해빙궁(北海氷宮) — 새외무림, 빙결. 빙백신장(열혈강호로 한국에서 가장 유명), 빙백신공(심법), 빙결쇄권, 한빙면장, 한음지. 빙정(氷精)·만년설삼 등 음기 영약 → 내공 증진 아이템 테마. 메커닉: 빙결/둔화 디버프

---

# PART 4. 캐릭터 3 — 궁수

- [unknown] - (예정) 레골라스풍 궁수 캐릭터.

## 방향성 (2026-09-30 논의)
- [2026-09-30] - 살릴 레골라스 요소: 속사/연사, 곡예/회피, 근접 겸용 (엘프/자연 테마는 선택 안 함)
- [2026-09-30] - 기존 궁수 모드 TheArrow(5-5)와의 차별화는 크게 신경 쓰지 않음 — 참고만
- [2026-09-30] - 핵심 메커니즘 후보 10개 중 3개 채택 (세부 수치·조합 방식은 미정):
  - ① 화살통 + 쌍검 전환: 화살 N발 소모, 떨어지면 카드가 근접(쌍검) 버전으로 작동, 곡예/회수 카드로 재충전
  - ④ 시위(드로우 차지): 사격 카드를 즉시 쏘거나 손에 "당겨 두고" 턴을 넘겨 다음 턴에 강화 사격
  - ⑦ 화살 종류 장전: 불/얼음/관통 등 특수 화살을 장전하면 다음 사격 카드에 속성 부여 
- [2026-09-30] - 확정: 쌍검 전환은 카드 한 장에 원거리/근접 효과를 둘 다 적는 방식 (화살 있으면 원거리, 0이면 근접 효과)
- [2026-09-30] - 확정: 특수 화살 6종 — 불, 관통, 전기, 얼음, 폭발, 산탄(여러 발로 갈라져 박히는 화살) (세부 효과 미정)
- [2026-09-30] - 확정: 전투 중 화살은 회수로만 충전, 전투 시작 시 화살통 가득 참, 회수 경로 다양화(곡예 카드 화살+1, 쌍검 처치 시 회수, 회수 유물)
- [2026-09-30] - 확정: 화살통 기본 5발 (유물/파워로 최대치 증가 가능). 원거리 우위 X(1발당 근접 대비 추가 피해)는 +2~3 제안 상태, 미확정
- [2026-09-30] - 확정: 다른 캐릭터(무협 내공·북해빙궁 빙결 등)와 메커니즘이 겹치는 건 상관없음
- [2026-09-30] - 기각/보류된 후보: 흐름(콤보), 사거리(와처 스탠스와 유사 — 만신전 때 기각 기준에 해당), 회피 반격, 연사 카운트, 표식 사냥(아르테미스와 테마 중복 우려), 기동력 포인트, 근접→원거리 연계

---

# PART 5. 기존 모드 분석 (전체)

## 5-1. STS1 vs STS2 모딩 생태계
| 항목 | STS1 | STS2 |
|-----|------|------|
| 엔진 | LibGDX (Java) | Godot (C#) |
| 모딩 방식 | Java 역어셈블리, MTS(ModTheSpire) | 네이티브 Godot, Steam Workshop |
| 호환성 | 완전히 호환 불가 | 새로 작성 필요 |
| 그래픽 | 2D (hand-drawn) | 3D |

- [unknown] - STS1 모드는 STS2에서 절대 작동 불가. Downfall, Witch 등 인기 모드도 완전히 새로 작성해야 함. 모드 제작자들이 수동 포팅 중 (예: Minty Spire → Minty Spire 2, Watcher)
- [unknown] - STS1: ModTheSpire 런처, 수동 폴더 관리, BaseMod/MTS/Java Reflection
- [unknown] - STS2: Steam Workshop 네이티브 지원, 원클릭 구독, 내장 Mod Manager, 버전 관리 자동화(v0.107.1, v0.110.0 등). BaseLib, Godot 네이티브 모딩, C# + PCK 패킹, Steam Workshop API
- [unknown] - STS2 메타데이터: JSON 매니페스트(id, name, author, version, dependencies), Workshop 자동 등록, 의존성 관리, 게임 버전 호환성 선언
- [unknown] - 빌드 & 배포: (1) .NET 8 SDK로 C# 컴파일 → DLL (2) Godot 4.5.1 Mono에서 리소스 → PCK (3) JSON 매니페스트 작성 (4) Steam Workshop 또는 수동 배포

## 5-2. Watcher (STS2 포팅)
- [unknown] - 파일: Watcher.json(메타데이터), Watcher.dll(224KB), Watcher.pck(18MB)
- [unknown] - Watcher.json: id "Watcher", author "lamali", version "1.5.10", min_game_version "0.107.1", dependencies BaseLib min_version "3.4.5", affects_gameplay true
- [unknown] - STS1 캐릭터를 STS2로 포팅. 다국어 지원(한/중/러/일/이), 스탠스별 커스텀 VFX/SFX. C#(Mono/.NET), Godot, BaseLib v3.4.5+

## 5-3. The Cursed
- [unknown] - DLL 191KB, PCK 59MB
- [unknown] - Curse 시스템(GainRandomCurse, GetCurseCards), Rite 시스템(OnRiteEffect, ExecuteRiteEffect), Circle 시스템(OnCircleTrigger, OnCircleEffect), 카드 플레이 훅과 파워 연동
- [unknown] - 설계 철학: 누적 효과 기반(저주 카드 누적 → 의식 발동 → 이펙트). Watcher 스탠스와 다른 다층적 상호작용

## 5-4. BaseLib 핵심 API 구조 (TheBomber 모드에서 발견)
- [unknown] - BaseLib 원작자 Alchyr, 버전 v0.1.2, "Modding utility for Slay the Spire 2", affects_gameplay false(순수 유틸리티)
- [unknown] - 게임 코어 네임스페이스(MegaCrit.Sts2.Core): Models(CardModel, PowerModel, RelicModel, CharacterModel, PotionModel, CardPoolModel), Entities.Cards(CardPile, PileTypeExtensions, CardKeywordOrder), Entities.Players(PlayerCombatState), Nodes.Cards(NCard), Nodes.Combat(NEnergyCounter), Hooks, CardSelection, Runs(RunManager), Saves(SaveManager, ProgressSaveManager), Localization(DynamicVars, Formatters), Random, Combat.History
- [unknown] - BaseLib Custom* 확장 포인트: CustomCardModel, CustomCharacterModel, CustomPowerModel, CustomRelicModel, CustomPotionModel / CustomCardFrame, CustomCardPortraitPath / CustomCharacterVisuals, CustomCharacterVisualPath / CustomAncients, CustomAncientModel / CustomKeywords / CustomEnums, CustomEnumAttribute / CustomPileProviders, CustomPilePosition / CustomSharedPools, CustomCardPoolModel, CustomRelicPoolModel, CustomPotionPoolModel / CustomStarterUpgrade / CustomModelCounts / CustomAnimator
- [unknown] - BaseLib 유틸리티: BaseLib.Config / Config.UI(NConfigButton, NConfigDropdown, NConfigTickbox, NModConfigPopup), BaseLib.Utils.CommonActions(Draw, Apply, CardBlock, SelectCards, SelectSingleCard), BaseLib.Patches.Features(RefundPatch 등), Patches.Content / UI / Compatibility

## 5-5. TheArrow ("STS2 Character Creator"로 제작)
- [unknown] - 원작자 yamoyima, 1.0.0, "Made with the STS2 Character Creator" — 별도 캐릭터 제작 도구 존재. BaseLib v3.4.1+
- [unknown] - 파일: TheArrow.dll(9KB, 진입점만), TheArrow.pck(37MB), main/TheArrow.Gameplay.main.dll(220KB), beta/TheArrow.Gameplay.beta.dll(220KB) — 브랜치별(main/beta) DLL 분리로 버전 호환성 관리
- [unknown] - 네임스페이스: CardPools, Cards(40개+: RedAura, TwinShot, ArrowRain, MegaShot, PowerShot 등), Characters, Commands, Nodes, Patches, PotionPools/Potions, Powers(QuickArrowPower, HeavyArrowPower, RedShieldPower, StrengthMarkPower, WellPreparedPower 등), RelicPools/Relics, TokenCards
- [unknown] - 파워 훅: AfterCardDrawn, AfterCardPlayed, AfterDamageGiven, AfterDamageReceived, AfterBlockBroken, AfterPlayerTurnStart, AfterSideTurnEnd, AfterCurrentHpChanged, AfterPowerAmountChanged(애정도 변화 감지에 핵심 참고)

## 5-6. Heathcliff Wild Hunt (림버스 컴퍼니)
- [unknown] - 원작자 kiteseven, 0.0.1. "광란의 사냥 히스클리프: 12장 전용 카드 + 두라한 폼 + 관/이성/추종자 메커닉". PCK 263MB, .pdb 포함
- [unknown] - 이성(Sanity) 게이지 — 애정도와 매우 유사: SanityPower, Drain/Restore, IsAtBottomSanity, highSanity, BaseSanityRestore, SanityDrainBase, DrainPerHit, DrainUp, DrainUpgrade, RestoreUpgrade, OnAttackBlockedDrain, OnTakeDamageDrain
- [unknown] - 두라한 폼: DullahanPower, SyncStatBuffs, AfterDamageGiven, FromDeckForTransformationWithoutEgo, RunToDeathDullahan, OhDullahan
- [unknown] - 관(Coffin): CoffinPower(Gain/Consume, AfterCardPlayed, BeforeTurnEnd). 카드: CoffinNail, CoffinOath, CoffinBurst, CoffinWater, EmptyCoffin, CoffinHammer, CoffinSearch, CoffinBarrier, CoffinCollapse, CoffinWhisper, IntoThisCoffin, SanityForge
- [unknown] - 3중 자원(이성+관+두라한 변신). 게이지가 임계값(바닥/최고)에 도달하면 상태가 바뀌는 패턴 — 애정도와 가장 유사한 사례

## 5-7. Painter (화가)
- [unknown] - 원작자 Hope, 0.32, DLL 413KB(최대 코드베이스), BaseLib
- [unknown] - 네임스페이스: Cards.Copy/Cry/Ink/Mark/Mock/Peel/Rip/Skip, Character, Powers, Relics, Potions, Keywords, Tags, Scripts, Utils, Extensions
- [unknown] - PenStroke 카운터: PenStrokeCount, PenStrokeData, TriggerPenStroke, OnPenStroke, OnPenStrokePerformed, ApplyPenStrokeEffect, ApplyExistingPenStroke, DoubleExistingPenStrokes, PenStrokeSelectionPrompt, GetPenStrokeTitle, BuildPenStrokeHoverTips, IsRemovablePenStroke, RemovePenStrokeEffect, RemoveExtraPenStrokeEffect, CanApplyExtraPenStrokeEffect, GetRequestedExtraPenStrokeEffects, ExtraPenStrokeEffectProvider, PenStrokeCostRecalculated
- [unknown] - PenStrokeTag: HasPenStrokeTag, GetAppliedPenStrokeTags, GetDisplayedPenStrokeTags, GetSerializedPenStrokeTagCounts, RestoreSerializedPenStrokeTagCounts, PainterSavedPenStrokeTags(세이브 유지)
- [unknown] - Ink: InkPool, RichInk, ThickInk, BloodInk, SpotInk, SplatterInk, InkShield, InkAccumulation, WithBloodAsInk, InkAccumulationPower(AfterApplied/AfterRemoved)
- [unknown] - CreativeForm: CreativeFormPower, GivePenCard, AfterCardPlayed, AfterSideTurnStart
- [unknown] - 기타 파워: CyclePower, RecyclePower(AfterCardExhausted), PridePower, SolitaryPower, TrainingPower, EmpathyPower, ChromophiliaPower, ManicDepressivePower, PsychosisPower(AfterPlayerTurnStart), MobiusStripPower, EndlessCanvasPower, WhiteSpacePower, PerfectionismPower(OnStatApplied), PreparationPower(BeforeCardPlayed — 플레이 "전" 개입)
- [unknown] - 설계 철학: 태그 붙은 이벤트를 집계하고 집계값에 따라 효과 파생. 신별 카드 플레이 횟수를 태그 카운트로 저장하는 패턴 참고 가능

## 5-8. 초기 애정도 구현 참고 패턴 (위 모드 종합)
- [unknown] - Heathcliff SanityPower처럼 신마다 독립 Power 클래스(ZeusAffectionPower 등 5개)
- [unknown] - IsAtMaxAffection(10) / IsAtZeroAffection(0) 판정 프로퍼티
- [unknown] - AfterCardPlayed로 +1, AfterPlayerTurnStart로 모든 신 -1, AfterPowerAmountChanged로 UI 업데이트 및 축복/저주 트리거 감지
- [unknown] - TheArrow처럼 Cards/Powers/Relics/CardPools 분리, 신별 하위 네임스페이스(Cards.Zeus 등) 고려
- [unknown] - "STS2 Character Creator" 도구 찾아 활용 검토 필요
- [unknown] - Watcher 스탠스처럼 신당 0~10 상태 관리 + The Cursed Rite/Circle처럼 트리거(10 축복, 0 저주)
- [unknown] - 카드 풀: 공용(애정도 무관) / 신별 고유(애정도 영향) / 특수(축복/저주만)

## 5-9. BaseLib v3.3.8 (최신) — CustomResource API
- [unknown] - DLL 991KB(구버전 155KB), min_game_version 0.107.1
- [unknown] - CustomResource(제네릭 Spend), CustomResources`1(여러 자원 컬렉션 — 5신 애정도에 부합), CustomResourceCost`1, ICustomResourceCost, CustomResourceUiPatches, ICustomResourceVisualsHandler, BasicCustomResource, AfterSpendCustomResource 훅, CustomResourcePatches.SpendAdditionalCosts
- [unknown] - 신규 카테고리: CustomOrbModel 계열(CreateSprite, IconPath, SpritePath, RandomPool), CustomEnchantmentModel, CustomModifierModel, CustomPetModel, CustomEventModel/CustomSharedEvents, CustomEncounterModel/Background, CustomMonsterModel, CustomActModel 계열(BackgroundScenePath, MapBot/Mid/TopBgPath, RestSiteBackgroundPath, TreasureChest), CustomCharacterSelectEntry/Context/ScreenState/SortPatch, CustomTemporaryPowerModel/Wrapper`2, CustomCalculatedVar/DamageVar/BlockVar/CustomExtraDamageVar, CustomBadgeTypes/IconPathDict, CustomLocTableManager/Patches, CustomTargetType/ValidTargets/TargetingAllowed/TargetValidityChecks/SingleTargets/ControllerTargeting/MouseTargetSelection, CustomRestSiteOption/IconPath, CustomRewardPatches, CustomTooltips, CustomKeyword/CustomKeywords
- [unknown] - 영향: CustomResources`1로 5신 통합 자원 관리가 더 정석일 수 있음. ICustomResourceVisualsHandler로 게이지 시각화, CustomCalculatedVar로 애정도 따라 카드 텍스트 수치 자동 변경, CustomCharacterSelectEntry로 선택 화면 등록
- [unknown] - TODO: CustomResource/CustomResources`1 실제 사용 예시 모드 찾기

## 5-10. ModConfig
- [unknown] - 원작자 皮一下就很凡@Bilibili, 0.2.3, affects_gameplay false. 설정 화면에 "Mods" 탭 주입, 토글/슬라이더/드롭다운/단축키/텍스트/버튼/색상 선택기 등록 API. 애정도 감소 속도 등 유저 설정용 참고

## 5-11. STS2-RitsuLib (커뮤니티 프레임워크)
- [unknown] - 원작자 OLC, 0.5.20. "재사용 가능한 패칭, 영속성, 라이프사이클, 로컬라이제이션, 유틸리티 API를 제공하는 공유 모드 프레임워크". affects_gameplay false
- [unknown] - 게임 버전별(0.107.1, 0.109.0, 0.110.0, 0.111.0) DLL 각각 제공. 영/중 이중언어 XML API 문서(10만 줄+), viewer/index.html 포함
- [unknown] - 이벤트: CardPlayedEvent, CardPlayingEvent, CardDrawnEvent, CardDiscardedEvent, CardExhaustedEvent, BlockGainedEvent, BlockBrokenEvent, EnergyGainedEvent, EnergySpentEvent, CombatStartingEvent, CombatEndedEvent, PlayerTurnStartedEvent, SideTurnStartedEvent/EndedEvent, RelicObtainedEvent, PotionUsedEvent, CreatureDiedEvent, CurrentHpChangedEvent 등
- [unknown] - 하위 시스템: Cards, Relics, CardTags, CardPiles, Content, Audio(AudioAdaptiveMusicDirector, AudioAdaptiveMusicPlan), Unlocks(ModUnlockRegistry.RequireEpoch()), Timeline, Saves, RunData
- [unknown] - CardTags: ModCardTagRegistry(커스텀 카드 태그 등록/동적 CardTag 해석), CardTagRegistrationEntry, ModCardTagDefinition. CardTag는 게임 코어 공식 개념
- [unknown] - ModContentPackBuilder: fluent API, .For(modId) 시작, CardPileManifest() 등 체이닝, TimelineColumnPackEntry`1
- [unknown] - 콘텐츠 레지스트리: ModContentRegistry, ContentRegistrationState, ContentRegistryPhase, Patches.AllCardPoolsPatch/AllRelicsPatch/AllPowersPatch/AllOrbsPatch, CardLibraryCompendiumPlacementRule, CardLibraryCompendiumFilterInsertRelation
- [unknown] - 라이프사이클: FrameworkInitializing/Initialized, EssentialInitializationStarting/Completed, DeferredInitializationStarting/Completed, ContentRegistrationClosedEvent(이전에 모든 등록 완료 필요)
- [unknown] - 영향: ModCardTagRegistry로 "God_Zeus" 등 신별 태그 등록 가능. 이벤트 구독 방식이 Power 훅 오버라이드보다 유연할 수 있음. 버전별 DLL 분리 참고

## 5-12. Qu — RitsuLib 단독 사용 사례
- [unknown] - 원작자 RaoLong, v0.1.0, BaseLib 없이 STS2-RitsuLib(0.4.25)만 의존. PCK 219MB. Qu.QuCode.Cards.Commen, Qu.QuCode.Patch
- [unknown] - 사용: CardPiles.Nodes.NModCardPileButton, Combat.Rewards, Interactions.RightClick, Scaffolding.Characters / Characters.Visuals.Definition, IModCreatureVisualsFactory.TryCreateCreatureVisuals

## 5-13. RitsuLib 어트리뷰트 자동 등록(AutoRegistration)
- [unknown] - STS2RitsuLib.Interop.AutoRegistration: 클래스에 어트리뷰트만 붙이면 자동 등록. AttributeAutoRegistrationTypeDiscoveryContributor가 어셈블리 1회 스캔 → 결정론적 정렬 → 명시적 레지스트리 API 실행. 예: [RegisterOwnedCardPile] + IModCardPileHandler
- [unknown] - 캐릭터/카드: RegisterCharacter, RegisterCard, RegisterCharacterStarterCard/Potion/Relic, RegisterPower, RegisterRelic, RegisterPotion, RegisterOrb
- [unknown] - 태그/키워드: RegisterOwnedCardTag, RegisterOwnedCardKeyword, RegisterOwnedKeyword, RegisterOwnedCardPile, RegisterOwnedTopBarButton(애정도 상단바 표시 후보)
- [unknown] - 콘텐츠/월드: RegisterAct, RegisterActEncounter, RegisterActEvent, RegisterActAncient, RegisterMonster, RegisterAchievement, RegisterEnchantment, RegisterAffliction, RegisterSharedCardPool/RelicPool/PotionPool/Ancient/Event
- [unknown] - 진행/해금: RegisterEpoch, RegisterEpochCards, RegisterEpochRelicsFromPool, RequireEpoch, UnlockCharacterAfterRunAs, UnlockEpochAfterRunAs, UnlockEpochAfterWinAs, UnlockEpochAfterAscensionWin, UnlockEpochAfterBossVictories, UnlockEpochAfterEliteVictories
- [unknown] - 기타: RegisterSmartFormatSource, RegisterSmartFormatter(애정도 수치 카드 텍스트 반영), RegisterNodeAttachment 계열, RitsuLibOwnedBy
- [unknown] - 영향: 수동 등록보다 코드 간결. [RegisterCard] + [RegisterOwnedCardTag("God_Zeus")] 식 선언적 구현. BaseLib vs RitsuLib vs 병용 결정 필요

## 5-14. ⭐ TheQueen — 완전한 .cs 소스코드
- [unknown] - 네임스페이스 ComicChess.TheQueen, 여왕 테마, "긁기(Scratch)" 핵심. PCK 미컴파일 순수 .cs 포함, BaseLib+RitsuLib 병용(RitsuLib 번들), 중국어 주석 상세

### Entry.cs (진입점)
```csharp
using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace ComicChess.TheQueen;

[ModInitializer("Init")]
public class Entry
{
    public static void Init()
    {
        var harmony = new Harmony("sts2.comicchess.thequeen");
        harmony.PatchAll();
        ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly);
    }
}
```
- [unknown] - [ModInitializer("Init")] 문자열은 초기화 함수명과 일치. HarmonyLib harmony.PatchAll()이 게임 수정 핵심. ScriptManagerBridge.LookupScriptsInAssembly()로 스크립트 등록. Harmony ID는 타 모드와 충돌 금지

### QueenCardTags.cs (커스텀 카드 태그)
```csharp
using MegaCrit.Sts2.Core.Entities.Cards;

namespace ComicChess.TheQueen;

public static class QueenCardTags
{
    /// <summary>긁기 체계 공격 카드; ScratchTaggedCard와 함께 사용.</summary>
    public const CardTag Scratch = (CardTag)79;
}
```
- [unknown] - CardTag enum이 확장에 열려있지 않을 때 미사용 정수값(79) 캐스팅. "완벽한 타격"이 CardTag.Strike를 쓰는 방식에서 착안
- [unknown] - 만신전 적용: 80=Zeus, 81=Poseidon, 82=Hades, 83=Athena, 84=Artemis 식 할당 가능. RitsuLib ModCardTagRegistry와의 관계는 불명

### Scratch.cs (카드 예시)
```csharp
[Pool(typeof(QueenCardPool))]
public sealed class Scratch : ScratchTaggedCard
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(5m, ValueProp.Move),
        new RepeatVar(1),
        new IntVar("IncreaseDamage", 2m)
    ];

    public Scratch() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .WithHitCount(base.DynamicVars.Repeat.IntValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
        // ... 추가 로직 (다른 카드들에 버프 부여)
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(2m);
        base.DynamicVars["IncreaseDamage"].BaseValue += 1m;
    }
}
```
- [unknown] - [Pool(typeof(...))]로 카드풀 지정, 기본 속성은 private const, CanonicalVars로 DamageVar/RepeatVar/IntVar, OnPlay는 async Task + DamageCmd 빌더(Fluent) 패턴, OnUpgrade로 강화. 같은 종류 카드 순회: base.Owner.PlayerCombatState.AllCards.OfType<T>()

### QueenPowerModel.cs (파워 베이스)
```csharp
public abstract class QueenPowerModel : CustomPowerModel
{
    public override string? CustomPackedIconPath => $"res://TheQueen/images/powers/power.png";
    public override string? CustomBigIconPath => $"res://TheQueen/images/powers/big/power.png";
}
```
- [unknown] - 공통 베이스로 아이콘 경로 관리. 만신전: AffectionPowerBase : CustomPowerModel → 5개 신 상속

### QueenCardPool.cs
```csharp
public class QueenCardPool : CustomCardPoolModel
{
    public override string Title => "Queen";
    public override string? TextEnergyIconPath => "res://TheQueen/images/charui/text_energy.png";
    public override string? BigEnergyIconPath => "res://TheQueen/images/charui/big_energy.png";
    public override bool IsColorless => false;
    public override Color DeckEntryCardColor => new(112f/255f, 42f/255f, 112f/255f);
    public override Color ShaderColor => new(112f/255f, 42f/255f, 112f/255f);
}
```
- [unknown] - 카드풀 색상 RGB 지정, 에너지 아이콘 풀 단위 커스텀. 신별 별도 CardPool vs 통합 풀 + 카드별 색상 결정 필요

### QueenCharacter.cs
```csharp
public class QueenCharacter : PlaceholderCharacterModel
{
    public override Color NameColor => new(161f/255f, 67f/255f, 144f/255f);
    public override Color EnergyLabelOutlineColor => new(161f/255f, 67f/255f, 144f/255f);
    public override CharacterGender Gender => CharacterGender.Feminine;
    public override int StartingHp => 70;
    public override string CustomVisualPath => "res://TheQueen/scenes/creature_visuals/queen_character.tscn";
    public override string CustomIconTexturePath => "res://icon.svg";
    // CustomTrailPath, CustomIconPath, CustomEnergyCounterPath,
    // CustomRestSiteAnimPath, CustomMerchantAnimPath,
    // CustomArmPointingTexturePath/RockTexturePath/PaperTexturePath/ScissorsTexturePath,
    // CustomCharacterSelectBg, CustomCharacterSelectIconPath / LockedIconPath,
    // CustomCharacterSelectTransitionPath, CustomMapMarkerPath,
    // CustomAttackSfx, CustomCastSfx
}
```
- [unknown] - 캐릭터 체크리스트: 이름색, 시작 HP, 비주얼 씬, 아이콘, 카드 궤적, 에너지 게이지, 휴게소/상점 애니메이션, 멀티플레이 가위바위보 손모양, 선택 화면 전반, 맵 마커, 공격/시전 SFX

### QueenScratchBonusTracker.cs (플레이어별 정적 트래커)
```csharp
internal static class QueenScratchBonusTracker
{
    private sealed class Entry { public decimal ScratchDamageFromPlays; }
    private static readonly Dictionary<ulong, Entry> ByPlayer = new();
    private static Entry GetOrCreate(ulong netId) { /* 없으면 생성 */ }
    public static void RecordScratchPlay(Player owner, decimal increase)
    {
        if (owner.Character is not QueenCharacter) return;
        GetOrCreate(owner.NetId).ScratchDamageFromPlays += increase;
    }
}
```
- [unknown] - Dictionary<ulong netId, Entry>로 멀티플레이 플레이어별 분리. 애정도는 파워 스택으로 관리하는 편이 게임 표준 UI 자동 노출로 더 적합할 가능성

### HungerPower.cs
```csharp
public sealed class HungerPower : QueenPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        CardModel played = cardPlay.Card;
        if (played is not Devour || played.Owner?.Creature != base.Owner) return;
        if (base.Owner.Player is not { } player) return;
        await QueenCardCmd.AddSoulLamp(player, 1);
    }
}
```
- [unknown] - PowerType.Buff, PowerStackType.Counter(애정도에 적합 가능성), AfterCardPlayed 실제 시그니처. 애정도: 카드가 해당 신 태그인지 체크 후 스택 조정
- [unknown] - TODO: PowerStackType 다른 값들, PowerType.Debuff 등 확인

### TheQueen 결론
- [unknown] - BaseLib + Harmony + RitsuLib(선택) 실전 검증 조합. 신별 태그는 (CardTag)정수 캐스팅이 가장 간단. 카드는 [Pool] + 상속 + CanonicalVars + OnPlay/OnUpgrade. 애정도는 CustomPowerModel 베이스 + 신별 5개. 캐릭터는 PlaceholderCharacterModel 상속

## 5-15. YiSang (림버스컴퍼니 이상)
- [unknown] - 원작자 LingYin and Liuan, 1.3.9, "거미줄 신탁 대행자". 의존성 LibraryOfRuinaLib(0.1.0) — 제4의 프레임워크(Project Moon IP 전용 추정). min_game_version 0.109.0
- [unknown] - 3중 루트 상태: LI_XIANG_CI_CHUAN / JIA_HU / SHANG_HEN_STATE_POWER. 턴 종료 시 Sinking 전환 — Ci Chuan 1:1 → Butterfly, Jia Hu 2:1 → Coffin(나머지 Sinking 유지), Shang Hen 1:2 → Residual Scent
- [unknown] - LiXiangTriStateHelper.ApplyExclusiveState(하나만 활성), RemoveStateOnly, LiXiangTriStateCardRegistry(Scar/Pierce/Ward 카드 매핑), LiXiangTriStatePlayHookPower.AfterCardPlayed
- [unknown] - 무기 스탠스 순환: LiXiangWeaponStanceCycleHookPower.AfterPlayerTurnStart, LiXiangWeaponStanceCardRegistry, LiXiangWeaponStancePlayHookPower.AfterCardPlayed, LiXiangWeaponStanceHelper.ApplyExclusiveWeaponStance / ApplyLianDaoSwitchZuiYe, LiXiangWeaponStanceDamageHelper/DamagePatch
- [unknown] - 죄업(Guilt): LIXIANG_ZUI_YE_HOOK_POWER — 어떤 카드가 Guilt를 적용하는지 파워가 등록/관리(태그 대안)
- [unknown] - CHEN_LUN_POWER: 5턴마다 전체 적에게 Sinking × (1+Vulnerable) × (1+Weak) / 3 데미지 후 Sinking 제거
- [unknown] - Cards 40개+(Ira, GuDu, HuZui, WuYun, ZheBi, ChuJue, JianLi 등 병음)
- [unknown] - 영향: 배타적 상태 패턴은 "한 신만 10, 나머지 0" 판정에 응용. 애정도 자체는 Heathcliff 독립 게이지에 가까움. 레지스트리 패턴은 태그 대안(유연하나 보일러플레이트 많음). 곱셈 디버프 공식은 축복/저주 수치 설계 참고

## 5-16. Enkidu (길가메시 서사시)
- [unknown] - 원작자 wfxt, v0.0.8, BaseLib. DLL 293KB, PCK 137MB
- [unknown] - 네임스페이스: EnkiduModCode.Actions, Cards, Character, Extensions, Patches, Potions, Powers, Relics, Systems, TokenCards — .Systems는 범용 매니저용 독립 폴더
- [unknown] - Morph: Systems.MorphManager.ExecuteMorph, IMorphCard 인터페이스, MorphTracker, isMorphed, morphAction
- [unknown] - 파워: DivineClayPower(신들이 진흙으로 빚은 설정), FirstHumanityPower, ShieldOfUrukPower, ChainOfHeavenPower, JailOfHeavenPower, FlowerOfHumbabaPower, EchoOfTheEarthPower, SourceSoilPower, ResonanceOfTheLeylinePower, PerpetuityOfTheEarthPower/ActivePower(지속형+활성형 쌍), ReasonOfCreationPower, ManifestationOfDivineMightPower, BindingOfLawPower, NpPower(Fate "Noble Phantasm" 연상)
- [unknown] - 영향: 신화적 사실을 파워/카드명에 정확히 반영(아스트라페, 트라이던트, 아이기스 등). Systems/AffectionManager.cs, AffectionTracker.cs 분리 고려. IGodCard { God OwningGod } 인터페이스 대안. 상시 파워 + 축복 발동 임시 파워 조합 참고

## 5-17. TheCorrupted (The Cursed에서 영감)
- [unknown] - 원작자 LeifererGamer, v1.1.7, BaseLib v3.3.0+, min_game_version 0.106.1, DLL 286KB, PCK 125MB
- [unknown] - 폴더 구조(모범 사례):
```
TheCorrupted.src.Core.Models.Cards.{Basic, Common, Uncommon, Rare, Curse, Token, Ancient}
TheCorrupted.src.Core.Models.{Powers, Relics, RelicPools, Potions, PotionPools, Characters, Monsters, Enchantments, Afflictions, Commands, Extensions}
TheCorrupted.src.Core.Nodes.{Combat, Screens.Shops, Vfx, RestSite}
TheCorrupted.src.Core.Timeline.{Epochs, Stories}
TheCorrupted.Patches
```
- [unknown] - 카드를 희귀도별 하위 폴더로 분류하는 것이 표준 관례. Nodes: SNCardTrail/SNCardTrailVfx/SNParticleContainer, SNCreatureVisuals, SNSelectionReticle, SNMerchantCharacter, SNRestSiteCharacter
- [unknown] - 폼: CorruptedFormPower.AfterCardDrawn ↔ NormalityPower.BeforeCardPlayed (정상/타락 이분법)
- [unknown] - 파워: DevourSoulsPower, GroupSummoningPower, ArmyOfDoomPower(AfterPlayerTurnStart), SummonArmyNextTurnPower, HellsGateOpenPower, HellfirePactPower, HellfireBreathingPower, DoomsdayPower, DoomingStrengthPower, MantleOfCorruptionPower, SavedByCorruptionPower, CorruptedDeliveryPower, CarefulPlanningPower, ArmyEmpowermentPower, UnstableEnergyPower, EnergyCirculationPower
- [unknown] - 게임 코어 공식: MegaCrit.Sts2.Core.Models.Powers.DoomPower(DoomPowerPatch로 확장), MegaCrit.Sts2.Core.Timeline.Epochs.NeowEpoch
- [unknown] - 영향: Cards/{Rarity}/{God}CardName.cs 또는 Cards/{God}/{Rarity}CardName.cs 중 택1. 코어 파워 재활용 검토. 신별 카드 궤적 Vfx(제우스=번개, 포세이돈=물방울). Timeline/Epoch로 만신전 이벤트 가능

## 5-18. TheArtist (Boon/Bane)
- [unknown] - 원작자 mremi, v0.1.0, BaseLib v3.1.0+. 손패 위치별 9색(빨/주/노/초/파/남/보/흰/검) 라이더 효과
- [unknown] - 네임스페이스: Cards, Character, Config, Mechanics, Patches, Potions, Powers, Relics
- [unknown] - Mechanics.SlotEffects: ApplyBoon, ApplyBane, CardWillFireBoon, WillFireBane, WillAbsorbBane, pickedColor, ApplyOnPlayRider, ApplyProportionalBonus, ApplyGoldBonus, ApplyGoldBaneCascade, SelfDamage, DiscardOneRandomFromHand, ReplayCardOnPlay
- [unknown] - InspirationPower.PickHandIndexViaArrow, PreResolvedHandIndex, handIndex
- [unknown] - 영향: 축복/저주를 AffectionBoonPower/AffectionBanePower 또는 ApplyBoon/ApplyBane로 명명. 판정(IsAtMaxAffection) → 적용(ApplyBlessing) 2단계. 애정도 비례 보너스 옵션. Mechanics/AffectionSystem.cs 분리

## 5-19. HermitMod (STS1 포팅 총잡이)
- [unknown] - Hermit Mod Team, v1.5.0, BaseLib v3.1.3+. "Dead On"(손패 위치 트리거) + "Bruise"(누적 디버프) + 저주 시너지
- [unknown] - DeadOnHelper, DeadOnGlowPatch, IncrementDeadOnCount, WasDeadOnFor. 네임스페이스: Cards, Cards.DeadOrAlive, Cards.Dissolve, Character, Extensions, Patches, Powers, Relics, Utility

## 5-20. Ryoshu (림버스컴퍼니 료슈)
- [unknown] - 원작자 帽子, 0.3.6, dependencies 없음(외부 프레임워크 미사용), .NET 9.0, "Ryoshu - beta" 경로, HongluMod.Code.Helpers 공유
- [unknown] - RyoshuPoisePower: BeforeAttack, AfterAttack, AfterCardPlayed, AfterPowerAmountChanged
- [unknown] - IOnPoisePowerTriggered 인터페이스(Observer) — 게임 코어 MegaCrit.Sts2.Core.Models.Powers.PungentSmellPower가 구현. NotifyPoisePowerTriggered, CriticalRhythmPower.OnPoisePowerTriggered, GetTotalPoisePowerGainedThisCombat / _totalPoisePowerGainedThisCombat
- [unknown] - MugaPower(AfterPlayerTurnStart, AfterPowerAmountChanged), RyoshuCombatHelper(MemoryAll, MemoryChoose, MemoryRandom, Erase, EnsureState, GainPoisePower), RewriteDestinyPower, RyoshuExtraTurnPower.AfterTakingExtraTurn, RyoshuBleedPower.AfterDamageGiven
- [unknown] - 영향: IOnGodBlessingTriggered 인터페이스로 유물이 축복 발동 감지. 프레임워크 없이도 가능하나 비권장. 전투 중 축복/저주 발동 횟수 통계 필드 고려

## 5-21. The Reaper (ZSMod)
- [unknown] - 원작자 zswzxc, v0.5.4, BaseLib 3.3.4+, min_game_version 0.107.1. 클래스 ZSproject, id zsproject, 표시명 ZSMod-Reaper
- [unknown] - 폴더: Cards/{Basic, Common, Uncommon, Unommon(오타 중복), Rare, Curses, Token, Vars}, Actions, Character, Command, Enchantments, Events, Monsters, Patch, Potions, Powers, Relics, Scripts
- [unknown] - StyxFormZsPower(AfterApplied, AfterRemoved, AfterCardEnteredCombat, ProcessCard), StyxFormZsReaper(Rare), UnderworldPathZsPower, OversoulZsPower.AfterSideTurnEnd, SoulMasteryZsPower, EwigeWiederkunftZsPower, KingOfWormsZsPower, HarvestMoonZsPower, LegionZsPower, TerrorZsPower, MourningZsPower, EnshroundZsPower, SoulGuardZsPower, SpiritCycleZsPower
- [unknown] - 영향: Styx/Underworld 명명 이미 존재 → 하데스 명명 차별화. Cards/Vars 분리. AfterCardEnteredCombat은 애정도 초기화에 활용 가능. ProcessCard는 고급 확장 패턴

## 5-22. ⭐⭐⭐ scbsmod (Succubus) — Progress Power 이중 구조
- [unknown] - 원작자 oscar865, v0.9.0, BaseLib v3.3.0+. 쾌락 자원 축적 → 임계값에서 폼 각성 — 애정도와 구조적으로 가장 유사
- [unknown] - AwakenedFormProgressPower(SetProgress, Clear, AfterCombatEnd) + AwakenedFormPower(Record/RecordInternal, AfterCombatEnd, DealRandomEnemy, AwakenedFormRecordType)
- [unknown] - FelPactProgressPower(SetProgress, Clear, AfterCombatEnd) + FelPactPower(BeforeCardPlayed/AfterCardPlayed, AfterDamageReceived, RecordHpLossForDiscount)
- [unknown] - DivinePactPower.TriggerIfReady(명시적 임계값 체크), AfterCombatEnd, AfterPowerAmountChanged
- [unknown] - 진행도 파워 = 애정도 0~10 누적/리셋, 효과 파워 = 임계값에서 축복/저주. TriggerIfReady 명명 채용 가능. AfterCombatEnd Clear 여부 = 애정도가 런 전체 유지인지 전투 단위 리셋인지 결정 지점
- [unknown] - 최종 아키텍처:
```
Powers/
├── AffectionProgressPowerBase.cs  (CustomPowerModel 상속)
│   ├── ZeusAffectionProgressPower.cs   : SetProgress, Clear, AfterCombatEnd
│   ├── PoseidonAffectionProgressPower.cs
│   ├── HadesAffectionProgressPower.cs
│   ├── AthenaAffectionProgressPower.cs
│   └── ArtemisAffectionProgressPower.cs
├── AffectionEffectPowerBase.cs
│   ├── ZeusBlessingCursePower.cs  : TriggerIfReady, AfterPowerAmountChanged
│   ├── PoseidonBlessingCursePower.cs
│   ├── HadesBlessingCursePower.cs
│   ├── AthenaBlessingCursePower.cs
│   └── ArtemisBlessingCursePower.cs
```
- [unknown] - 진행도 파워는 AfterCardPlayed(+1)와 AfterPlayerTurnStart(-1)만 담당. 효과 파워는 진행도 값 참조 후 TriggerIfReady()로 10/0 발동
- [unknown] - 기타 파워: BloodcraftPower, BloodMarkWeakPower, CharmPower, ConsumedByLustPower, AphrodisiacBodyPower, ForbiddenRitualPower, ResourceBoostPower, PleasureEchoPower, PleasureGroovingPower, EnemyLustConsumedThisTurnPower, HpLostThisTurnPower, HealedThisTurnPower(턴 단위 추적)
- [unknown] - 폴더: Cards.Ancient/Basic/Common/Curses/Generated/Rare/Statuses/Uncommon

## 5-23. ⭐⭐ WineFox(酒狐) — RitsuLib 저자 제작, 커스텀 HUD
- [unknown] - 원작자 灯火橘, OLC(RitsuLib 제작자), 1.2.16, min_game_version 0.110.0, RitsuLib 0.5.0+. 상업적 이용 금지, 코드 MIT / 에셋 CC BY-NC-SA 4.0
- [unknown] - HUD: NMaterialInventoryHud, MaterialInventoryBox, MaterialInventoryPanel, ShouldShowMaterialInventory, NCombatUiActivateMaterialInventoryHudPatch / NCombatUiAnimOutMaterialInventoryHudPatch / NCombatUiDeactivateMaterialInventoryHudPatch, MaterialInventoryHudPatches, WineFoxCounterLabel, WineFoxEnergyCounter, WineFoxEnergyVfxParticles
- [unknown] - Epoch: WineFoxCharacterEpoch, WineFoxCardEpoch, WineFoxBossEpoch, WineFoxEliteEpoch, WineFoxVictoryEpoch
- [unknown] - CraftHook: BeforeCraft, BeforeCraftProductDelivered, AfterCraftProductDelivered
- [unknown] - 기타: WineFoxCraftingCardPool, WineFoxFoodPotionPool, WineFoxModStory, SakuraWindController, ThrottledShaderViewport, PotionCardRewardSaveData
- [unknown] - 영향: 애정도 게이지를 상시 커스텀 HUD 노드로(검증된 정석). 활성화/퇴장/비활성화 3종 패치 채용. 재사용 카운터 라벨. Epoch로 단계적 언락. RitsuLib 채택 시 가장 신뢰할 참고 코드

## 5-24. Artanis (스타크래프트2)
- [unknown] - 원작자 KishibeKoma, v1.00, dependencies 없음. NexusPower, PhotonCannonPower, ShieldBatteryPower, CyberneticsCorePower, ArtanisForce, ArtanisForge, ArtanisDisintegration. 폴더 Core.Models.{Afflictions, CardPools, Cards, Characters, PotionPools, Potions, Powers, RelicPools, Relics}

## 5-25. Deadcells (데드셀즈)
- [unknown] - 원작자 [CSTG]GongJuYin, v0.2.0, BaseLib 3.2.1+. DeadcellsCardTag 전용 클래스(get_DCCDTags, get_DeadcellsCardTags, get_CanonicalTags). BaseLib.Config.SimpleModConfig 사용
- [unknown] - 파워: Bleeding, BleedingSpread, Burns, Frostbite, CorrosiveCloud, IceShield, Magnetic. 폴더 Scripts.{cards, character, events, nodes, powers, relics, utils}(소문자)

## 5-26. Mordekaiser (LoL)
- [unknown] - 원작자 ice snow, v0.1.7, dependencies 없음. 설치 안내: mods 폴더를 게임 루트에 복사(Steam 우클릭 → 관리 → 로컬 파일 탐색)
- [unknown] - MordekaiserArchitectDialogue(아키텍트 NPC 대화). 영혼 순환: soulcallpower.AfterPlayerTurnStart → soulreleasepower.AfterCardExhausted → soulreavepower.AfterPowerAmountChanged
- [unknown] - underworldpower, dashstancepower, soulsteelformpower, deceasedsdomainpower, finaljudgmentpower, doomhourpower. 폴더 Core.CardLibrary, Core.Nodes.{Combat, RestSite, Screens.Shops, Vfx}, Core.Timeline.{Epochs, Stories}, Mordekaiserpools, Utils.CardUtils, afflictions, cards, power, relics, scripts
- [unknown] - 3개 모드 종합 영향: 애정도도 생성→감소→감지 3단계 체인 가능. 저승 테마 흔함 → Tartarus, Cocytus, Erebus 등 특색 있는 이름 고려. DeadcellsCardTag식 전용 태그 클래스. SimpleModConfig로 밸런스 설정

## 5-27. ⭐⭐⭐ MarthCharacterMod (업로드명 HeroKing, 파이어엠블렘)
- [unknown] - 원작자 Yendorami, v1.0.5, BaseLib 3.3.0+, min_game_version 0.107.1. "created from a template for use with BaseLib" — BaseLib용 캐릭터 템플릿 존재 직접 증언
- [unknown] - 템플릿 명명: {ModName}Card, {ModName}CardPool, {ModName}Potion, {ModName}PotionPool, {ModName}Power, {ModName}Relic, {ModName}RelicPool (MarthCharacterMod, TheArrow, Painter 등 반복)
- [unknown] - 템플릿 폴더: {ModName}Code/{Cards, Character, Extensions, Helpers(HelperFunctions), Hovertips({ModName}HoverTips), Potions, Powers, Relics}. MarthHoverTips : MegaCrit.Sts2.Core.HoverTips, get_ExtraHoverTips
- [unknown] - 카드: Sol, Luna, Astra, Aegis, Momentum, Engage, Disarm, Counter, Fortified, IWontLose, ForAltea, FogOfWar, HalfMoon, HardEdge, WoDao, IronSword, LowThrust, RaidChop, FlagCut
- [unknown] - 영향: BaseLib 템플릿 찾아 시작(Alchyr 저장소/위키 검색). PantheonCard, PantheonCardPool, PantheonPower, PantheonRelic 명명. Helpers/Hovertips 채용. 신화 용어를 카드명으로

## 5-28. ⭐⭐⭐ PrismMod (Prism Shirou)
- [unknown] - 원작자 nagis, v0.1.0, "플레이 가능한 보스 캐릭터"(프리즈마☆이리야 시로우). BaseLib 3.1.8+ + STS2-RitsuLib 0.4.6+ 동시 의존. min_game_version 0.105.0
- [unknown] - using: BaseLib.Abstracts, STS2RitsuLib.Scaffolding.Ancients.Options, Scaffolding.Characters, Scaffolding.Characters.Visuals.Definition, Scaffolding.Content, Timeline.Scaffolding, Unlocks, Utils.Persistence, Utils.Persistence.Migration(세이브 마이그레이션), Data
- [unknown] - 결론: BaseLib으로 기본 모델, RitsuLib으로 스캐폴딩·언락·세이브 마이그레이션 보완이 표준 조합
- [unknown] - 디버그: PrismBrainLeechConsoleCmd, PrismByrdonisConsoleCmd, PrismArchitectConsoleCmd, PrismAttackIntentDeckConsoleCmd, PrismBrainLeechCardFactoryDiagnosticsPatch, PrismBrainLeechShareKnowledgeDiagnosticsPatch, PrismBeamDamageDebugConsoleCmd
- [unknown] - DefectBoss, DefectFrostPower. 아키텍트/에인션트: PrismArchitectDialoguePatch, PrismAncientFirstVisitDialoguePatch, PrismAncientPopulateLocKeysPatch, PrismArchitectAttackAnimationPatch, PrismArchitectEntryAttackPatch, PrismAllowedAncientDialogues, PrismAncientDialogueLayoutPatch
- [unknown] - 파워: IndexFundPower, RegentForgePower, ShardFurnacePower, JointGuaranteePower, OverchargedLensPower, RadiantGamblePower, MirrorScreenPower, BorrowedOrbitPower, BorrowedFootworkPower, OverinvestmentPower, DopaminePower
- [unknown] - PrismByrdonisNestEvent/EventPatch — Byrdonis Nest는 STS2 공식 이벤트
- [unknown] - 영향: 병용 스택 근거 강화. SetGodAffection(zeus, 10) 같은 디버그 콘솔 커맨드. 마이그레이션은 초기 우선순위 낮음. Byrdonis Nest에 만신전 선택지 검토

## 5-29. ⭐⭐⭐ Wanderer (제네릭 파워)
- [unknown] - 원작자 zeqzero, v0.1.2, BaseLib v3.1.4+(미동봉). readme.txt 상세 설치 안내(모범). 사무라이/로닌, 검도 자세명(Chudan/Gedan/Jodan/Hasso/Waki)
- [unknown] - WandererNextTurnApplyPower`1(ApplyNow, ApplyEffect, AfterSideTurnStart, get_ExtraHoverTips) → WandererNextTurnBlockPower, WandererNextTurnDamagePower, WandererNextTurnDrawPower. NextTurnPowers
- [unknown] - TargetArmsPower, TargetHeadPower, TargetLegsPower, RoninFormPower, SenNoSenPower, DeathPactPower, ShinigamiPower. .Interfaces 폴더
- [unknown] - 영향: GodAffectionPower<TGod> 단일 제네릭 가능성(또는 생성자 인자 방식). 단 엔진의 파워 식별 방식 확인 필요, 구체 클래스 5개가 더 보편적이라 신중히 선택

## 5-30. Alice (동방 프로젝트)
- [unknown] - 원작자 Reddo, 2.4.0. 카드 95장(멀티+에인션트 포함), 이벤트 6, 유물 10, 포션 3. BaseLib 3.2.0 + Patchoulib 1.8.0 — 완성형 모드 규모 기준점
- [unknown] - Scrpits.Dolls: DollAutoPower(AfterCardPlayed/AfterSideTurnEnd), DollBowPower, DollDancePower, DollJudgmentPower, DollReplacementPower, DollRevengePower, DollerPower/ColorfulDollerPower(AfterPlayerTurnStart), WitchFormPower
- [unknown] - 지역 파워: FrancePower, RussiaPower, LondonPower, NetherlandPower, ShangHaiPower, PengLaiPower, XiZangPower, HinaPower, GoliathPower, OrlPower
- [unknown] - 영향: 신별 세트를 별칭/성지 이름으로 명명. 인형사+인형 이중 구조가 신 파워+효과 구조와 개념 유사

## 5-31. SlayTheSpireBossAncients
- [unknown] - 원작자 Reddo, 1.3.0. STS1 보스 9명 → 에인션트, 각 10개 전용 유물. BaseLib 3.2.0 + Patchoulib 1.8.0. Patchoulib.Scrpits.Main
- [unknown] - 폴더 StsBossAncients.Scripts.{Ancients, Cards, Enchantments, Main, Patches, Powers, Relics, Utils}
- [unknown] - 유물: Timer.Countdown, CopperBall.PutInFlow/TakeOutFlow/OnRightClick, WraithFury.AfterDeath, GluttonHeart.AfterDeath, HotForge/SlimeBall/BrainInJar/BelieverSet.AfterObtained, ExecutionDevice.TryExecute, StarFruit.AfterStarsGained(게임 코어 Stars 자원), DeadBell.AfterDamageGiven, LapisLazuli.AfterCardDrawn, BackupBattery.OnRightClick, AgelessWatch.OnRightClick
- [unknown] - 영향: 신별 유물 설계 시 AfterObtained/AfterDeath/OnRightClick/TryExecute 활용. Stars 시스템과 애정도 관계 검토

## 5-32. Patchoulib (정체 확인)
- [unknown] - "개인 도구 라이브러리, 충돌 방지를 위해 자주 쓰이는 패치 통합". Reddo 개인 라이브러리, BaseLib 3.2.0+ 위에 얹는 보조. DLL 70KB, PCK 146KB
- [unknown] - CardTransformListenerPatch.NotifyListeners, HealthBarOverlayPowerPatch, RightClickableCardPatch, RightClickableRelicPatch, VisiblePowerAppliedPatch/RemovedPatch/CreatureInitPatch/CreatureCleanupPatch, VisibleCardPoolCardLibraryPatch, VisibleCardPoolModelDbPatch, ModSFXPatch, OptionTexturePatch
- [unknown] - 영향: 공용 유틸 분리는 검증된 관행(초기엔 불필요). VisiblePower 4단계를 애정도 UI 생명주기 체크리스트로

## 5-33. Faust (파우스트)
- [unknown] - 원작자 帽子, 0.1.8, dependencies 없음, PCK 236MB. FaustMod.Code.Helpers, FaustMod.PaperSlip, FaustMod.Patches
- [unknown] - MephistophelesPower, MirrorWorld, TwinSisters(AfterAddedToHand), CrushboundPast, StripFleshFromBones, DecaextractionStrike, ExtractStrike, DifferentPath
- [unknown] - 시간: FaustTimePower, OverClockPower, CorrectDeviationPower/OwnerPower, FaustExtraTurnPower, RepetitionPower
- [unknown] - 헬퍼: FaustCombatHelper, FaustAudioHelper, FaustSimpleBHelper, DrawPilePreviewHelper, DrawPreviewSelectHelper, LoadCardHelper, FaustExtractionHelper, GearHitVfxHelper. CardPileCmd_Add_AfterAddedToHandPatch
- [unknown] - 영향: 정확한 신화 명칭. AffectionCombatHelper/AffectionVfxHelper 등 세분화는 규모에 비례. 손패 진입 즉시 반응 카드(신탁 카드) 참고
- [unknown] - 개인 개발자 생태계 종합: (1) 개인 유틸 라이브러리 분리(Patchoulib) vs (2) 프레임워크 없이 개발(帽子). 우리 규모엔 BaseLib+RitsuLib이 효율적

## 5-34. The Sorceress (이중 모멘텀 + 7단계 Epoch)
- [unknown] - 원작자 weird autumn, v0.3.0, BaseLib 3.3.2+. "재치, 민첩성, 마법의 불꽃에 의존하는 교활한 사기꾼이자 마법 트릭스터". 아티스트 Ghostnade, Korvi__, megueggu, CupsOfJade / 음향 SonoFxAudio, Saladsounds
- [unknown] - SorcerousMomentumPower.AfterCardPlayed + CunningMomentumPower.AfterCardPlayed — 같은 훅, 다른 카드 종류 반응(5신 애정도의 축소판). SorcerersGambitPower
- [unknown] - TwoWeapon: Starter 2, Common 3, Uncommon 3, Rare 3, Token 8 등 15장+. TwoWeaponFlurryPower, TwoWeaponDefensePower, TwoWeaponSorceryPower, TwoWeaponBurst.BeforeHandDraw
- [unknown] - Sorceress1Epoch ~ Sorceress7Epoch, SorceressAscensionEpochPostfix, BossEpochPostfix, EliteEpochPostfix, UnlockEpochPostfix, SorceressEras
- [unknown] - SorceressNeowExpansionPatch, SorceressByrdonisNestInitialPatch/NestEatPatch/NestTakePatch, SorceressPhilosophersPatch, Cards.Multiplayer, Cards.Starter, DefensiveAdvantagePower + DefensiveAdvantageUpgradePower
- [unknown] - 영향: 신별 카드 8~12장이 현실적. 이벤트에 신별 선택지("이 신에게 제물을 바치겠습니까?"). 멀티플레이 시 플레이어별 애정도 분리(Dictionary<ulong>)

## 5-35. ⭐⭐⭐ Cloud (FF7) — 4중 메커닉 + 오버레이 UI
- [unknown] - 원작자 HummingBird, v1.0.11, BaseLib v3.3.8+, min_game_version 0.109.0
```
CloudCode.Mechanics/
├── ATB/    (ATBDisplayOverlay.cs(Setup), ATBDisplayOverlayPatch, ATBCardUi, CardModel_SpendResources_ATB)
├── Limit/  (LimitDisplayOverlay.cs(Setup), LimitDisplayOverlayPatch)
├── Summon/ (SummonDisplayOverlay.cs(Setup), SummonDisplayOverlayPatch)
└── Stance/
```
- [unknown] - {Mechanic}DisplayOverlay.cs + {Mechanic}DisplayOverlayPatch 2종 세트 표준. Setup()이 공통 진입점. res://CloudCode/Mechanics/{Name}/{Name}DisplayOverlay.cs — Godot 씬 스크립트 직접 attach
- [unknown] - 영향: 한 캐릭터가 여러 게이지 오버레이를 동시에 가질 수 있음이 검증됨. AffectionDisplayOverlay.cs + AffectionDisplayOverlayPatch.cs. ATBCardUi처럼 카드에 소속 신/현재 애정도 표시 가능. Mechanics/Affection/ 폴더 채택
- [unknown] - Cards.{Ancient, Basic, Common, Rare, Uncommon}, Nodes.NCloud, CloudNMerchantCharacter, CloudNRestSiteCharacter, CloudNSelectionReticle

## 5-36. TheCompleated (SpireModTheAlest)
- [unknown] - 원작자 TheAlest, v0.9.5.1, BaseLib. EchoesPower, InexorableTidePower, InfectPower. Cards.{Basic, Common, Rare, Tokens, Uncommon, Vars}

---

# PART 6. 최종 기술 스택 결론

## 확정 가능한 개발 스택
- [unknown] - 필수 기반: BaseLib (Alchyr) — CustomCardModel, CustomPowerModel, CustomCharacterModel, CustomRelicModel 등
- [unknown] - 패치: HarmonyLib — [ModInitializer("Init")] + new Harmony(고유ID).PatchAll()
- [unknown] - 보조: STS2-RitsuLib — 어트리뷰트 자동등록, 이벤트 시스템. 단독(Qu) 또는 BaseLib 병행(TheQueen, PrismMod)
- [unknown] - 애정도 옵션 A(권장): 신별 CustomPowerModel 파워(Heathcliff SanityPower 패턴) — 게임 표준 UI 자동 노출 / 옵션 B: CustomResources`1 통합 자원 — 사용 예시 미발견
- [unknown] - 신별 태그 옵션 A: (CardTag)임의정수 캐스팅(TheQueen 검증) / 옵션 B: RegisterOwnedCardTagAttribute + ModCardTagRegistry(RitsuLib 의존)

## 폴더 구조 권장안 (Corrupted + TheArrow + Painter 종합)
```
PantheonMod/
├── Cards/
│   ├── Common/
│   ├── Zeus/ Poseidon/ Hades/ Athena/ Artemis/
├── Powers/
│   ├── AffectionPowerBase.cs
│   └── {God}AffectionPower.cs  (5개)
├── Relics/ Potions/ CardPools/ Characters/
├── Patches/
├── Tags/
└── Entry.cs
```

## 애정도 UI 최종 아키텍처 (WineFox + Cloud 종합)
```
Mechanics/Affection/
├── AffectionDisplayOverlay.cs      (Setup()에서 5개 신 게이지 UI 초기화)
├── AffectionDisplayOverlayPatch.cs (전투 진입/퇴장 시 활성화/비활성화)
├── ZeusAffectionProgressPower.cs   (scbsmod 패턴)
├── ZeusAffectionEffectPower.cs
├── PoseidonAffectionProgressPower.cs
├── ... (5신 × 2파워)
```
- [unknown] - 27개 이상 모드에서 반복 확인된 핵심 패턴(진행도/효과 분리, 오버레이 UI, Harmony 패치, 신별 구체 클래스)을 통합한 최종 설계

## 남은 TODO
- [unknown] - CustomResources`1 실제 사용 예시 발견 시 옵션 A/B 최종 결정
- [unknown] - PowerStackType / PowerType 전체 열거형 값 확인
- [unknown] - "BaseLib 템플릿" / "STS2 Character Creator" 도구 찾기
- [unknown] - AfterCombatEnd에서 애정도 리셋 여부(런 유지 vs 전투 단위) 결정
- [unknown] - 신별 카드풀 분리 vs 통합 풀 결정
