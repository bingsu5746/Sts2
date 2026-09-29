# Wanderer 모드 (STS2) 리버스 엔지니어링 분석 보고서

> 대상: `Wanderer_Standard_802_0_1_2_2026-06-19T22-03Z_R0WdQbR3s.zip`
> 모드: **Wanderer v0.1.2** / 제작자 **zeqzero** / Slay the Spire 2 캐릭터 모드
> 분석 방법: `.dll` → IL 디스어셈블(dnfile + dncil, 토큰·제네릭·문자열 전부 해석), `.pck` → 자체 Godot PCK 파서로 전량 추출
> 목적: 자체 STS2 캐릭터 모드(만신전/무협/궁수) 제작 시 참고할 **구현 패턴 레퍼런스**

---

## 0. TL;DR — 이 모드에서 훔쳐올 만한 것 8가지

| # | 패턴 | 왜 중요한가 |
|---|------|-------------|
| 1 | **스탠스 = `IStancePower`를 구현한 PowerModel** | 별도 상태 저장소 없이 `creature.Powers.OfType<IStancePower>().FirstOrDefault()` 하나로 "현재 스탠스"가 정의됨. 세이브/멀티플레이 동기화 공짜. |
| 2 | **전환 진입점 단일화 (`WandererCmd.EnterStance`)** | 카드·파워·유물 어디서 바꾸든 전부 이 함수 하나를 통과. 이벤트 방송/차단/시각효과가 한 곳에 모임. |
| 3 | **`AfterPowerAmountChanged` + `AfterPlayerTurnStart` 이중 훅** | "진입 시 + 턴 시작 시" 효과를 BaseLib 훅 2개에 같은 로직으로 구현. STS1의 `onEnterStance`/`atStartOfTurn` 대응. |
| 4 | **`IWandererEventListener` 자체 이벤트 버스** | BaseLib이 안 주는 커스텀 이벤트(스탠스 이탈/진입, Shift, Refill, 의식적 죽음)를 카드·파워·유물에 브로드캐스트. |
| 5 | **`SteadyPower` = 전환 차단 플래그** | 전환 함수 내부에서 `Any(p => p.Amount > 0)` 한 줄로 "스탠스 고정" 구현. |
| 6 | **토큰 카드 + `ChooseCard`로 스탠스 선택 UI** | 스탠스 선택을 별도 UI 없이 "카드 5장 중 고르기" 화면으로 처리 (`Kamae` 카드). |
| 7 | **정적 `Dictionary<Creature, T>` 사이드카 상태** | PowerModel에 못 넣는 카운터(이탈 횟수, Shift 횟수)를 `WandererCmd`의 static dict로 보관. |
| 8 | **`WandererVisuals.SetStance`** | 스탠스별 스프라이트 텍스처 교체 + 사망/시니가미 틴트. Godot 노드 직접 접근. |

---

## 1. 패키지 구조

```
Wanderer_Standard_802_0_1_2_....zip
├── readme.txt
└── mods/Wanderer/
    ├── Wanderer.json     (266 B)   — 매니페스트
    ├── Wanderer.dll      (269,824 B) — C# 로직 (459 타입)
    └── Wanderer.pck      (75,642,832 B) — Godot 리소스 팩 (750 엔트리)
```

### 1.1 매니페스트 (`Wanderer.json`)

```json
{
  "id": "Wanderer",
  "name": "Wanderer",
  "author": "zeqzero",
  "description": "Wanderer Character Mod",
  "version": "v0.1.2",
  "has_pck": true,
  "has_dll": true,
  "dependencies": [{ "id": "BaseLib", "min_version": "v3.1.4" }],
  "affects_gameplay": true
}
```

- BaseLib은 **번들하지 않음**. 별도 설치 전제(readme 명시).
- 설치 경로: `Slay the Spire 2/mods/Wanderer/`.

### 1.2 PCK 포맷 (자체 파서로 확인한 실제 값)

| 필드 | 값 |
|---|---|
| magic | `GDPC` |
| pack_format_version | **3** |
| Godot 버전 | **4.5.1** |
| pack_flags | **2** (= `PACK_REL_FILEBASE`) |
| file_base | 112 |
| 디렉터리 오프셋 | 75,566,688 (**파일 끝쪽**) |
| 엔트리 수 | 750 |

**중요한 함정**: `pack_flags = 2`이면 인덱스의 파일 오프셋이 **`file_base`(112) 기준 상대값**입니다. 절대 오프셋 = `entry.offset + 112`. 이걸 빼먹으면 모든 파일이 112바이트씩 밀려서 읽힙니다. (형식 v3 = 디렉터리가 끝에 붙는 구조)

엔트리 구조: `path_len(u32) | path | offset(u64) | size(u64) | md5(16B) | flags(u32)`.
전 파일 `flags = 0` (암호화 없음).

### 1.3 PCK 내용물

| 확장자 | 개수 | 비고 |
|---|---|---|
| `.ctex` | 339 | Godot `CompressedTexture2D`. 내부는 **무손실 WebP** (`GST2` 헤더, data_format=2) |
| `.import` | 339 | 원본 png 경로 ↔ ctex 매핑 |
| `.json` | 57 | 로컬라이즈 55 + `Wanderer.json` + `dotnet-tools.json` |
| `.scn` | 6 | 컴파일된 씬 |
| `.remap` | 6 | `.tscn` → `.scn` 리맵 |
| `.cfg` / `.bin` / `.binary` | 3 | `global_script_class_cache.cfg`, `uid_cache.bin`, `project.binary` |

**씬 6종** (캐릭터 모드가 반드시 갖춰야 하는 세트):

```
res://Wanderer/scenes/wanderer/wanderer.tscn                (전투용 본체)
res://Wanderer/scenes/wanderer/wanderer_rest_site.tscn      (모닥불)
res://Wanderer/scenes/wanderer/wanderer_merchant.tscn       (상점)
res://Wanderer/scenes/wanderer/wanderer_icon.tscn           (아이콘)
res://Wanderer/scenes/wanderer/char_select_bg_wanderer.tscn (캐릭터 선택 배경)
res://Wanderer/scenes/wanderer/selection_reticle.tscn       (선택 레티클)
```

### 1.4 이미지 경로 규약 (`WandererCode.Extensions.StringExtensions`)

`Path.Join`으로 조립. 모두 `res://Wanderer/images/` 하위.

```
ImagePath(p)         → Wanderer/images/{p}
CardImagePath(p)     → Wanderer/images/card_portraits/{p}
BigCardImagePath(p)  → Wanderer/images/card_portraits/big/{p}
PowerImagePath(p)    → Wanderer/images/powers/{p}
BigPowerImagePath(p) → Wanderer/images/powers/big/{p}
PotionImagePath(p)   → Wanderer/images/potions/{p}
RelicImagePath(p)    → Wanderer/images/relics/{p}
```

**자동 매핑 규칙** (`WandererCard` / `WandererPower` 기반 클래스):

```csharp
// 카드 초상화 = 모델 ID의 Entry에서 "WANDERER-" 접두사 제거 → 소문자 → ".png"
Id.Entry.RemovePrefix().ToLowerInvariant() + ".png"
// 예: WANDERER-AH_TA_TA_TA_TA_TA → ah_ta_ta_ta_ta_ta.png
```

파워 아이콘은 파일이 없으면 **`power.png` 폴백** (`ResourceLoader.Exists` 체크). 이 폴백 패턴은 에셋이 미완성인 상태로도 크래시 없이 빌드를 돌릴 수 있게 해 주므로, 개발 초기에 그대로 복사할 가치가 있습니다.

---

## 2. 부트스트랩 (`Wanderer.MainFile`)

`Godot.Node` 상속. Godot 측에서 `Initialize()`가 호출되는 구조(`GetGodotMethodList` / `InvokeGodotClassMethod` 생성 코드 존재).

