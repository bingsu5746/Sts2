# 청운문 (Cheongunmun) — STS2 캐릭터 모드

[Alchyr의 ModTemplate-StS2](https://github.com/Alchyr/ModTemplate-StS2) 캐릭터 템플릿 기반. 설계 문서는 저장소의 `docs/wuxia-character-spec.md` 참고.

## 준비물 (PC)
- **.NET SDK 9.0 이상**
- **MegaDot 4.5.1** (megadot.megacrit.com) — 게임은 4.5.1보다 새 버전으로 만든 `.pck`를 읽지 못함
- **BaseLib** — 게임에 설치돼 있어야 함 (Steam 창작마당 구독 또는 GitHub 릴리스)

게임 DLL은 빌드할 때 Steam 설치 경로에서 자동으로 찾는다. 저장소에 올리지 않는다.

## 처음 한 번 설정
1. `Directory.Build.props.example`을 같은 폴더에 `Directory.Build.props`로 복사한다 (이 파일은 git에 올라가지 않음).
2. 그 안의 `<GodotPath>`를 PC의 MegaDot 실행 파일 경로로 바꾼다.
3. 게임이 Steam 기본 위치에 없으면 `<Sts2Path>` 줄의 주석을 풀고 게임 폴더 경로를 넣는다.

## 빌드
이 폴더(`Cheongunmun/`)에서:

| 상황 | 명령 | 하는 일 |
|---|---|---|
| 처음, 또는 텍스트·이미지를 바꿨을 때 | `dotnet publish` | `.dll` + `.pck` + `.json`을 게임 `mods/Cheongunmun/`에 복사 |
| 코드만 바꿨을 때 | `dotnet build` | `.dll`만 다시 복사 |

카드 이름·설명은 `.pck`에 들어가므로 **처음엔 반드시 `publish`**를 해야 한다.

## 현재 들어있는 것
- 캐릭터: 청운문 제자, HP 70
- 자원: 내공 (전투마다 초기화, 단전이 시작 시 최대치까지 채움)
- 시작 유물: 단전 (내공 최대치 저장, 아이콘 숫자 = 최대치)
- 시작덱: 삼재검법×4, 철포삼×4, 운기조식×1, 육합검법×1

이미지는 전부 템플릿 기본 이미지이고, 내공 아이콘은 임시로 에너지 아이콘을 쓴다.
