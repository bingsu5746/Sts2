# 무명 그림 출처 (AI 생성 그림)

무명 모드의 배경과 캐릭터 그림은 **AI로 생성한 그림**입니다 (2026-11-13).

- 생성: AI Horde (aihorde.net, 자원봉사 GPU 네트워크, 익명 사용), 모델 `AlbedoBase XL (SDXL)`, 30 steps, k_dpmpp_2m (karras)
- 후처리: AI Horde의 `RealESRGAN_x2plus` (2배 확대), 캐릭터는 `strip_background`(배경 제거)
- 배경 제거가 실패한 그림은 rembg 프로젝트(github.com/danielgatis/rembg)가 배포하는 `isnet-general-use.onnx` 모델로 다시 지움
- 게임용 가공: `tools/import_art.py` (배경은 1920×1080으로 자르기, 영혼 그림은 푸른 유령 색조·아래로 흩어지는 투명도 적용)
- 프롬프트 원본: `Mumyeong/tools/art_jobs/backgrounds.json`, `characters.json`, 고른 시드: `picks.json`
- 생성 이미지의 라이선스: AI Horde 이용 약관에서 생성물 라이선스를 명시한 문서를 찾지 못했습니다(카드 그림 출처 문서와 같은 상태). 공개 배포 전에 확인 필요.
- Steam에 공개할 경우 AI 생성 콘텐츠 사용 사실을 밝히는 것을 권장합니다.
- 알려진 한계: 이순신 그림의 갑옷은 조선 두정갑과 다르게 서양식 흉갑이 섞여 그려졌습니다. 배경 장면의 인물·건물도 역사 고증을 거친 그림이 아닙니다.

## 배경 (1920×1080)

| 파일 | 프롬프트 (장면 부분) |
|---|---|
| `events/theater_hall.png` | an old abandoned underworld movie theater, rows of empty red velvet seats seen from behind in darkness, a pale glowing blank cinema screen far ahead, a beam of projector light cutting through dust and drifting smoke, three dark silhouettes sitting in the seats, no faces visible, lower third of the image dark |
| `events/scenes/bridge.png` | You were leading people across a collapsing bridge. It buckles, and fire rises behind you. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/siege.png` | The last night of a besieged fortress. The gate is cracked and the arrows are gone. At dawn it ends. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/storm.png` | A storm at sea. The mast is broken and the ship lists. There is only one lifeboat. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/temple.png` | A burning temple. The roof is falling in, and people are still trapped inside. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/scaffold.png` | The square before the scaffold. The crowd roars. Take back a single word, they say, and you will live. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/snowmarch.png` | A march through snowbound mountains. The road is buried, soldiers fall one by one. Turn back, and you live. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/flood.png` | A flooded village. The dike has burst and the water reaches the roofs. There is one boat. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/well.png` | A desert well. There is water enough for one, and thirsty people stand all around. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/mine.png` | A collapsing mine. The exit is blocked and the last lamp is dying. There is a narrow crack above. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/plague.png` | A gate in a time of plague. The sick are inside, refugees outside. Open the gate, and the sickness may spread. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/border.png` | A border bridge at night. The pursuers' torches draw near. Cut the bridge, and your own side cannot cross either. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/coronation.png` | The eve of a coronation. Rebel blades approach through the cathedral crypt. The crown still rests on the altar. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/arena.png` | The middle of an arena. The crowd wants blood, and your fallen opponent begs for their life. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/ambush.png` | Ambushed in a narrow gorge. Arrows rain from the cliffs on both sides. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/mutiny.png` | A ship in mutiny. The crew have drawn blades and demand that you give up command. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/famine.png` | A city in famine. The granary key is in your hand. People in the villages outside are starving too. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/duel.png` | A duel at dawn. Your opponent is your old teacher. It ends only when one falls. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/exile.png` | An exile's island in the middle of the sea. A guard ship circles it. Tonight, a single boat will land. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/false_order.png` | In the command tent. A false order has come from court. Obey, and thousands die. Refuse, and you die. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/river.png` | A flooded riverbank. The pursuers are closing in. The boat is small, and the current is strong. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/tribunal.png` | A tribunal. The witness has lied. Just before the verdict, you are given one chance to speak. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/hostage.png` | The enemy commander holds a hostage. Lay down your arms, and they will be released. You cannot trust it. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/last_camp.png` | The last camp after a defeat. By tomorrow the encirclement will close. The remaining soldiers look only to you. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/scenes/tower.png` | The top of a collapsing tower. The stairs are gone and fire climbs from below. The next tower might be one leap away. Wide establishing shot, no faces, people only as small distant silhouettes, the lower third of the image darker. |
| `events/figures/guanyu.png` | a wide river crossing among misty green mountains in ancient China at dusk, war banners on poles, an empty riverbank, no people, empty space in the center of the image, the lower third darker |
| `events/figures/joan.png` | a fortified medieval French walled city at dawn with tall towers and banners, a muddy siege field in front, no people, empty space in the center of the image, the lower third darker |
| `events/figures/leonidas.png` | the narrow coastal mountain pass of Thermopylae between steep cliffs and the sea at sunset, ancient Greek stone wall, no people, empty space in the center of the image, the lower third darker |
| `events/figures/genghis.png` | the endless Mongolian steppe at golden hour, round white felt gers in the distance, wild horses, vast sky, no people, empty space in the center of the image, the lower third darker |
| `events/figures/yisunsin.png` | a narrow sea strait between dark rocky islands on the southern coast of Korea at dusk, strong tidal current swirling white around the rocks, pine trees on the cliffs, empty water, no people, empty space in the center of the image, the lower third darker |
| `events/figures/napoleon.png` | a misty early nineteenth-century battlefield at dawn, a lone field cannon on a hill, distant ranks of infantry as tiny silhouettes, rising sun, empty space in the center of the image, the lower third darker |