```csharp
public static string ModId = "Wanderer";
public static string CompatibleSts2Version = "v0.107.1-59260271";
public static Logger Logger = new Logger("Wanderer", false);

public static void Initialize()
{
    var running = ReadRunningSts2Version();
    Logger.Info($"Built against sts2 {CompatibleSts2Version}; running on {running ?? "<unknown>"}.");
    if (running != null && running != CompatibleSts2Version)
        Logger.Warn($"sts2 version mismatch - mod was built against {CompatibleSts2Version} but game is {running}. Expect breakage.");

    new Harmony("Wanderer").PatchAll();
}

private static string? ReadRunningSts2Version()
{
    var dir = Path.GetDirectoryName(OS.GetExecutablePath());
    if (dir == null) return null;
    var file = Path.Combine(dir, "release_info.json");
    if (!File.Exists(file)) return null;
    try {
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        return "v" + doc.RootElement.GetProperty("version").GetString()
                   + "-" + doc.RootElement.GetProperty("commit").GetString();
    } catch (Exception e) {
        Logger.Warn("Could not read sts2 release_info.json: " + e.Message);
        return null;
    }
}
```

**그대로 베낄 것**: 게임 실행 파일 옆 `release_info.json`(`version`, `commit`)을 읽어 빌드 대상 버전과 비교 → 불일치 시 경고. STS2가 자주 패치되므로 모드 깨짐 원인을 로그로 바로 알 수 있습니다.

> 카드/파워/유물의 **등록 코드가 전혀 없습니다.** BaseLib이 `CustomCardModel` / `CustomPowerModel` / `CustomRelicModel` 등 추상 클래스를 리플렉션으로 스캔해 자동 등록하는 구조입니다 (`WandererCard.ctor`에 `autoAdd` 파라미터가 있는 것이 근거). 즉 새 카드 = 클래스 하나 추가로 끝.

---

## 3. 캐릭터 정의 (`WandererCode.Character.Wanderer`)

`BaseLib.Abstracts.PlaceholderCharacterModel` 상속.

```csharp
public const string CharacterId = "Wanderer";
public static readonly Color Color = new Color("3E5158");   // 어두운 청회색

MapDrawingColor => Color;
NameColor       => Color;
Gender          => 2;
StartingHp      => 66;

StartingDeck    => [ Kamae, Strike×5, Defend×4 ]   // 총 10장, Kamae가 첫 카드
StartingRelics  => [ BrokenJuzuRelic ]
CardPool        => WandererCardPool
RelicPool       => WandererRelicPool
PotionPool      => WandererPotionPool
```

**커스텀 비주얼 경로 오버라이드 (BaseLib 제공 훅 전량)**

```
CustomVisualPath                    res://Wanderer/scenes/wanderer/wanderer.tscn
CustomRestSiteAnimPath              res://Wanderer/scenes/wanderer/wanderer_rest_site.tscn
CustomMerchantAnimPath              res://Wanderer/scenes/wanderer/wanderer_merchant.tscn
CustomCharacterSelectBg             res://Wanderer/scenes/wanderer/char_select_bg_wanderer.tscn
CustomIconPath                      res://Wanderer/scenes/wanderer/wanderer_icon.tscn
CustomIconTexturePath               res://Wanderer/images/wanderer/character_icon_wanderer.png
CustomCharacterSelectIconPath       res://Wanderer/images/wanderer/char_select_wanderer.png
CustomCharacterSelectLockedIconPath res://Wanderer/images/wanderer/char_select_wanderer_locked.png
CustomMapMarkerPath                 res://Wanderer/images/wanderer/map_marker_wanderer.png
CustomArmPointingTexturePath        res://Wanderer/images/ui/hands/multiplayer_hand_wanderer_point.png
CustomArmRockTexturePath            res://Wanderer/images/ui/hands/multiplayer_hand_wanderer_rock.png
CustomArmPaperTexturePath           res://Wanderer/images/ui/hands/multiplayer_hand_wanderer_paper.png
CustomArmScissorsTexturePath        res://Wanderer/images/ui/hands/multiplayer_hand_wanderer_scissors.png
CustomEnergyCounter                 new CustomEnergyCounter(fmt, Color("8BB5C4"), Color("8BB5C4"))
```

멀티플레이 가위바위보 손 텍스처까지 캐릭터마다 필요하다는 점은 직접 만들 때 놓치기 쉬운 부분입니다.

**풀 클래스들**

```csharp
WandererCardPool : CustomCardPoolModel
  Title => "Wanderer"
  BigEnergyIconPath  => res://Wanderer/images/ui/combat/wanderer_energy_icon.png
  TextEnergyIconPath => res://Wanderer/images/ui/combat/text_wanderer_energy_icon.png
  H / S / V          => (카드 색상 HSV 오버라이드)
  DeckEntryCardColor => Wanderer.Color
  IsColorless        => false

WandererRelicPool  : CustomRelicPoolModel   // LabOutlineColor = Wanderer.Color
WandererPotionPool : CustomPotionPoolModel  //  ↑ 동일, 에너지 아이콘은 charui/ 경로
```

---

## 4. ★ 스탠스 시스템 (핵심) ★

### 4.1 Stance enum

```csharp
namespace Wanderer.WandererCode.Commands;

public enum Stance
{
    Chudan = 0,   // 中段 — 힘 (기본 공격 스탠스)
    Hasso  = 1,   // 八相 — 드로우
    Gedan  = 2,   // 下段 — 민첩 + 카운터 (방어)
    Jodan  = 3,   // 上段 — 소멸 + 기력 (해금형)
    Waki   = 4,   // 脇構 — Shift + 유지 (해금형)
}
```

`Chudan / Hasso / Gedan` 3종이 **기본 개방**, `Jodan / Waki` 2종은 **전투 중 해금형**입니다. 해금 플래그는 `HashSet<Creature> _jodanEnabled / _wakiEnabled` 로 관리되고, 해당 스탠스에 **실제로 진입하는 순간** `EnterStance` 내부에서 자동으로 추가됩니다.

### 4.2 `IStancePower` — 스탠스의 실체

```csharp
namespace Wanderer.WandererCode.Interfaces;

public interface IStancePower
{
    Stance Stance { get; }
}
```

5개 파워가 전부 `WandererPower`(= `BaseLib.Abstracts.CustomPowerModel`)를 상속하면서 이 인터페이스를 구현합니다.

```csharp
class ChudanPower : WandererPower, IStancePower { Stance => Stance.Chudan; Type => 1; StackType => 1; }
class HassoPower  : WandererPower, IStancePower { Stance => Stance.Hasso;  ... }
class GedanPower  : WandererPower, IStancePower { Stance => Stance.Gedan;  ... }
class JodanPower  : WandererPower, IStancePower { Stance => Stance.Jodan;  ... }
class WakiPower   : WandererPower, IStancePower { Stance => Stance.Waki;   ... }
```

- `Type => 1`, `StackType => 1` — 5개 전부 동일. (버프 / 비중첩)
- **"현재 스탠스"라는 별도 변수가 없습니다.** 아래 한 줄이 곧 상태 조회:

```csharp
public static IStancePower? GetCurrentStancePower(this Creature creature)
    => creature.Powers.OfType<IStancePower>().FirstOrDefault();
```

이 설계의 장점: 파워 시스템이 이미 처리하는 **세이브/로드·멀티플레이 동기화·툴팁·UI 아이콘 표시**를 전부 공짜로 얻습니다. 직접 만들 때 `Dictionary<Player, MyStance>` 같은 걸 들고 있으면 이 전부를 직접 구현해야 합니다.

### 4.3 ★ `WandererCmd.EnterStance` — 전환의 유일한 관문 ★

IL 역구성 결과 (비동기 상태머신 `<EnterStance>d__29` 기준):

