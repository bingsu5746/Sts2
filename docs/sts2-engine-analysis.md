# STS2 엔진 분석 — 레인저 구현용 레퍼런스

> 대상: Slay the Spire 2 **v0.107.1** (commit `59260271`, 2026-06-18 빌드), `sts2.dll` + `sts2.xml`
> 방법: `dnfile` + `dncil`로 IL 디스어셈블, 메타데이터 시그니처 디코딩 (직접 작성한 스크립트)
> 원본 게임 파일은 저장소에 넣지 않음. 전체 시그니처 덤프: [sts2-api-reference.txt](./sts2-api-reference.txt)
> **주의:** 아래 C# 코드는 IL을 보고 사람이 재구성한 것. 메서드명·인자 순서·타입은 메타데이터에서 확인한 값이지만, 비동기 상태머신 내부 흐름은 요약이다. 버전이 바뀌면 다시 검증할 것.

---

## 1. 검증된 기본 수치

| 항목 | 값 | 출처 |
|---|---|---|
| 턴당 최대 에너지 | 3 | `CharacterModel.get_MaxEnergy` → `ldc.i4.3` |
| 턴 시작 드로우 | 5 | `CombatManager.SetupPlayerTurn` → `Hook.ModifyHandDraw(..., 5m, ...)` |
| 손패 최대 | 10 | `CardPile.get_MaxCardsInHand` → `ldc.i4.s 10` |
| 시작 체력 | 아이언클래드 80 / 사일런트 70 | `get_StartingHp` |
| 시작 덱 | 아이언클래드 10장 / 사일런트 12장 | `get_StartingDeck` 배열 길이 |
| 타격(아이언클래드) | 1코, 피해 6, 강화 +3 | `StrikeIronclad` |
| 수비(아이언클래드) | 1코, 방어 5, 강화 +3 | `DefendIronclad` |
| 카드 풀 크기 | 아이언클래드 87 / 사일런트 88 (디펙트·리젠트·네크로바인더도 88) | `GenerateAllCards` 배열 길이 |

**카드 풀 분포 (희귀도)** — 아이언클래드: Basic 3 / Common 20 / Uncommon 36 / Rare 26 / Ancient 2. 사일런트: Basic 4 / Common 20 / Uncommon 36 / Rare 26 / Ancient 2.
**타입 분포 (아이언클래드 87장)** — Attack 37 / Skill 30 / Power 20. **코스트** — 0코 12 / 1코 48 / 2코 19 / 3코 7 / X 1.

---

## 2. 핵심 열거형 (정수값 = 선언 순서)

| enum | 값 |
|---|---|
| `CardType` | None=0, Attack=1, Skill=2, Power=3, Status=4, Curse=5, Quest=6 |
| `CardRarity` | None=0, Basic=1, Common=2, Uncommon=3, Rare=4, Ancient=5, Event=6, Token=7, Status=8, Curse=9, Quest=10 |
| `TargetType` | None=0, Self=1, AnyEnemy=2, AllEnemies=3, RandomEnemy=4, AnyPlayer=5, AnyAlly=6, AllAllies=7, TargetedNoCreature=8, Osty=9 |
| `CardKeyword` | None, Exhaust, Ethereal, Innate, Unplayable, **Retain(=5)**, Sly, Eternal |
| `CardTag` | None, Strike, Defend, Minion, OstyAttack, Shiv |
| `PileType` | None, Draw, Hand, Discard, Exhaust, Play, Deck |
| `ValueProp` | Unblockable, Unpowered, Move, SkipHurtAnim (플래그) |
| `PowerType` | None, Buff, Debuff |
| `RelicRarity` | None, Starter, Common, Uncommon, Rare, Shop, Event, Ancient |
| `PotionRarity` | None, Common, Uncommon, Rare, Event, Token |
| `PotionUsage` | None, CombatOnly, AnyTime, Automatic |

> `CardTag`는 확장 불가 enum — 기존 메모(5-14 TheQueen)처럼 미사용 정수 캐스팅이 필요할 수 있음. 레인저는 태그 대신 **마커 인터페이스**(예: `IArrowCard`) 사용을 권장.

---

## 3. 카드 (`CardModel`)

### 3.1 생성자
```csharp
protected CardModel(int canonicalEnergyCost, CardType type, CardRarity rarity,
                    TargetType targetType, bool shouldShowInCardLibrary)
```
예: `StrikeIronclad : base(1, Attack, Basic, AnyEnemy, true)` / `DefendIronclad : base(1, Skill, Basic, Self, true)` / `Volley : base(0, Attack, Uncommon, RandomEnemy, true)` + `HasEnergyCostX => true`.

