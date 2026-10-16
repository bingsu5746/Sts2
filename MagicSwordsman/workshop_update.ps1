# 엔시페르 창작마당 업데이트: 빌드 -> content 복사 -> changeNote -> 업로드
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $here

# 1) 업로더 폴더 찾기
$uploader = Join-Path $env:USERPROFILE 'Downloads\ModUploader-win-x64'
while (-not (Test-Path (Join-Path $uploader 'ModUploader.exe'))) {
    Write-Host "ModUploader.exe를 찾지 못했어요: $uploader" -ForegroundColor Yellow
    $uploader = Read-Host 'ModUploader.exe가 들어 있는 폴더 경로를 붙여넣고 엔터'
    $uploader = $uploader.Trim('"')
}
$ws = Join-Path $uploader 'NewModWorkspace'
if (-not (Test-Path (Join-Path $ws 'workshop.json'))) { throw "작업 폴더가 없어요: $ws" }
if (-not (Test-Path (Join-Path $ws 'mod_id.txt'))) {
    Write-Host 'mod_id.txt가 없어요. 이대로 올리면 창작마당에 새 항목이 생길 수 있어요.' -ForegroundColor Yellow
    if ((Read-Host '그래도 계속할까요? (y/n)') -ne 'y') { exit 1 }
}

# 2) 빌드
Write-Host '[1/4] 빌드 중 (dotnet publish)...' -ForegroundColor Cyan
dotnet publish MagicSwordsman.csproj
if ($LASTEXITCODE -ne 0) { throw '빌드 실패. 위의 오류 메시지를 캡처해 주세요.' }

# 3) 빌드 결과를 content에 복사
$modsPath = (dotnet msbuild MagicSwordsman.csproj -getProperty:ModsPath).Trim()
$modDir = Join-Path $modsPath 'MagicSwordsman'
Write-Host "[2/4] 복사: $modDir -> $ws\content" -ForegroundColor Cyan
$content = Join-Path $ws 'content'
New-Item -ItemType Directory -Force $content | Out-Null
foreach ($f in 'MagicSwordsman.dll', 'MagicSwordsman.pck', 'MagicSwordsman.json') {
    $src = Join-Path $modDir $f
    if (-not (Test-Path $src)) { throw "빌드 결과 파일이 없어요: $src" }
    Copy-Item $src $content -Force
}
$pck = Get-Item (Join-Path $content 'MagicSwordsman.pck')
if ($pck.Length -lt 50MB) {
    throw "MagicSwordsman.pck가 너무 작아요 ($([math]::Round($pck.Length/1MB,1)) MB). 그림·텍스트가 빠진 빌드라 업로드하지 않았어요. 빌드 창 마지막 부분을 캡처해 주세요."
}

# 4) changeNote
$version = (Get-Content (Join-Path $here 'MagicSwordsman.json') -Raw -Encoding UTF8 | ConvertFrom-Json).version
$note = Read-Host "[3/4] 이번 업데이트 내용 한 줄 (그냥 엔터 = '$version 업데이트')"
if ([string]::IsNullOrWhiteSpace($note)) { $note = "$version 업데이트" }
$esc = $note.Replace('\', '\\').Replace('"', '\"')
$wj = Join-Path $ws 'workshop.json'
$json = [IO.File]::ReadAllText($wj)
$json = [regex]::Replace($json, '"changeNote"\s*:\s*(null|"(?:[^"\\]|\\.)*")', { param($m) '"changeNote": "' + $esc + '"' })
[IO.File]::WriteAllText($wj, $json, (New-Object Text.UTF8Encoding $false))

# 5) 업로드
Write-Host '[4/4] 창작마당에 업로드 중...' -ForegroundColor Cyan
Push-Location $uploader
& .\ModUploader.exe upload -w NewModWorkspace
$code = $LASTEXITCODE
Pop-Location
if ($code -ne 0) { throw "업로드 실패. $uploader\mod-uploader.log 내용을 보내주세요." }
Write-Host '업로드 완료!' -ForegroundColor Green

# 6) 창작마당 버전으로 플레이하려면 직접 설치본을 치운다
if ((Read-Host '게임 mods 폴더의 직접 설치본을 바탕화면으로 옮길까요? 창작마당 버전으로 플레이하려면 y (y/n)') -eq 'y') {
    $backup = Join-Path ([Environment]::GetFolderPath('Desktop')) 'MagicSwordsman_local_backup'
    if (Test-Path $backup) { Remove-Item $backup -Recurse -Force }
    Move-Item $modDir $backup
    Write-Host "옮겼어요: $backup" -ForegroundColor Green
}