```csharp
public static async Task EnterStance(
    PlayerChoiceContext choiceContext,
    Creature creature,
    Stance stance,
    int amount)
{
    // ── 1) 현재 스탠스 조회 ──────────────────────────────
    IStancePower oldStancePower = GetCurrentStancePower(creature);

    // ── 2) 다른 스탠스로 바뀌는 경우에만 "이탈" 처리 ──────
    if (oldStancePower != null && oldStancePower.Stance != stance)
    {
        // 2-a) Steady가 하나라도 남아 있으면 전환 자체를 취소 (return)
        if (creature.Powers.OfType<SteadyPower>().Any(p => p.Amount > 0))
            return;

        // 2-b) 기존 스탠스 파워 제거
        await PowerCmd.Remove((PowerModel)oldStancePower);

        // 2-c) 이탈 카운터 증가 (Flourish 카드 등이 참조)
        _leftStanceCounts[creature] = GetLeftStanceCounts(creature) + 1;

        // 2-d) 이탈 이벤트 방송
        await AfterStanceLeft(creature, oldStancePower.Stance);
    }

    // ── 3) 새 스탠스 파워 적용 ───────────────────────────
    switch (stance)
    {
        case Stance.Chudan:
            await PowerCmd.Apply<ChudanPower>(choiceContext, creature, amount, creature, null, false);
            break;
        case Stance.Hasso:
            await PowerCmd.Apply<HassoPower>(choiceContext, creature, amount, creature, null, false);
            break;
        case Stance.Gedan:
            await PowerCmd.Apply<GedanPower>(choiceContext, creature, amount, creature, null, false);
            break;
        case Stance.Jodan:
            await PowerCmd.Apply<JodanPower>(choiceContext, creature, amount, creature, null, false);
            EnableJodan(creature);      // ← 전투 내 영구 해금
            break;
        case Stance.Waki:
            await PowerCmd.Apply<WakiPower>(choiceContext, creature, amount, creature, null, false);
            EnableWaki(creature);       // ← 전투 내 영구 해금
            break;
    }

    // ── 4) 시각 효과 ────────────────────────────────────
    WandererVisuals.SetStance(creature, stance.ToString().ToLowerInvariant());

    // ── 5) 진입 이벤트 방송 ─────────────────────────────
    await AfterStanceEntered(creature, stance);
}
```

**여기서 읽어내야 할 설계 결정 6가지**

1. **같은 스탠스 재진입은 이탈 처리를 건너뜀.** `oldStancePower.Stance != stance` 조건 때문입니다. 3단계의 `PowerCmd.Apply`만 다시 호출되고, 그 결과 `AfterPowerAmountChanged` 훅이 다시 돌아 스탠스 효과가 재발동합니다. 이것이 **"Reenter your Stance"** (Tamp / Fudōshin / Stolid Strike)의 정확한 동작 원리입니다. 별도 `ReenterStance` 함수가 없습니다.
2. **Steady 검사는 "이탈이 발생할 때만" 수행.** 같은 스탠스 재진입은 Steady가 있어도 통과합니다. 그래서 `Fudōshin`(턴 시작마다 Steady 획득 + 스탠스 재진입)이 자기 자신에게 막히지 않습니다. — 매우 영리한 순서 배치이고, 순서를 바꾸면 즉시 데드락이 납니다.
3. **전환 실패가 `return`(무음)이다.** 예외도, 로그도, 플레이어 피드백도 없습니다. 실제 모드로 만든다면 여기에 "막힘" 연출을 추가하는 게 좋습니다.
4. **해금(`EnableJodan/Waki`)이 진입의 부작용.** "Jodan Stance 카드 = 진입 + 전투 내 해금"이 별도 코드 없이 성립합니다.
5. **`amount` 파라미터로 스탠스 강도를 조절.** 스탠스 파워의 `Amount`가 효과 배수로 쓰입니다(§4.5). `Mu-gamae` 카드가 이걸 활용합니다.
6. **이탈 카운터는 이탈 시에만 증가.** 재진입은 카운트되지 않음 → `Flourish`("이번 전투에 스탠스를 떠난 횟수만큼 반복")가 재진입 스팸으로 폭발하지 않습니다.

### 4.4 `GetRandomStance` — 랜덤 전환

```csharp
public static Stance GetRandomStance(Creature creature, bool different)
{
    var pool = new List<Stance>(3) { Stance.Chudan, Stance.Hasso, Stance.Gedan };  // 인덱스 0,1,2
    if (IsJodanEnabled(creature)) pool.Add(Stance.Jodan);
    if (IsWakiEnabled(creature))  pool.Add(Stance.Waki);

    var current = GetCurrentStancePower(creature);
    if (different && current != null && pool.Contains(current.Stance))
        pool.Remove(current.Stance);

    // 결정론적 RNG 스트림 사용 (멀티플레이/리플레이 동기화)
    return creature.Player.RunState.Rng.CombatCardSelection.Choose(pool);
}
```

**핵심**: `Random`이나 `GD.randi()`가 아니라 `RunState.Rng.CombatCardSelection` 스트림을 씁니다. STS2는 시드 기반 결정론이 전제이므로, 직접 만들 때도 **반드시 `RunRngSet`의 적절한 스트림**을 써야 합니다. 아니면 멀티플레이에서 상태가 갈라집니다.

사용처: `Suikyō Strike`("다른 랜덤 스탠스로 진입"), `OverflowingPower`("카드를 낼 때마다 다른 랜덤 스탠스").

### 4.5 스탠스 효과 구현 패턴 — 이중 훅

5개 스탠스 파워 모두 **완전히 동일한 형태**를 따릅니다:

```csharp
// 훅 A: 스탠스에 "진입"했을 때 (= 파워가 적용/증가했을 때)
public override async Task AfterPowerAmountChanged(
    PlayerChoiceContext ctx, PowerModel power, decimal amount, ..., ...)
{ /* 효과 발동, 세기 = amount */ }

// 훅 B: 스탠스를 "유지한 채 턴 시작"
public override async Task AfterPlayerTurnStart(PlayerChoiceContext ctx, Player player)
{
    if (player != Owner.Player) return;      // 소유자 확인 가드 (반드시 필요)
    /* 효과 발동, 세기 = this.Amount */
}
```

→ 로컬라이즈 텍스트의 *"On enter and turn start, ..."* 가 정확히 이 두 훅에 대응합니다.

#### 스탠스별 실제 효과

| 스탠스 | 효과 | 구현 |
|---|---|---|
| **Chudan** (中段) | 진입/턴시작 시 이번 턴 **힘 +2** | `PowerCmd.Apply<ChudanStrengthPower>(ctx, Owner, CanonicalVar("ChudanStrengthPower").BaseValue × Amount, Owner, null, false)` |
| **Hasso** (八相) | 진입/턴시작 시 **다음 턴 드로우 +1** 부여 | `PowerCmd.Apply<WandererNextTurnDrawPower>(..., DynamicVars.Cards.BaseValue × Amount, ...)` |
| **Gedan** (下段) | 진입/턴시작 시 이번 턴 **민첩 +1 & 카운터 +1** | `Apply<GedanDexterityPower>(Amount)` 후 `Apply<CounterPower>(Amount)` — 2연속 await |
| **Jodan** (上段) | 진입/턴시작 시 **카드 1장 소멸**, 소멸할 때마다 **기력(Vigor) +5** | 진입 시 `RunAsHookAction(ctx => ExhaustCards(ctx, (int)amount))`, 턴시작 시 `ExhaustCards(ctx, (int)Amount)`, 별도로 `AfterCardExhausted` → `Apply<VigorPower>(PowerVar("VigorPower").BaseValue)` |
| **Waki** (脇構) | 진입/턴시작 시 **카드 1장 Shift + 유지(Retain)**, 유지 카드 전체 코스트 −1 | `ShiftAndRetain(ctx, count)` |

#### 세부 구현 조각

**Chudan — 동적 변수에서 수치를 읽는 방식**