### 3.2 오버라이드 지점 (실제 바닐라 카드가 쓰는 것)
| 멤버 | 용도 |
|---|---|
| `protected virtual IEnumerable<DynamicVar> CanonicalVars` | 수치 선언 (`DamageVar`, `BlockVar`, `PowerVar<T>`, `IntVar`, `RepeatVar`, `CardsVar`, `EnergyVar` 등) |
| `protected virtual Task OnPlay(PlayerChoiceContext ctx, CardPlay cardPlay)` | 사용 효과 (async) |
| `protected virtual void OnUpgrade()` | 강화. `DynamicVars.Damage.UpgradeValueBy(3m)` 식 |
| `protected virtual HashSet<CardTag> CanonicalTags` | 태그 |
| `public virtual IEnumerable<CardKeyword> CanonicalKeywords` | 기본 키워드 |
| `protected virtual IEnumerable<IHoverTip> ExtraHoverTips` | 툴팁 (`HoverTipFactory.FromPower<T>()`, `FromKeyword(CardKeyword)`) |
| `public virtual bool GainsBlock` | 방어 카드 표시 |
| `protected virtual bool HasEnergyCostX` | X 코스트 |
| `protected virtual bool ShouldGlowGoldInternal / ShouldGlowRedInternal` | 카드 테두리 강조 (예: 화살 0일 때 근접 강조) |
| `protected virtual bool IsPlayable` | 사용 가능 조건 |
| `protected virtual void AddExtraArgsToDescription(LocString)` | 설명문 동적 인자 |
| `Task OnTurnEndInHand(...)` | 턴 종료 시 손에 있을 때 (화상·저주류가 사용) |

### 3.3 런타임 API (카드 인스턴스)
- 키워드: `AddKeyword(CardKeyword)`, `RemoveKeyword(CardKeyword)`, `Keywords`(읽기)
- **1턴 보존: `GiveSingleTurnRetain()`, `HasSingleTurnRetain`, `ShouldRetainThisTurn`** — 3.4 참조
- 비용: `EnergyCost` (`CardEnergyCost`: `AddThisTurn/AddThisCombat/AddUntilPlayed/SetThisTurn/SetThisCombat/SetUntilPlayed/...`), `SetToFreeThisTurn()`, `SetToFreeThisCombat()`
- 상태: `Owner`(Player), `Pile`, `CombatState`, `IsUpgraded`, `DynamicVars`, `ExhaustOnNextPlay`
- X값: `ResolveEnergyXValue()`

### 3.4 ⭐ 1회 보존 — [시위] 구현 근거
`CardModel.ShouldRetainThisTurn` (IL 재구성):
```csharp
public bool ShouldRetainThisTurn => Keywords.Contains(CardKeyword.Retain) || HasSingleTurnRetain;
```
`CardModel.EndOfTurnCleanup()` 첫 줄들: `ExhaustOnNextPlay = false; HasSingleTurnRetain = false; HasSingleTurnSly = false; EnergyCost.EndOfTurnCleanup(); ...`
→ **게임 자체에 "이번 턴만 보존" 플래그가 있고, 턴 정리 때 자동으로 꺼진다.** 바닐라 사용처: `WellLaidPlansPower`(사일런트 "치밀한 계획")가 `BeforeFlushLate` 훅에서 손패 카드 선택 후 `card.GiveSingleTurnRetain()` 호출.

**레인저 적용안:** [시위] 카드가 `BeforeFlushLate`(손패 버리기 직전) 훅에서 자신이 손에 있고 아직 당김 상태가 아니면 `GiveSingleTurnRetain()` + 당김 플래그 ON. 다음 턴에는 플래그가 이미 ON이므로 다시 보존하지 않음 → 자연스럽게 버려짐. 당김 플래그는 카드 필드로 두고 `AfterCardPlayed`/버려질 때 리셋.
> 미검증: 카드 자체가 `BeforeFlushLate` 훅을 받는지(`ShouldReceiveCombatHooks`의 카드 쪽 조건). 안 되면 레인저 전용 파워(예: 화살통 파워)가 손패를 훑어서 처리.

