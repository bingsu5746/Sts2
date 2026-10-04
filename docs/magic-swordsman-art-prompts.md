# 마검사 — 그림 생성 가이드 (AI 도구 · 프롬프트)

> 작성 2026-10-04. 도구 정보는 웹 검색 결과 기준이며, 가격·조건은 바뀔 수 있으니 결제 전 각 사이트에서 직접 확인할 것.
> 검 외형 묘사 중 **[원전]** 표시는 `magic-sword-research.docx`에 있는 내용, 나머지는 그림을 위한 창작이다.

## 1. 도구 추천
| 용도 | 추천 | 이유 |
|---|---|---|
| 카드 그림 대량 (140장) | **Leonardo.ai** 또는 **Scenario** | 스타일 레퍼런스/커스텀 모델로 그림체 통일, 투명 PNG 지원, 무료 사용량 있음 |
| 대표 그림·캐릭터 컨셉 | **Midjourney** | 일러스트 품질 최상, Style Reference(스타일 코드 재사용). 무료 없음 |
| 떠 있는 검·만검총 (투명 배경) | **Ideogram** 또는 Leonardo의 Transparency | 배경 없는 PNG를 바로 뽑기 쉬움 |

순서: ① 대표 그림 1장으로 그림체를 정한다 → ② 그 그림을 **스타일 레퍼런스**로 고정 → ③ 나머지를 같은 레퍼런스로 뽑는다. 그림체가 섞이는 게 가장 흔한 실패다.

## 2. 공통 스타일 문구 (모든 프롬프트 앞에 붙임)
```
hand-painted 2D game illustration, Slay the Spire style card art, bold readable silhouette, painterly brush strokes, dramatic rim lighting, dark fantasy, muted background with one strong accent color, no text, no letters, no watermark, no frame
```
- 원작 그림을 그대로 베끼지 않게, "Slay the Spire style"은 첫 시도에만 쓰고 그림체가 정해지면 빼고 스타일 레퍼런스만 쓰는 것을 권장.
- 네거티브(지원하는 도구만): `text, watermark, signature, frame, border, blurry, extra fingers, photo, 3d render`

## 3. 자산별 프롬프트 틀
| 자산 | 크기/비율 | 프롬프트 틀 |
|---|---|---|
| 카드 그림 | 1000×760 (약 4:3, 생성 후 축소; 작은 것 250×190) | `[공통], [장면 설명], centered subject, simple background` |
| 캐릭터 몸 | 260×400 기준, 세로 | `[공통], full body, a swordsman in a dark hooded cloak with several swords floating behind, standing facing right, side view, transparent background` |
| 떠 있는 검 | 세로, 칼날이 위로 | `single sword, vertical, blade pointing up, isolated, transparent background, [검 묘사]` |
| 만검총 | 정사각 | `ancient stone sword tomb with a small dark door, swords stuck in the ground around it, isolated, transparent background` |
| 유물 아이콘 | 256×256 (작은 것 94×94) | `game relic icon, single object, centered, thick outline, transparent background, [물건]` |
| 파워 아이콘 | 64×64 | `simple flat game buff icon, single symbol, high contrast, transparent background, [상징]` |

## 4. 검별 묘사 (검 그림 + 그 검 카드 그림에 공통으로 넣기)
| 검 | 묘사 문구 | 근거 |
|---|---|---|
| 그람 | `a broken norse longsword reforged from two pieces, glowing seam along the blade, oak tree Branstock in background` | [원전] 오딘이 브란스톡 나무에 꽂음, 부러진 뒤 두 조각을 다시 벼림 |
| 간장·막야 | `a pair of ancient chinese twin swords, one dark (male) and one pale (female), forged in a roaring furnace` | [원전] 암수 한 쌍, 용광로에서 3년 만에 완성 / 색 구분은 창작 |
| 쿠사나기 | `an ancient japanese straight sword emerging from the tail of an eight-headed serpent, storm clouds` | [원전] 야마타노오로치 꼬리에서 발견, "하늘의 구름을 모으는 검". 실물 비공개라 형태는 창작 |
| 티르빙 | `a norse sword with a golden hilt and a blade glowing like flame, cursed aura` | [원전] 황금 자루, 불꽃처럼 빛나는 칼날 |
| 다인슬레이프 | `a dark norse sword dripping red, endless battlefield of fallen warriors` | [원전] 끝나지 않는 전투, 낫지 않는 상처 / 색은 창작 |
| 뒤랑달 | `a holy french knight sword with a golden hilt containing relics, unbreakable, struck against a cliff` | [원전] 황금 자루 속 성유물, 바위를 열 번 내리쳐도 흠집 없음 |
| 스코프눙 | `a pale blue norse king's sword surrounded by twelve ghostly berserker spirits` | [원전] 광전사 12명의 혼 |
| 오니마루 | `a curved japanese tachi floating by itself, cutting a demon (oni)` | [원전] 다치, 스스로 움직여 오니를 벰 |
| 클라이브 솔라시 | `a sword of pure white light being drawn from a dark sheath, blinding radiance` | [원전] 빛의 검, 죽음의 칼집에서 뽑히면 누구도 벗어나지 못함 |
| 칼라드볼그 | `a celtic sword growing as large as a rainbow, cutting the tops off three hills` | [원전] 무지개만큼 커짐, 언덕 세 개의 꼭대기를 벰 |

카드 그림은 `[공통], [검 묘사], [카드 장면]` 순서. 카드 장면은 `docs/magic-swordsman-content.md`의 카드 이름·「」 문구를 영어로 한 줄 요약해 넣는다.
예) 부서진 칼날: `[공통], a broken norse longsword reforged from two pieces..., a hooded swordsman slashing with the cracked blade, sparks flying`

## 5. 파일 넣기
- 이름 규칙과 크기는 `MagicSwordsman/README.md`의 "그림·연출 넣는 법" 참고.
- 생성한 그림은 PNG로 저장해 해당 폴더에 넣고 `dotnet publish`로 `.pck`를 다시 만든다.

## 6. 공개 배포 시 주의
- 상업적 이용 가능 여부는 도구·요금제마다 다르다 (예: Recraft 무료 요금제는 상업 권리 제한). 결제 전 약관 확인.
- Steam은 2026-01 AI 공개 지침을 개정해 플레이어가 보는 생성형 AI 콘텐츠(그림 등)를 공개 대상으로 본다. 이 지침은 스토어 게임 기준이라 창작마당 모드에도 같은 요구가 있는지는 확인하지 못했다. 모드 설명에 AI 사용 사실을 적어 두는 것을 권장.