```csharp
// 기본값 2를 코드에 하드코딩하지 않고 CanonicalVars로 선언 → 로컬라이즈 {ChudanStrengthPower}와 자동 연동
public override IReadOnlyList<DynamicVar> CanonicalVars =>
    [ new PowerVar<ChudanStrengthPower>(2m) ];

public override IReadOnlyList<IHoverTip> ExtraHoverTips =>
    [ HoverTipFactory.FromPower<ChudanStrengthPower>() ];

// 발동 시
var baseVal = Owner.DynamicVars["ChudanStrengthPower"].BaseValue;   // 문자열 키로 접근
await PowerCmd.Apply<ChudanStrengthPower>(ctx, Owner, baseVal * Amount, Owner, null, false);
```

`ChudanStrengthPower : WandererTemporaryStrengthPower`, `OriginModel => ModelDb.Card<EnterChudan>()`
(→ 파워 툴팁에서 "어디서 온 효과인지" 역추적 가능하게 하는 BaseLib 규약)

**Hasso** — `CanonicalVars => [ new CardsVar(1m) ]`, 발동 시 `Owner.DynamicVars.Cards.BaseValue`로 접근 (`Cards`는 전용 프로퍼티).

**Jodan — `RunAsHookAction` 래퍼 (중요)**

`AfterPowerAmountChanged` 훅 안에서 **플레이어 입력이 필요한 작업**(카드 선택 등)을 하려면 그냥 `await` 하면 안 됩니다. `WandererPower.RunAsHookAction`을 통해야 합니다:

```csharp
protected async Task RunAsHookAction(Func<PlayerChoiceContext, Task> work)
{
    if (!LocalContext.NetId.HasValue) return;
    var ctx = new HookPlayerChoiceContext(LocalContext.NetId.Value, CombatState, true);
    await ctx.AssignTaskAndWaitForPauseOrCompletion(work(ctx));
}

// Jodan 사용부
public override async Task AfterPowerAmountChanged(...)
{
    await RunAsHookAction(ctx => ExhaustCards(ctx, (int)amount));
}

private async Task ExhaustCards(PlayerChoiceContext ctx, int amount)
{
    var prefs = new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, ...);
    foreach (var card in await CardSelectCmd.FromHand(ctx, Owner.Player, amount, prefs))
        await CardCmd.Exhaust(ctx, card, ...);
}
```

Waki도 동일 패턴(`RunAsHookAction(ctx => ShiftAndRetain(ctx, (int)amount))`)을 씁니다.
**→ 훅에서 UI를 띄워야 할 때의 정답 패턴. 이거 모르면 멀티플레이에서 데드락/desync 납니다.**

**Waki — Shift + Retain + 코스트 감소**

```csharp
private static readonly LocString ShiftAndRetainPrompt =
    new LocString("card_selection", "WANDERER-TO_SHIFT_AND_RETAIN");

private async Task ShiftAndRetain(PlayerChoiceContext ctx, int count)
{
    await WandererCmd.PickAndShiftCardsFromHand(
        ctx, count, Owner.Player, this, upgrade: false,
        addKeywords: [ CardKeyword.Retain ],       // Shift 결과물에 Retain 부여
        prompt: ShiftAndRetainPrompt);

    // 손패의 Retain 카드 전부 코스트 -1 (이번에 낼 때까지)
    foreach (var c in PileType.Hand.GetPile(Owner.Player).Cards
                       .Where(c => c.Keywords.Contains(CardKeyword.Retain)))
        c.EnergyCost.AddUntilPlayed(-1);
}
```

### 4.6 `SteadyPower` — 전환 차단

```csharp
class SteadyPower : WandererPower   // IStancePower 아님!
{
    Type => 1; StackType => 1;
    // 턴 종료(늦은 시점)마다 1 감소
    public override async Task AfterSideTurnEndLate(ctx, CombatSide side, participants)
    {
        if (side != /*플레이어 측*/) return;
        await PowerCmd.Decrement(this, 1);
    }
}
```

로컬라이즈: *"You cannot leave your Stance."* / *"For the next {Amount} turns, you cannot leave your Stance."*

차단 로직 자체는 `EnterStance` 안에 한 줄(§4.3 2-a)로만 존재합니다. **파워가 전환을 가로채는 게 아니라, 전환 함수가 파워를 조회**하는 구조 — 훨씬 단순하고 디버깅이 쉽습니다.

연계 파워:
- `LongDrawsPower` — "Steady를 얻을 때마다 카드 N장 드로우"
- `FudoshinPower` — 턴 시작마다 `Apply<SteadyPower>(Amount)` + 현재 스탠스 재진입 ×Amount
- 카드: `Tamp`, `Big Hit`, `Inhale, Exhale`, `Stolid Strike`(Steady 2 획득), `Ichigeki`(Steady당 피해 증가)

### 4.7 스탠스 이벤트 버스 — `IWandererEventListener`

BaseLib이 제공하지 않는 모드 고유 이벤트를 직접 방송합니다.

```csharp
public interface IWandererEventListener
{
    Task BeforeRitualDeath(Creature creature);
    Task AfterEnteredShinigami(Creature creature);
    Task AfterStanceLeft(Creature creature, Stance oldStance);
    Task AfterStanceEntered(Creature creature, Stance stance);
    Task AfterShifted(CardModel card);
    Task AfterRefilled(CardModel card);
}
```

두 기반 클래스가 전부 구현하고 **빈 메서드로 기본 제공**합니다 → 필요한 카드/파워만 override.

```csharp
class WandererCard  : CustomCardModel,  IWandererEventListener { /* 전부 no-op 기본 */ }
class WandererPower : CustomPowerModel, IWandererEventListener { /* 전부 no-op 기본 */ }
```

**리스너 수집 방식** (`WandererCmd.GetListeners<T>`):

```csharp
private static List<T> GetListeners<T>(Creature creature)
{
    var list = new List<T>();
    var p = creature.Player;
    list.AddRange(PileType.Hand   .GetPile(p).Cards.OfType<T>());
    list.AddRange(PileType.Draw   .GetPile(p).Cards.OfType<T>());
    list.AddRange(PileType.Discard.GetPile(p).Cards.OfType<T>());
    list.AddRange(PileType.Exhaust.GetPile(p).Cards.OfType<T>());
    list.AddRange(creature.Powers.OfType<T>());
    list.AddRange(creature.Player.Relics.OfType<T>());
    return list;
}
```

방송:

```csharp
public static async Task AfterStanceLeft(Creature creature, Stance oldStance)
{
    foreach (var l in GetListeners<IWandererEventListener>(creature))
        await l.AfterStanceLeft(creature, oldStance);   // 순차 await
}
// AfterStanceEntered / AfterShifted / AfterRefilled / BeforeRitualDeath / AfterEnteredShinigami 동일 형태
```

⚠️ **주의점 (직접 구현 시 함정)**
- **덱(Deck) 파일은 포함 안 됨.** 전투 중 4개 파일 + 파워 + 유물만 리스너가 됩니다.
- **순차 `await`**이라 리스너가 많으면 느려집니다. 매 전환마다 6개 컬렉션을 `OfType`으로 새로 훑으므로 캐싱이 없습니다. 카드 수가 많은 덱에서는 최적화 여지.
- 리스너 순회 중에 카드가 파일 사이를 이동하면 컬렉션 변경 예외 위험 — 원본은 `List`로 복사해 담으므로 안전합니다.

**실제 구독자**:
- `FlowPower` — `AfterStanceLeft` 구독 → "스탠스를 떠날 때마다 손패 랜덤 1장 코스트 −1"
- `WandererCard.ShiftSelfAfterPlay` — `AfterShifted` 경유 Refill 체인

### 4.8 스탠스 선택 UI — `Kamae` 카드

별도 UI를 만들지 않고 **토큰 카드 5종 + 카드 선택 화면**으로 처리합니다.