### 3.5 공격 빌더 (`DamageCmd.Attack` → `AttackCommand`)
```csharp
// StrikeIronclad.OnPlay 재구성
ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
    .FromCard(this)
    .Targeting(cardPlay.Target)
    .WithHitFx("vfx/vfx_attack_slash", null, null)
    .Execute(choiceContext);

// Volley(X코 무작위 다단히트) 재구성 — 레인저 "연사/산탄" 참고
await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
    .WithHitCount(ResolveEnergyXValue())
    .FromCard(this)
    .TargetingRandomOpponents(CombatState, true)   // allowDuplicates
    .WithHitFx("vfx/vfx_attack_slash", null, null)
    .Execute(choiceContext);
```
빌더 메서드 전체: `FromCard, FromOsty, FromMonster, Targeting(Creature), TargetingAllOpponents(ICombatState), TargetingRandomOpponents(ICombatState, bool), Unpowered(), WithHitCount(int), WithAttackerAnim, WithNoAttackerAnim, AfterAttackerAnim, WithAttackerFx, WithWaitBeforeHit, WithHitFx(vfx,sfx,tmpSfx), SpawningHitVfxOnEachCreature, WithHitVfxSpawnedAtBase, WithHitVfxNode, OnlyPlayAnimOnce, BeforeDamage(Func<Task>), Execute(PlayerChoiceContext)`.
결과: `AttackCommand.Results` (`IEnumerable<List<DamageResult>>`) — 처치 판정(회수 베기)에 사용.

사용 예 빈도: `WithHitCount` 37장, `TargetingAllOpponents` 28장, `TargetingRandomOpponents` 7장(FlakCannon, Ricochet, RipAndTear, Stardust, SweepingGaze, SwordBoomerang, Volley).

### 3.6 방어
```csharp
// DefendIronclad.OnPlay 재구성
await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay, false);
```
시그니처: `CreatureCmd.GainBlock(Creature, BlockVar, CardPlay, bool fast)` / `GainBlock(Creature, decimal, ValueProp, CardPlay, bool fast)`.

### 3.7 디버프/버프 부여
```csharp
// Footwork(민첩 파워 카드) 재구성
await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature,
        DynamicVars.Dexterity.BaseValue, Owner.Creature, this, false);
// CanonicalVars: new PowerVar<DexterityPower>(2m)  /  OnUpgrade: DynamicVars.Dexterity.UpgradeValueBy(1m)
```
시그니처: `PowerCmd.Apply<T>(PlayerChoiceContext, Creature target, decimal amount, Creature applier, CardModel cardSource, bool silent)` (다중 대상 오버로드 있음). 그 외 `ModifyAmount, Decrement, Remove, TickDownDuration`.

### 3.8 키워드 부여 (손패 선택)
```csharp
// Snap 일부 재구성: 손에서 1장 골라 보존 키워드 부여
var card = (await CardSelectCmd.FromHand(ctx, Owner, prefs, filter, this)).FirstOrDefault();
CardCmd.ApplyKeyword(card, CardKeyword.Retain);   // CardCmd.ApplyKeyword(card, params CardKeyword[])
```

---

## 4. 파워 (`PowerModel`)
필수 오버라이드: `PowerType Type`, `PowerStackType StackType`. 수치는 `Amount`. 모든 `AbstractModel` 훅(§6)을 오버라이드 가능.
```csharp
// WellLaidPlansPower 재구성 (1회 보존 부여 파워)
public override PowerType Type => PowerType.Buff;
public override PowerStackType StackType => (PowerStackType)1;
public override async Task BeforeFlushLate(PlayerChoiceContext ctx, Player player) {
    if (player != Owner.Player) return;
    // Amount장까지 손패에서 선택 (이미 보존되는 카드 제외)
    var picked = await CardSelectCmd.FromHand(ctx, Owner.Player, new CardSelectorPrefs(SelectionScreenPrompt, 0, Amount), RetainFilter, this);
    foreach (var c in picked.ToList()) c.GiveSingleTurnRetain();
}
private bool RetainFilter(CardModel c) => !c.ShouldRetainThisTurn;
```
바닐라에 **화상(Burn) 파워는 없음** — 불 화살의 화상은 커스텀 파워로 구현 (턴 시작 피해 후 1 감소: `PoisonPower` 구조 참고). 약화는 `WeakPower`, 취약 `VulnerablePower`, 무형 `IntangiblePower`, 다음 턴 방어 `BlockNextTurnPower`, 다음 턴 드로우 `DrawCardsNextTurnPower`, 다음 턴 에너지 `EnergyNextTurnPower`가 있음.

---

## 5. 유물·포션·캐릭터·풀