## 캐릭터

| 파일 | 사용한 생성물 | 프롬프트 (인물 부분) |
|---|---|---|
| `character/reaper.png` | `reaper_stand_3` | a tall grim reaper man in a long black trench coat with the collar turned up and a black fedora hat, black leather gloves, his face completely hidden by swirling grey smoke under the hat brim, no face, no eyes, smoke drifting from his collar, standing calmly, three-quarter view facing right |
| `character/reaper_rest.png` | `reaper_sit_8` | a person SITTING DOWN on a low flat rock, seated pose, knees bent, legs folded, side view facing right, whole body visible including feet, elbows resting on his knees, a tall grim reaper man in a long black trench coat with the collar turned up and a black fedora hat, black leather gloves, his face completely hidden by swirling grey smoke under the hat brim, no face, no eyes, smoke drifting from his collar, resting quietly by a campfire at night |
| `character/spirit_guanyu.png` | `spirit_guanyu_stand_9` | Guan Yu, a Chinese general of the Three Kingdoms period, very long flowing black beard, dark green robe over armor, green headscarf, holding a guandao crescent-blade polearm upright beside him, stern noble expression, standing, three-quarter view facing right |
| `character/spirit_guanyu_rest.png` | `spirit_guanyu_sit_4` | a person SITTING DOWN on a low flat rock, seated pose, knees bent, legs folded, side view facing right, whole body visible including feet, Guan Yu, a Chinese general of the Three Kingdoms period, very long flowing black beard, dark green robe over armor, green headscarf, stern noble expression, weapon lying on the ground beside the rock, resting quietly by a campfire at night |
| `character/spirit_joan.png` | `spirit_joan_stand_9` | Joan of Arc, a young French woman with a short bob haircut in full medieval plate armor, holding a white banner on a long staff, a sword at her hip, determined calm expression, standing, three-quarter view facing right |
| `character/spirit_joan_rest.png` | `spirit_joan_sit_4` | a person SITTING DOWN on a low flat rock, seated pose, knees bent, legs folded, side view facing right, whole body visible including feet, Joan of Arc, a young French woman with a short bob haircut in full medieval plate armor, a sword at her hip, determined calm expression, weapon lying on the ground beside the rock, resting quietly by a campfire at night |
| `character/spirit_leonidas.png` | `spirit_leonidas_stand_11` | Leonidas king of Sparta, bronze Corinthian helmet with a horsehair crest, bronze round hoplite shield with a lambda symbol, long spear, red cloak, muscular, bearded, standing, three-quarter view facing right |
| `character/spirit_leonidas_rest.png` | `spirit_leonidas_sit_11` | a person SITTING DOWN on a low flat rock, seated pose, knees bent, legs folded, side view facing right, whole body visible including feet, Leonidas king of Sparta, bronze Corinthian helmet with a horsehair crest, bronze round hoplite shield with a lambda symbol, long spear, red cloak, muscular, bearded, weapon lying on the ground beside the rock, resting quietly by a campfire at night |
| `character/spirit_genghis.png` | `spirit_genghis_stand_4` | Genghis Khan, Mongol ruler, fur-trimmed pointed Mongol hat, long moustache and beard, Mongol deel robe with a wide sash, holding a recurve composite bow, quiver of arrows, standing, three-quarter view facing right |
| `character/spirit_genghis_rest.png` | `spirit_genghis_sit_4` | a person SITTING DOWN on a low flat rock, seated pose, knees bent, legs folded, side view facing right, whole body visible including feet, Genghis Khan, Mongol ruler, fur-trimmed pointed Mongol hat, long moustache and beard, Mongol deel robe with a wide sash, quiver of arrows, weapon lying on the ground beside the rock, resting quietly by a campfire at night |
| `character/spirit_yisunsin.png` | `spirit_yisunsin_stand_9` | Joseon dynasty Korean admiral in traditional Korean general armor, a studded long armor coat (dujeonggap) and an iron helmet with a tall spike and red plume, holding a long Korean sword point down in front of him with both hands on the hilt, stern dignified expression, short beard, standing, three-quarter view facing right |
| `character/spirit_yisunsin_rest.png` | `spirit_yisunsin_sit_4` | a person SITTING DOWN on a low flat rock, seated pose, knees bent, legs folded, side view facing right, whole body visible including feet, Joseon dynasty Korean admiral in traditional Korean general armor, a studded long armor coat (dujeonggap) and an iron helmet with a tall spike and red plume, stern dignified expression, short beard, weapon lying on the ground beside the rock, resting quietly by a campfire at night |
| `character/spirit_napoleon.png` | `spirit_napoleon_stand_4` | Napoleon Bonaparte, black bicorne hat worn sideways, dark blue French uniform coat with gold epaulettes, white waistcoat and breeches, black boots, right hand tucked into his waistcoat, standing, three-quarter view facing right |
| `character/spirit_napoleon_rest.png` | `spirit_napoleon_sit_4` | a person SITTING DOWN on a low flat rock, seated pose, knees bent, legs folded, side view facing right, whole body visible including feet, Napoleon Bonaparte, black bicorne hat worn sideways, dark blue French uniform coat with gold epaulettes, white waistcoat and breeches, black boots, weapon lying on the ground beside the rock, resting quietly by a campfire at night |

공통 제외 프롬프트(배경): `text, letters, words, watermark, signature, logo, frame, border, ui, photo, 3d render, anime, cartoon, close-up, modern objects, cars, nsfw`

`character/char_select_bg.png`는 AI 그림이 아니라 `tools/gen_character.py`로 그린 절차적 그림입니다.