```csharp
class Kamae : WandererCard
{
    CanonicalKeywords => [ /* 단일 키워드 */ ];

    // 선택지로 띄울 임시 카드 생성 (모델 복제 + 소유자 주입)
    private CardModel CreateChoiceCard<T>() where T : CardModel
    {
        var c = ModelDb.Card<T>().MutableClone();
        c.Owner = this.Owner;
        return c;
    }

    public override async Task OnPlay(PlayerChoiceContext ctx, CardPlay cardPlay)
    {
        var creature = Owner.Creature;
        var choices = new List<CardModel>();

        // 현재 들어가 있는 스탠스는 선택지에서 제외 (= "다른 스탠스로 진입")
        if (!creature.Powers.OfType<JodanPower>().Any()  && WandererCmd.IsJodanEnabled(creature))
            choices.Add(CreateChoiceCard<EnterJodan>());
        if (!creature.Powers.OfType<ChudanPower>().Any())
            choices.Add(CreateChoiceCard<EnterChudan>());
        if (!creature.Powers.OfType<HassoPower>().Any())
            choices.Add(CreateChoiceCard<EnterHasso>());
        if (!creature.Powers.OfType<GedanPower>().Any())
            choices.Add(CreateChoiceCard<EnterGedan>());
        if (!creature.Powers.OfType<WakiPower>().Any()   && WandererCmd.IsWakiEnabled(creature))
            choices.Add(CreateChoiceCard<EnterWaki>());

        if (choices.Count == 0) return;
        if (Owner.HasPower<SteadyPower>() && WandererCmd.GetCurrentStancePower(creature) != null)
            return;                                     // Steady면 선택창 자체를 안 띄움

        var picked = await WandererCmd.ChooseCard(ctx, choices, Owner, canSkip: false);
        await ((IEnterStance)picked).OnEnter(ctx, cardPlay, 1);

        if (IsUpgraded)
            await CardPileCmd.Draw(ctx, Owner, 1m);      // 업그레이드: 1장 드로우
    }
}
```

**`IEnterStance` 인터페이스** (전역 네임스페이스):

```csharp
interface IEnterStance { Task OnEnter(PlayerChoiceContext ctx, CardPlay cardPlay, int amount); }
```

**진입 토큰 카드 5종** — `EnterChudan / EnterHasso / EnterGedan / EnterJodan / EnterWaki`

```csharp
class EnterChudan : WandererCard, IEnterStance
{
    public override CardPoolModel Pool => ModelDb.CardPool<TokenCardPool>();   // ★ 토큰 풀 → 보상에 안 나옴
    public override IReadOnlyList<IHoverTip> WandererExtraHoverTips =>
        [ HoverTipFactory.FromPower<ChudanPower>() ];

    public async Task OnEnter(PlayerChoiceContext ctx, CardPlay cardPlay, int amount)
        => await WandererCmd.EnterStance(ctx, Owner.Creature, Stance.Chudan, amount);
}
```

`Pool => TokenCardPool`이 **핵심**입니다. 이걸 지정해야 카드 보상/상점/도서관에 노출되지 않습니다.

**`WandererCmd.ChooseCard`** — 멀티플레이 안전한 선택 구현:

```csharp
public static async Task<CardModel> ChooseCard(PlayerChoiceContext ctx,
    IReadOnlyList<CardModel> cards, Player player, bool canSkip)
{
    uint choiceId = RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId();
    ctx.SignalPlayerChoiceBegun(...);

    if (LocalContext.IsMe(player) /* && 넷 타입 체크 */)
    {
        NPlayerHand.Instance?.CancelAllCardPlay();
        foreach (var c in cards) SaveManager.Instance.MarkCardAsSeen(c);
        await NChooseACardSelectionScreen.ShowScreen(...);
        var sel = NChooseACardSelectionScreen.CardsSelected.FirstOrDefault();
        RunManager.Instance.PlayerChoiceSynchronizer
            .SyncLocalChoice(choiceId, PlayerChoiceResult.FromIndex(cards.IndexOf(sel)));
    }
    var result = await RunManager.Instance.PlayerChoiceSynchronizer.WaitForRemoteChoice(choiceId);
    var chosen = cards[result.AsIndex()];
    ctx.SignalPlayerChoiceEnded(...);
    return chosen;
}
```

> **로컬에서 UI를 띄우고 → 인덱스만 동기화 → 모두가 인덱스로 결과를 복원**. 커스텀 선택 UI를 만들 때 이 3단계를 그대로 따라야 합니다.

### 4.9 스탠스 시각 효과 — `WandererVisuals`

```csharp
static class WandererVisuals
{
    private static readonly Dictionary<string, Texture2D> _stanceTextures = new();
    public static readonly string[] Stances = { "chudan", "gedan", "hasso", "jodan", "waki" };
    public static readonly Color ShinigamiTint = new Color(/* White 기반 변형 */);

    public static void SetStance(Creature creature, string stance)
    {
        var sprite = GetSprite(creature, "Sprite");
        if (sprite == null) return;
        if (_stanceTextures.Count == 0) LoadTextures();
        if (_stanceTextures.TryGetValue(stance, out var tex)) sprite.Texture = tex;
        else GD.PrintErr("[WandererVisuals] Unknown stance: " + stance);
    }

    public static void SetShinigamiActive(Creature creature, bool active)
    {
        var sprite = GetSprite(creature, "Sprite");
        if (sprite != null) sprite.Modulate = active ? ShinigamiTint : Colors.White;
        var corpse = GetSprite(creature, "Corpse");
        if (corpse != null) corpse.Visible = active;
    }

    public static void ApplyDeadState(NCreatureVisuals visuals)
    {
        var body   = visuals.GetCurrentBody();
        var sprite = body.GetNodeOrNull<Sprite2D>("Sprite");
        var corpse = body.GetNodeOrNull<Sprite2D>("Corpse");
        if (sprite != null) { sprite.Visible = false; sprite.Modulate = Colors.White; }
        if (corpse != null) corpse.Visible = true;
    }

    private static Sprite2D GetSprite(Creature creature, string name)
        => NCombatRoom.Instance?.GetCreatureNode(creature)?.Visuals?
             .GetCurrentBody()?.GetNodeOrNull<Sprite2D>(name);

    private static void LoadTextures()
    {
        foreach (var s in Stances)
        {
            var tex = GD.Load<Texture2D>("res://Wanderer/images/wanderer/" + s + ".png");
            if (tex != null) _stanceTextures[s] = tex;
            else GD.PrintErr("[WandererVisuals] Failed to load texture for stance: " + s);
        }
    }
}
```

**→ 씬(`wanderer.tscn`)의 body 노드 안에 `Sprite`와 `Corpse`라는 이름의 `Sprite2D` 두 개가 반드시 있어야 합니다.** 스탠스 전환 = `Sprite.Texture` 교체, 사망 = `Sprite` 숨기고 `Corpse` 표시. 애니메이션이 아니라 정적 텍스처 스왑이라 구현이 매우 가볍습니다.

### 4.10 스탠스 관련 정적 상태 (사이드카)