### 5.1 유물 (`RelicModel`) — 필수: `RelicRarity Rarity`
```csharp
// BurningBlood 재구성
public override RelicRarity Rarity => RelicRarity.Starter;
protected override IEnumerable<DynamicVar> CanonicalVars => [new HealVar(6m)];
public override async Task AfterCombatVictory(CombatRoom _) {
    if (Owner.Creature.IsDead) return;
    Flash();
    await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue, true);
}
```
→ **엘븐 롱보우**: `BeforeCombatStart`(또는 `AfterPlayerTurnStart` 첫 턴)에서 화살통 파워를 5로 세팅.

### 5.2 포션 (`PotionModel`) — 필수: `Rarity`, `Usage`, `TargetType`, `OnUse(PlayerChoiceContext, Creature target)`
```csharp
// WeakPotion 재구성
Rarity => PotionRarity.Common; Usage => PotionUsage.CombatOnly; TargetType => TargetType.AnyEnemy;
CanonicalVars => [new PowerVar<WeakPower>(3m)];
OnUse: AssertValidForTargetedPotion(target); (스플래시 VFX);
       await PowerCmd.Apply<WeakPower>(ctx, target, DynamicVars.Weak.BaseValue, Owner.Creature, null, false);
```

### 5.3 캐릭터 (`CharacterModel`) — 추상 멤버 (반드시 구현)
`NameColor, Gender, UnlocksAfterRunAs, StartingHp, StartingGold, CardPool, RelicPool, PotionPool, StartingDeck, StartingRelics, AttackAnimDelay, CastAnimDelay, GetArchitectAttackVfx()` + 다수의 에셋 경로 getter. `MaxEnergy`는 virtual(기본 3).

### 5.4 풀 — 추상 멤버
- `CardPoolModel`: `Title, EnergyColorName, CardFrameMaterialPath, DeckEntryCardColor, IsColorless, GenerateAllCards()`
- `RelicPoolModel`: `EnergyColorName, GenerateAllRelics()`
- `PotionPoolModel`: `EnergyColorName, GenerateAllPotions()`
- 바닐라 풀은 `ModelDb.Card<T>()`를 배열로 나열. 모드에서는 공식 `ModHelper.AddModelToPool(Type poolType, Type modelType)` / `ConcatModelsFromMods`로 주입 가능.

---

## 6. 훅 (모든 `AbstractModel` = 카드·파워·유물·포션 공통 오버라이드 지점)
레인저에 중요한 것만 (전체 목록은 api 덤프의 AbstractModel 절):
- 턴: `AfterPlayerTurnStart(ctx, player)`, `AfterPlayerTurnStartEarly/Late`, `BeforeSideTurnEnd(ctx, side, participants)`, `AfterSideTurnEnd`
- 손패 버리기(턴 종료): **`BeforeFlush` / `BeforeFlushLate(ctx, player)` / `AfterFlush(ctx, player, flushed, retained)`**
- 카드: `BeforeCardPlayed(CardPlay)`, `AfterCardPlayed(ctx, CardPlay)`, `AfterCardDrawn(ctx, card, fromHandDraw)`, `AfterCardDiscarded`, `AfterCardExhausted`
- 전투: `BeforeCombatStart()`, `BeforeCombatStartLate()`, `AfterCombatEnd(room)`, `AfterCombatVictory(room)`
- 피해: `BeforeAttack(AttackCommand)`, `AfterAttack(ctx, AttackCommand)`, `AfterDamageGiven(ctx, dealer, DamageResult, props, target, cardSource)`, `AfterDeath(ctx, creature, ...)`
- 수치 수정: `ModifyDamageAdditive / ModifyDamageMultiplicative(target, amount, props, dealer, cardSource)`, `ModifyBlockAdditive/Multiplicative`, `ModifyAttackHitCount`, `ModifyHandDraw`, `ModifyMaxEnergy`, `TryModifyEnergyCostInCombat`, `TryModifyKeywordsInCombat`
- 파워 변화: `AfterPowerAmountChanged(ctx, power, amount, applier, cardSource)` — 화살 수 변화 감지

---

