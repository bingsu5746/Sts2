# 마검사 — 게임 소스 확인 결과

> 출처: 사용자가 올린 게임 바이너리(`sts2.dll`, 파일 날짜 2026-08-20)를 ILSpy(ilspycmd 8.2)로 디컴파일해 직접 확인. 바이너리 자체는 저장소에 넣지 않음.
> 아래 경로는 디컴파일 결과의 네임스페이스/파일 기준.

## 1. Eternal은 복제를 막지 못한다
- `CardModel.IsRemovable => !Keywords.Contains(CardKeyword.Eternal)` — Eternal은 **제거 방지**만 담당.
- `Enchantments/Clone.cs`는 빈 클래스. `EnchantmentModel.CanEnchant`는 카드 타입(Status/Curse/Quest 제외), 덱의 Unplayable, 기존 인챈트만 검사하고 Eternal은 보지 않음.
  → 소환 카드에 Clone 인챈트가 붙을 수 있고, 휴식처 `CloneRestSiteOption`이 Clone 붙은 카드를 `RunState.CloneCard`로 복제함.
- `RunState.CloneCard`를 쓰는 다른 복제 경로: `DollysMirror`(필터는 Quest 타입만 제외), 이벤트 `Reflections`의 Shatter(덱 전체 복제), `SilverCrucible`, `Glitter`, 알 유물들 등 다수.
- 결론: Eternal만으로는 부족. 대응 후보
  - 소환 카드 타입을 `Quest`로 두기 → `CanEnchant`와 DollysMirror 필터에서 자동 제외 (단 Quest 타입의 다른 부작용은 미확인)
  - 또는 덱에 추가될 때 끼어드는 훅 `Hook.ModifyCardBeingAddedToDeck`로 두 번째 사본을 막기
  - 또는 Harmony로 `EnchantmentModel.CanEnchant`를 패치

## 2. Soul's Power 제외
- `Enchantments/SoulsPower.cs`: `CanEnchant`가 **카드 자체(Local)에 Exhaust 키워드가 있을 때만** true, 인챈트 시 Exhaust 제거.
- 소환 카드는 소멸(Exhaust)이 있으므로 대상이 됨. 위 1번의 `Quest` 타입이나 `CanEnchant` 패치로 함께 막을 수 있음.

## 3. 카드 보상·상점을 보유 마검 카드로 제한 — 가능
- `Hook.ModifyCardRewardCreationOptions` → 리스너의 `AbstractModel.ModifyCardRewardCreationOptions(player, options)` 호출. `CardCreationOptions.CardPoolFilter`(`Func<CardModel,bool>`)로 카드를 걸러낼 수 있음.
  - 같은 방식을 쓰는 기존 유물: `DingyRug`, `PrismaticGem`.
- 상점: `Hook.ModifyMerchantCardPool(runState, player, options)` → `AbstractModel.ModifyMerchantCardPool`.
- 리스너는 `RunState.IterateHookListeners`로 순회되며, 유물이 리스너로 동작함(위 두 유물이 증거) → **시작 유물**에 필터를 넣는 방식이 자연스러움.

## 4. 런 단위 저장 — 가능 (모드 타입 등록 필요)
- `[SavedProperty]` 속성으로 카드·유물의 값을 런 세이브에 저장. 게임 안에서 쓰이는 타입: int(32곳), bool, string, ModelId, ModelId?, SerializableCard, List<SerializableCard> 등.
- `SavedPropertiesTypeCache`는 게임 자체 모델 타입만 자동 등록함. 모드 클래스는 공개 메서드 `SavedPropertiesTypeCache.InjectTypeIntoCache(Type)`로 등록해야 할 것으로 보임 (실제 동작은 미검증).
- 보유 마검 목록은 소환 카드나 시작 유물에 ModelId / string / int 형태로 저장 가능.

## 5. 아직 확인하지 않은 것
- 검 획득 연출(임시)을 보상·이벤트 화면 위에 띄우는 지점
- 떠 있는 검의 시각 배치(오브 배치 방식)
- `Quest` 카드 타입을 소환 카드에 쓸 때의 부작용