```csharp
static class WandererCmd  // abstract sealed = static class
{
    public static int DefaultShinigamiMaxHp { get; set; }
    public static int ShinigamiExhaustThreshold { get; set; }

    private static readonly HashSet<Creature> _jodanEnabled = new();
    private static readonly HashSet<Creature> _wakiEnabled  = new();
    private static readonly Dictionary<Creature, int> _leftStanceCounts = new();
    private static readonly Dictionary<Creature, int> _shiftCounts      = new();
    private static readonly Dictionary<Creature, ShinigamiState> _shinigamiStates = new();
    private static readonly Dictionary<CardModel, CardModel> _ofudaShiftedCards = new();
    private static readonly HashSet<CardModel> _pendingShinigamiShifts = new();
    private static readonly Dictionary<CardModel, CardModel> _refillBackups = new();
    private static readonly Dictionary<CardModel, CardModel> _ofudaRefillContinuations = new();
    public static readonly LocString DefaultShiftPrompt =
        new LocString("card_selection", "WANDERER-TO_SHIFT");

    public static int GetLeftStanceCounts(Creature c) => _leftStanceCounts.TryGetValue(c, out var v) ? v : 0;
    public static int GetShiftCount(Creature c)       => _shiftCounts.TryGetValue(c, out var v) ? v : 0;
    public static void IncrementShiftCount(Creature c) => _shiftCounts[c] = GetShiftCount(c) + 1;
    public static bool IsJodanEnabled(Creature c) => _jodanEnabled.Contains(c);
    public static bool IsWakiEnabled(Creature c)  => _wakiEnabled.Contains(c);
    public static void EnableJodan(Creature c) => _jodanEnabled.Add(c);
    public static void EnableWaki(Creature c)  => _wakiEnabled.Add(c);

    public static void Reset()   // 전투 시작 시 호출되는 듯
    {
        _ofudaShiftedCards.Clear();
        _ofudaRefillContinuations.Clear();
        _pendingShinigamiShifts.Clear();
        /* ... */
    }
}
```

⚠️ **이 설계의 알려진 위험** — 직접 만들 때 반드시 인지:
- `static Dictionary<Creature, ...>` 는 **세이브에 저장되지 않습니다.** 전투 중 저장 → 종료 → 재개 시 Jodan/Waki 해금, 이탈 카운터, Shift 카운터가 **전부 날아갑니다.**
- `Reset()`에서 `_jodanEnabled` / `_wakiEnabled` / `_leftStanceCounts` / `_shiftCounts`가 지워지는지 IL상 명확히 확인되지 않았습니다(확인된 건 Ofuda 관련 3개). 전투 간 누수 가능성 있음.
- `Creature` 키를 `HashSet`에 계속 넣으므로 **메모리 누수** 소지.
- **권장 대안**: 저장이 필요한 카운터는 `Amount`만 쓰고 표시되지 않는 숨김 PowerModel(예: `StanceLeftCounterPower`)로 보관하거나, BaseLib이 제공하는 영속 상태 API를 쓰는 편이 안전합니다.

### 4.11 스탠스를 건드리는 전체 목록

**카드 (13곳에서 `EnterStance` 호출)**

| 카드 | 동작 |
|---|---|
| `Kamae` | 현재와 다른 스탠스 중 택1 (업글: +1 드로우) |
| `EnterChudan/Hasso/Gedan/Jodan/Waki` | 토큰. 해당 스탠스 진입 |
| `Jodan` (카드) | Jodan 진입 + 전투 내 해금 |
| `Waki` (카드) | Waki 진입 + 전투 내 해금 |
| `Mu-gamae` | 스탠스에 **N번** 진입 (업글: +1 드로우) |
| `Suikyō Strike` | 피해 + 다른 랜덤 스탠스 진입 |
| `Tamp` | 스탠스 **재진입** + Steady 2 |
| `Stolid Strike` | 전체 피해 + 재진입 + Steady 2 |
| `Press` / `Yield` / `Kiai` | `Kamae`를 손으로 (간접 전환) |

**파워**

| 파워 | 동작 |
|---|---|
| `FudoshinPower` | 턴 시작마다 Steady 획득 + 스탠스 재진입 |
| `OverflowingPower` | 카드를 낼 때마다 다른 랜덤 스탠스 진입 |
| `FlowPower` | 스탠스 **이탈** 시 손패 랜덤 1장 코스트 −1 |
| `NoPlanPower` / `FlailingPower` / `ImprovisePower` | Shift 트리거 (스탠스와는 별개 축) |

**카드 수치가 스탠스 카운터를 참조하는 곳**
- `Flourish` — `GetLeftStanceCounts`(이번 전투 이탈 횟수)만큼 전체 피해 반복
- `The Spins` — `GetShiftCount`만큼 추가 피해

---

## 5. 두 번째 축 — Shift / Ofuda / Refill 시스템

스탠스와 독립적인 두 번째 기믹입니다. 자체 모드에서 "변신/변화" 계열을 만들 계획이 있다면 통째로 참고 가치가 있습니다.

### 5.1 커스텀 키워드

```csharp
static class WandererKeywords
{
    public static CardKeyword Enshrined;   // 이 카드는 Shift 불가
    public static CardKeyword Refill;      // 낸 뒤 Shift되고, 다시 Shift하면 원형 복귀
    public static CardKeyword Refilling;   // Refill 진행 중 상태 마커

    public static HoverTip ShiftHoverTip =>
        new HoverTip(new LocString("card_keywords", "WANDERER-SHIFT.title"),
                     new LocString("card_keywords", "WANDERER-SHIFT.description"));
    public static HoverTip RemoveDishonorHoverTip => /* 동일 패턴 */;
}
```

### 5.2 `ShiftCard` 핵심 로직

```csharp
public static async Task ShiftCard(CardModel card, Player player, bool upgrade,
                                   IEnumerable<CardKeyword> addKeywords)
{
    if (card.Keywords.Contains(WandererKeywords.Enshrined)) return;   // ★ 차단

    // 1) Ofuda 되돌리기인 경우
    var original = GetOriginalCard(card);
    if (original != null) { await CardCmd.Transform(card, original, ...); _ofudaShiftedCards.Remove(card); ... }

    // 2) Refill 백업이 있으면 원형 복귀
    if (_refillBackups.TryGetValue(card, out var backup)) {
        var clone = card.CombatState.CloneCard(backup);
        await CardCmd.Transform(card, clone, ...);
        _refillBackups.Remove(card);
        card.RemoveKeyword(WandererKeywords.Refilling);
        (card as WandererCard)?.ClearRuntimeHoverTips();
        omiki?.Flash();
        EstablishRefillBond(result, backup);
        return;
    }

    // 3) 일반 Shift: 캐릭터 카드풀에서 후보 추출
    var pool = player.Character.CardPool
        .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
        .Where(c => !c.Keywords.Contains(Enshrined))
        .Where(c => omiki == null || c.Keywords.Contains(Refill) == card.Keywords.Contains(Refill)); // Omiki 유물
    var newCard = await CardCmd.Transform(card,
        new CardTransformation(pool).Yield(player.RunState.Rng.CombatCardGeneration), ...);

    if (upgrade && newCard.IsUpgradable) await CardCmd.Upgrade(newCard, ...);
    foreach (var k in addKeywords) newCard.AddKeyword(k);

    await AfterShifted(newCard);
    if (newCard.Keywords.Contains(Refill)) await AfterRefilled(newCard);
}
```

포인트:
- **Omiki 유물**이 `Where` 절 하나로 "Refill 카드는 Refill 카드로만 변한다"를 구현 (*Cards with Refills always Shift into another card with Refills*)
- RNG는 `Rng.CombatCardGeneration` 스트림
- `Enshrined` 검사가 **두 군데**(함수 진입부 + 풀 필터)에 있음

### 5.3 Shinigami (사망 → 부활 루프)

`BrokenJuzuRelic`(시작 유물): *On death, Shift ALL cards to Ofuda and apply 6 Shinigami.*