## 7. 커맨드 요약 (정적 메서드)
| 클래스 | 주요 메서드 |
|---|---|
| `DamageCmd` | `Attack(decimal)`, `Attack(CalculatedDamageVar)` |
| `CreatureCmd` | `Damage(...)` 다수 오버로드, `GainBlock`, `LoseBlock`, `Heal`, `Kill`, `TriggerAnim` |
| `PowerCmd` | `Apply<T>`, `Apply(power,...)`, `ModifyAmount`, `Decrement`, `Remove` |
| `CardPileCmd` | `Draw(ctx, count, player, fromHandDraw)`, `Add(card, PileType, position, ...)`, `AddGeneratedCardToCombat(card, PileType, creator, position)`, `Shuffle`, `RemoveFromCombat` |
| `CardCmd` | `ApplyKeyword`, `RemoveKeyword`, `Discard`, `Exhaust`, `Upgrade`, `Transform`, `AutoPlay` |
| `CardSelectCmd` | `FromHand(ctx, player, prefs, filter, source)`, `FromCombatPile(ctx, pile, player, prefs[, filter])`, `FromChooseACardScreen(ctx, cards, player, canSkip)` |
| `PlayerCmd` | `GainEnergy/LoseEnergy/SetEnergy(amount, player)`, `GainStars/LoseStars/SetStars`, `GainGold` |

---

## 8. 모딩 시스템 (게임 내장 `MegaCrit.Sts2.Core.Modding`)
- `ModManifest` 필드: `id, name, author, description, version, hasPck, hasDll, dependencies, affectsGameplay, minGameVersion`
- `[ModInitializer("메서드명")]` 어트리뷰트로 진입점 지정
- `ModHelper.AddModelToPool(poolType, modelType)`, `ModHelper.SubscribeForCombatStateHooks(id, delegate)`, `SubscribeForRunStateHooks`
- 커뮤니티 BaseLib/RitsuLib(기존 메모 5-4, 5-11)은 이 위에 얹는 편의 계층

---

## 9. 레인저 메커니즘 → 엔진 매핑 (구현 설계안)

| 레인저 기능 | 구현 방식 | 근거 |
|---|---|---|
| 화살통(0~5발) | **`QuiverPower`**(Buff, 수치=화살 수, 0이어도 유지) | 별(Stars)은 리젠트 전용 내장 자원(`PlayerCombatState.Stars`, 카드 `StarCost`)이라 재사용 시 UI·비용 시스템이 엮임 → 파워 방식이 안전 |
| 엘븐 롱보우 | 유물 `BeforeCombatStart`에서 `QuiverPower` 5 적용 | §5.1 |
| 원거리/근접 이중 효과 | `OnPlay`에서 `QuiverPower.Amount >= 필요화살` 분기. 부족 시 근접 수치로 공격 | §3.5 |
| 근접 강조 표시 | `ShouldGlowGoldInternal => 화살 부족` | §3.2 |
| 회수 | `PowerCmd.ModifyAmount(QuiverPower, +n)` (최대치 클램프는 파워 쪽에서) | §3.7 |
| 장전(속성 인챈트) | `LoadedArrowPower`(종류 1개, 덮어쓰기). 사격 시 소비 | 장전 슬롯 1개 규칙 |
| 화상 | 커스텀 `BurnPower` (턴 시작 피해 → 1 감소) | 바닐라에 없음 §4 |
| 얼음 → 약화 | `WeakPower` | 바닐라 |
| 관통(방어 무시) | `ValueProp.Unblockable` 피해 | §2 |
| 전기(전이 50%) | 공격 후 `CreatureCmd.Damage(다른 무작위 적, 50%)` | §7 |
| 폭발(광역, 회수 불가) | `TargetingAllOpponents` + `QuiverMaxThisCombat` 감소 처리 | §3.5 |
| 산탄(3조각 무작위) | `WithHitCount(3).TargetingRandomOpponents(..., true)` | Volley 패턴 |
| [시위] 1회 보존 | `BeforeFlushLate`에서 `GiveSingleTurnRetain()` + 당김 플래그 | §3.4 |
| 당김 강화(+50%, 속성 2배) | 카드 `OnPlay`에서 플래그 확인 후 수치 계산 | — |

### 남은 검증 항목
1. 카드 인스턴스가 손패에 있을 때 `BeforeFlushLate` 훅을 직접 받는지 (`ShouldReceiveCombatHooks`)
2. 파워 Amount 0 유지 가능 여부 (`PowerModel.ShouldRemoveDueToAmount` 오버라이드로 해결 예상)
3. 로컬라이제이션 키 규칙 (`LocString` 테이블 이름/키 형식) — 모드 예제(기존 메모 5-14)와 대조
4. 에셋 경로(카드 초상화, 에너지 아이콘) — `CharacterModel`의 경로 getter 목록 참고
