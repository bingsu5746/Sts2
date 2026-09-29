# Watcher 모드 (STS2) — 스탠스 구현 패턴 노트

> 출처: [lamali292/WatcherMod](https://github.com/lamali292/WatcherMod) (공개 소스), [lamali292/BaseLib-StS2](https://github.com/lamali292/BaseLib-StS2)
> 이 문서는 원본 코드를 그대로 옮긴 것이 아니라, 우리 모드 설계에 쓸 **패턴만 요약**한 것이다.
> WatcherMod README는 "이 프로젝트는 템플릿이 아니며 그대로 복사해서 쓰지 말 것"을 명시하고 있음 — 그래서 코드를 그대로 가져오지 않고 아래처럼 구조만 참고한다.

## 핵심 패턴: 스탠스 = 파워 하나

Watcher는 "현재 스탠스"를 위한 별도 변수/상태 저장소를 두지 않는다. 그냥 `WatcherStanceModel`을 상속한 파워 클래스(`WrathStance`, `CalmStance`, `DivinityStance`, `NoStance`) 4개가 있고, 조회는:

```
creature.Powers.OfType<WatcherStanceModel>().FirstOrDefault()  // 개념적 예시
```

전환은 `WatcherModel.SetStance<T>()` 한 함수가 전담:
1. 기존 스탠스의 `OnExitStance()` 호출
2. 새 스탠스의 `OnEnterStance()` 호출
3. 필요하면 VFX/오디오 갱신

카드는 "본연의 효과 실행 → `StanceCmd.EnterXXX()` 호출" 두 줄이면 끝난다 (예: Eruption 카드는 데미지 9 주고 분노 스탠스 진입).

## 자동 소멸형 스탠스 (Divinity)

Divinity 스탠스는 진입 시 즉발 효과(에너지 +3)를 주고, `BeforeSideTurnStart` 훅에서 자동으로 `ExitStance()`를 호출해 1턴짜리로 소멸한다. 이게 바로 우리가 찾던 **"카드 한 번 쓰면 다음 턴 전에 자동으로 꺼지는 임시 상태"** 패턴의 실례다.

## 우리 설계에 적용할 부분

무당파의 "보법 → 다음 카드 강화" 미니 스탠스는 Watcher의 4개짜리 상호배타 시스템 전체를 가져올 필요 없이, 이 골격만 축소해서 쓰면 된다:

- 상태 1개짜리 파워 클래스 (예: `대기Power`)
- 보법 카드 재생 시 진입, 다음 카드 재생 직후(또는 `AfterCardPlayed` 훅) 자동 소멸
- Watcher의 Divinity처럼 "진입 시 즉발 + 특정 시점에 자동 퇴장" 조합이면 충분

## Wanderer 분석([wanderer-mod-analysis.md](./wanderer-mod-analysis.md))과 함께 보는 이유

Wanderer는 5-스탠스 버전의 같은 아이디어를 `IStancePower` 마커 인터페이스로 일반화한 사례다. 마커 인터페이스를 쓰면 "스탠스 파워인지 아닌지"를 타입으로 판별할 수 있어, 나중에 스탠스 수를 늘리고 싶어져도(예: 무당 보법 계열을 여러 개로 확장) 구조를 갈아엎지 않고 확장 가능하다. 지금 당장은 스탠스가 1종류뿐이라 인터페이스까지는 불필요하지만, 두 문파(무당 외 다른 문파도 비슷한 걸 쓸 경우) 이상으로 늘어나면 이 패턴으로 전환을 고려한다.