```csharp
EnterShinigamiForm(player):
    state = GetOrCreateState(creature);
    juzu  = GetJuzuRelic(creature);
    if (juzu.ShinigamiCurrentHp == null) juzu.ShinigamiCurrentHp = /* 초기화 */;
    creature.SetMaxHpInternal(juzu.ShinigamiMaxHp);         // ★ 최대 HP를 시니가미 HP로 치환
    await CreatureCmd.Heal(creature, juzu.ShinigamiCurrentHp, ...);
    await PowerCmd.Apply<ShinigamiPower>(new BlockingPlayerChoiceContext(...),
                                        creature, ShinigamiExhaustThreshold, ...);
    _pendingShinigamiShifts.UnionWith(PileType.Exhaust.GetPile(player).Cards);
    await ShiftAllCardsToOfuda(player);                     // 손/드로우/버림 전부 Ofuda로
    ApplyShinigamiVisuals(creature, state);

ExitShinigamiForm(creature):
    juzu.ShinigamiMaxHp     = creature.MaxHp;
    juzu.ShinigamiCurrentHp = creature.CurrentHp;           // 시니가미 HP를 유물에 보존 (= 세이브됨!)
    await RestoreAllCards(player);
    creature.SetMaxHpInternal(state.OriginalMaxHp);
    await CreatureCmd.SetCurrentHp(creature, 1m /*또는 저장값*/);
    await PowerCmd.Remove<ShinigamiPower>(creature);
    ResetShinigamiVisuals(creature, state);
```

**주목**: 시니가미 HP를 `BrokenJuzuRelic`의 프로퍼티에 저장합니다. 유물 모델은 세이브 대상이므로 **런 전체에 걸쳐 지속**됩니다. §4.10의 static dict 문제를 여기서는 유물로 우회한 셈 — **영속 상태가 필요하면 유물/파워 모델에 얹어라**는 교훈.

`RitualDeath(creature)`:
```csharp
await BeforeRitualDeath(creature);   // 이벤트 방송 (내부에서 SetStoredHp(creature.CurrentHp))
await CreatureCmd.Kill(creature, ...);
```
→ `Seppuku`, `Play Dead`, `Die.`, `Lethal Exchange`, `Kasutori` 포션이 전부 이걸 호출합니다.

---

## 6. Harmony 패치 9종 (실제 타겟 메서드 확인 완료)

| 패치 클래스 | 타겟 | 종류 | 목적 |
|---|---|---|---|
| `ArchaicTooth_Patches+GetStarterPatch` | `Relics.ArchaicTooth.GetTranscendenceStarterCard` | Postfix | 덱에서 `Kamae`를 스타터 카드로 인식 |
| `ArchaicTooth_Patches+GetTransformedPatch` | `Relics.ArchaicTooth.GetTranscendenceTransformedCard` | Prefix | `Kamae` → `Mu-gamae`로 변환 (업그레이드/인챈트 승계) |
| `ArchaicTooth_Patches+TranscendenceCardsPatch` | `Relics.ArchaicTooth.TranscendenceCards` | Postfix | 목록에 `Mu-gamae` 추가 |
| `ColorfulPhilosophers_Patches` | `Models.Events.ColorfulPhilosophers` | Postfix | 이벤트 선택지에 `WandererCardPool` 추가 |
| `WandererRelicBlacklist_Patches` | `Runs.RelicGrabBag.Populate` | — | 특정 유물을 등장 풀에서 제외 |
| `NTopBarHp_Patches+OnFocusPatch` | `Nodes.TopBar.NTopBarHp.OnFocus` | Prefix | HP 툴팁에 **Shinigami HP** 줄 추가 |
| `NGameOverScreen_Patches+ApplyWandererCorpsePatch` | (게임오버 화면) | Postfix | `_creatureContainer` 리플렉션 → 시체 스프라이트 적용 |
| `NGeneralStatsGrid_Patches+LoadStatsPatch` | (통계 화면) | Postfix | `CreateCharacterSection` 리플렉션 호출로 Wanderer 통계 섹션 추가 |
| `ProgressSaveManager_Patches+ObtainEpochPatch` | `Saves.Managers.ProgressSaveManager.ObtainCharUnlockEpoch` | Prefix | 캐릭터 해금 시점 처리 |
| `RestSiteOption_Patches+IconGetterPatch` | `RestSite.RestSiteOption.Icon` | — | **Misogi** 휴식 옵션 아이콘 |
| `WandererMerchant_Patches+SkipPlayAnimationWithoutSpinePatch` | `Nodes.Screens.Shops.NMerchantCharacter.PlayAnimation` | Prefix | Spine 애니 없는 커스텀 상점 캐릭터에서 크래시 방지 |

**리플렉션 패턴** (private 멤버 접근):

```csharp
static readonly FieldInfo CreatureContainerField =
    AccessTools.Field(typeof(NGameOverScreen), "_creatureContainer");
static readonly MethodInfo CreateCharacterSectionMethod =
    AccessTools.Method(typeof(NGeneralStatsGrid), "CreateCharacterSection");
```

`SkipPlayAnimationWithoutSpinePatch`는 특히 실용적입니다. **커스텀 캐릭터가 Spine 애니메이션이 없으면 상점 화면에서 죽는 문제**를 Prefix로 우회 — 직접 만들 때 거의 확실히 만나게 될 버그입니다.

---

## 7. 로컬라이즈 구조

```
res://Wanderer/localization/{deu,eng,jpn,rus,zhs}/
    ancients.json         고대신 대화
    card_keywords.json    커스텀 키워드 툴팁
    card_selection.json   카드 선택 프롬프트
    cards.json            카드 이름/설명 (95종)
    characters.json       캐릭터 정보, 배너, 사세구(辞世句) 5종
    events.json           이벤트 연동 텍스트
    potions.json          포션 3종
    powers.json           파워 43종
    relics.json           유물 10종
    rest_site_ui.json     Misogi 휴식 옵션
    static_hover_tips.json  Shinigami HP 툴팁
```

**키 규약**: `WANDERER-{ENTRY}.title` / `.description` / `.smartDescription` / `.flavor` / `.selectionScreenPrompt`

**서식 태그**
```
[gold]...[/gold]      게임 키워드 강조
[purple]...[/purple]  저주/상태이상
[jitter]...[/jitter]  흔들림 애니메이션
{Damage:diff()}                업그레이드 차이 표시
{Cards:plural:card|cards}      복수형
{Amount:energyIcons(1)}        에너지 아이콘
{IfUpgraded:show:A|B}          업그레이드 분기
{InCombat:...|}                전투 중에만 표시
```

`description`(정적) 과 `smartDescription`(동적 변수 바인딩) 두 벌을 유지하는 것이 STS2 규약입니다.

**LocString 사용법**
```csharp
new LocString("card_selection", "WANDERER-TO_SHIFT_AND_RETAIN")   // (파일명, 키)
locString.Add("Current", value);                                   // 변수 주입
```

---

## 8. 전체 타입 인벤토리

| 네임스페이스 | 개수 | 내용 |
|---|---|---|
| `Wanderer` | 1 | `MainFile` |
| `.Character` | 4 | `Wanderer`, `WandererCardPool`, `WandererRelicPool`, `WandererPotionPool` |
| `.Commands` | 2 | `Stance` (enum), `WandererCmd` (53 메서드 / 12 필드) |
| `.Interfaces` | 2 | `IStancePower`, `IWandererEventListener` |
| `.Keywords` | 1 | `WandererKeywords` |
| `.Nodes` | 1 | `WandererVisuals` |
| `.Extensions` | 1 | `StringExtensions` |
| `.Cards` | 95 | 기반 `WandererCard` + 카드 94종 |
| `.Powers` | 43 | 기반 `WandererPower` + 파워 42종 |
| `.Relics` | 10 | 기반 `WandererRelic` + 유물 9종 |
| `.Potions` | 4 | 기반 `WandererPotion` + 포션 3종 |
| `.Patches` | 9 | Harmony 패치 |
| (컴파일러 생성) | 285 | async 상태머신, 람다 클로저 |
| **합계** | **459** | |

### 8.1 파워 목록 (43)

