# 스팀 창작마당 업로드 (엔시페르)

공식 업로더: https://github.com/megacrit/sts2-mod-uploader (Mega Crit, Windows용 `ModUploader.exe`)

1. Windows PC에서 이 저장소를 최신으로 받고 Rider/Visual Studio 또는 `dotnet publish`로 **Publish** 빌드
   (MegaDot 경로는 `MagicSwordsman/Directory.Build.props`의 `GodotPath`).
   결과: 게임 `mods/MagicSwordsman/` 폴더에 `MagicSwordsman.dll`, `MagicSwordsman.pck`, `MagicSwordsman.json`.
2. 그 상태로 게임에서 "Play with Mods"로 한 번 실행해 정상 작동 확인.
3. 업로더 릴리스를 받아 `ModUploader.exe`를 더블클릭 → `NewModWorkspace` 폴더 생성 → 이름 변경(예: `EnsiferWorkspace`).
4. 워크스페이스의 `content` 폴더에 3번이 아니라 1번 결과물 폴더(`MagicSwordsman/` 안의 dll·pck·json)를 넣는다
   (폴더 구조는 워크스페이스 안 README 안내를 따른다).
5. `workshop.json` 작성 (필드 설명은 워크스페이스 안 README). 미리보기는 이 폴더의 `image.png`(1MB 미만) 사용.
6. 업로더 폴더에서 명령 프롬프트: `ModUploader.exe upload -w EnsiferWorkspace`
7. 업데이트 때: `content` 파일 교체 → (선택) `workshop.json`의 `changeNote` → 같은 명령. 모드 ID는 `mod_id.txt`에 저장되므로 지우지 말 것.
8. 창작마당 페이지에서 BaseLib를 "필수 아이템"으로 연결(BaseLib가 창작마당에 있으면).
문제 시 `mod-uploader.log`를 확인.