**스탠스 계열 (5)** `ChudanPower` `HassoPower` `GedanPower` `JodanPower` `WakiPower`
**스탠스 파생 (2)** `ChudanStrengthPower` `GedanDexterityPower`
**임시 스탯 기반 (2)** `WandererTemporaryStrengthPower` `WandererTemporaryDexterityPower`
**Next Turn 계열 (9)** `NextTurnPowers`(판정 헬퍼), `WandererNextTurnApplyPower<T>`(제네릭 기반), `WandererNextTurnBlockPower` `WandererNextTurnDamagePower` `WandererNextTurnDrawPower` `WandererNextTurnEnergyPower` `WandererNextTurnKimePower` `WandererNextTurnRoninFormPower` `WandererNextTurnTargetHeadPower` `WandererNextTurnVigorPower`
**표적 부위 (3)** `TargetHeadPower`(피해 2배) `TargetArmsPower`(피격 시 약화) `TargetLegsPower`(피격 시 취약)
**기타** `BarikedoPower` `CounterPower` `CouragePower` `DeathPactPower` `FlailingPower` `FlowPower` `FudoshinPower` `ImprovisePower` `JigokuJunbiPower` `LongDrawsPower` `NoPlanPower` `OverflowingPower` `RetaliatePower` `RoninFormPower` `SecretSaucePower` `SenNoSenPower` `ShinigamiPower` `SteadyPower` `StolenPeachPower` `YomiDraftPower`

`WandererNextTurnApplyPower<T>` — **제네릭 기반 클래스로 "다음 턴에 X를 적용" 계열을 한 번에 찍어내는 패턴.** 자체 모드에서도 반복되는 파워 유형이 있으면 그대로 쓸 만합니다.

### 8.2 유물 (10)

| 유물 | 효과 |
|---|---|
| `BrokenJuzuRelic` (시작) | 사망 시 모든 카드를 Ofuda로 Shift + Shinigami 6 |
| `KintsugiJuzuRelic` | 위 + 각 Ofuda의 원본 공개 |
| `ShinkyoRelic` | 최대 Shinigami HP +5 |
| `MenpoRelic` | 그 턴에 피격 없으면 Block 최대 10 유지 |
| `ShimenawaRelic` | 턴 종료 시 고유 Next Turn 파워 3종 보유 시 전체 10 피해 |
| `IncenseRelic` | 전투당 첫 소멸 시 Block 8 |
| `KoteRelic` | 한 턴에 공격 3회마다 HP 1 회복 |
| `OmikiRelic` | Refill 카드는 Refill 카드로만 Shift |
| `HachimakiRelic` | Misogi 선택이 휴식 선택을 소비하지 않음 |
| (+ `SEA_GLASS` 캐릭터별 변형명 "Pilgrim Glass") | 기존 유물 이름 오버라이드 |

`SEA_GLASS.WANDERER-WANDERER.title` 키 — **기존 게임 유물의 이름을 캐릭터별로 바꾸는** 로컬라이즈 규약이 존재합니다.

### 8.3 포션 (3)
`Seishu`(Next Turn 파워 즉시 발동) · `Doburoku`(손패 3장까지 Shift) · `Kasutori`("Die.")

---

## 9. 자체 모드(만신전/무협/궁수) 적용 체크리스트

### 9.1 "스탠스 비슷한 것"을 만들 때 그대로 따라갈 순서

1. `enum MyStance { ... }` 정의 (값은 0부터 연속)
2. `interface IMyStancePower { MyStance Stance { get; } }`
3. 스탠스 개수만큼 `MyPower, IMyStancePower` 구현. 전부 `Type=1`, `StackType=1`
4. `static class MyCmd`에 **단일 진입점** `EnterStance(ctx, creature, stance, amount)` 작성 — §4.3 골격 그대로
5. 현재 스탠스 조회는 반드시 `creature.Powers.OfType<IMyStancePower>().FirstOrDefault()`
6. 효과는 `AfterPowerAmountChanged` + `AfterPlayerTurnStart` **두 훅에 동일 로직**
7. 훅 안에서 플레이어 선택이 필요하면 **반드시 `RunAsHookAction`** 경유
8. 랜덤은 반드시 `player.RunState.Rng.*` 스트림
9. 스탠스 선택 UI는 토큰 카드(`Pool => TokenCardPool`) + `ChooseCard` 패턴
10. 수치는 하드코딩 대신 `CanonicalVars` + `PowerVar<T>` / `CardsVar` 로 선언 → 로컬라이즈 자동 연동
11. 시각 효과는 씬에 `Sprite`(+필요 시 `Corpse`) `Sprite2D` 노드를 두고 텍스처 스왑

### 9.2 만신전(그리스 신화) 사제에 대응시킨다면

| Wanderer | 만신전 대응 예시 |
|---|---|
| Stance (5종, 2종 해금형) | 섬기는 신(神格) 전환 — 기본 3주신 + 조건부 해금 2신 |
| `Kamae` 시작 카드 | "기도/제의" 카드로 신격 선택 |
| `SteadyPower` (전환 봉인) | "서약" — 일정 턴 신격 고정 |
| `Fudōshin` (재진입) | "재봉헌" — 같은 신격 재진입으로 축복 재발동 |
| `Flourish` (이탈 횟수) | "변덕" — 신을 바꾼 횟수만큼 강해지는 카드 |
| `Shift/Ofuda` | 신탁/저주 카드 변환 |
| `BrokenJuzu` 사망 부활 | 하데스 계약 / 저승 귀환 |
| `Dishonor` 저주 축적 | 신성모독(Hubris) 누적 |

### 9.3 반드시 피해야 할 것 (이 모드의 약점)

- ❌ `static Dictionary<Creature, T>`에 **세이브가 필요한 상태**를 넣지 말 것 → 유물/파워 모델 프로퍼티로 (Wanderer가 Shinigami HP에는 이 방식을 쓰고, 나머지에는 안 쓴 게 비일관적)
- ❌ 전환 차단 시 **무음 `return`** → 플레이어 피드백 추가 필요
- ❌ `GetListeners`를 매 이벤트마다 6개 컬렉션 `OfType` 재순회 → 전투당 캐시 권장
- ❌ Steady 검사 위치를 함부로 옮기지 말 것 → 재진입 계열 카드가 전부 잠김
- ❌ 토큰 카드에 `Pool => TokenCardPool` 빠뜨리면 보상 목록에 스탠스 토큰이 등장

---

## 10. 동봉 파일

| 파일 | 설명 |
|---|---|
| `il_dump.txt` | **전체 IL 디스어셈블** (459 타입, 토큰/제네릭/문자열 전부 해석됨). 특정 메서드의 정확한 동작을 확인할 때 |
| `brief_all.txt` | 압축 의사코드. async 배관·스택 조작 제거하고 `call`/`ldstr`/필드접근만 남김. **읽기용은 이쪽** |
| `pck_index.json` | PCK 750 엔트리 인덱스 `[path, offset, size, flags]` |
| `asset_manifest.txt` | 이미지 339장의 원본 경로 목록 (에셋 네이밍 규약 확인용) |
| `localization/` | 5개 언어 × 11개 파일 전량 (카드/파워/유물/포션 텍스트 원문) |
| `scenes/` | 컴파일된 `.scn` 6개 + `.remap` |
| `manifest/` | `Wanderer.json`, `readme.txt`, `project.binary`, `global_script_class_cache.cfg` |

**Claude Code에 던질 때 권장 순서**
1. 이 보고서 (`Wanderer_Mod_Analysis_KR.md`) — 설계 이해
2. `brief_all.txt` — 구현 확인
3. `il_dump.txt` — 세부 분기/수치 확인 (필요할 때만, 1.9MB라 통째로 넣지 말 것)

---

*분석 기준: DLL은 `MegaCrit.Sts2.Core` / `BaseLib` / `HarmonyLib` / `Godot` 4개 어셈블리를 참조. 게임 빌드 `v0.107.1-59260271` 대상.*
*C# 코드 블록은 IL에서 역구성한 것으로, 변수명·일부 인자 순서는 실제 원본과 다를 수 있습니다. 제어 흐름과 호출 대상은 IL 기준 정확합니다.*
