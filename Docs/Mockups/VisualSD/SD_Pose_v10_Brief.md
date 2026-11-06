# SD 10차 지시서 · 2026-11-05

발주: 사용자 → Root(PM) → Codex · 규격·검수는 [SD_Spec.md](SD_Spec.md)가 기준이다(이 문서는 범위와 추가 규칙만). 머리 측정은 [9차 지시서](SD_Pose_v9_Brief.md)의 "보이는 머리 덩어리" 규칙을 그대로 쓴다.

## 범위(전부 적, 왼쪽 방향)

| 묶음 | 테마(themeId) | 대상(원화 키 · 이름 · 등급 · 키트) |
| --- | --- | --- |
| A 아스완 | 아스완(108) | RUIN_PALADIN 파멸의 성기사(일반·검·단일) · APOCALYPSE_PALADIN 종말의 성기사(정예) · RUIN_INQUISITOR 파멸의 이단심문관(일반·법사·치유) · APOCALYPSE_INQUISITOR 종말의 이단심문관(정예) · NETHER_WRAITH 명계의 사령(일반·검) · FALLEN_PRIEST 타락한 사제(일반·법사) · OSIRIS 오시리스(보스·법사·광역) · AMUN_RA 아문·라(보스·검) |
| B 노르드 | 노르드(117) | BERSERKER 광전사(일반·도끼) · EINHAR 에인하르(일반·도끼) · RAGNAR 라그나르(정예·도끼) · VOLVA 볼바(일반·법사·치유) · NORN 노른(일반·법사·치유) · THOR 토르(보스·망치) |
| C 노르드 북부 | 노르드 북부(115) | 신규: APOCALYPSE_GIANT 종말의 거인(정예·철퇴) · FROST_GIANT 혹한의 거인(보스·광역) · VOID_SEED 공허의 씨앗(일반·사격) · FROST_SEED 혹한의 씨앗(정예·사격) / 얼음 변종: VOID_PRISM_CRYO · VOID_WOLF_CRYO · VOID_MONSTROUS_BIRD_CRYO · VOID_BOAR_CRYO · VOID_MARKSMAN_CRYO · VOID_VANGUARD_CRYO |
| D 노르드 중부 | 노르드 중부(119) | VALKYRIE 발키리(일반·창 투척) · SIGURD 시구르드(정예·검) · BRYNHILD 브륀힐드(정예·창) · ODIN_PHASE1 오딘(보스·창) |
| E 오디세이 | 오디세이(122) | VOID_SCOUT 공허의 정찰병(일반·광선) · ABYSS_HUNTER 공허의 사냥꾼(일반·사격) · VOID_SLAUGHTERER 공허의 학살자(일반·포격) · HIPPOCAMPUS 히포캄푸스(일반·들이받기) · SIREN 세이렌(일반·노래) · ABYSS_HORROR 심연의 공포(정예·촉수) · SCYLLA 스킬라(보스·광역) |

- 키트는 `60_enemies.yaml`의 codes·reworkKit(normalName/ultimateName/normalMode)·`20_codes.yaml`에서 읽는다. 원화는 `Assets/Resources/Sprite/Standings/Enemies/{Normal|Elite|Boss}/{KEY}_STANDING.png`와 Portraits.
- 기존 SD가 있는 HORUS(아스완 보스, 아군 SD 공용)·VOID_VANGUARD·VOID_PRISM(노르드 중부)은 만들지 않는다.
- 일반병은 얼굴을 유지한다(익명 눈 차폐는 로마 군단 일반병 전용). 같은 계열의 일반/정예(성기사·이단심문관·씨앗·광전사 계열)는 정예가 장식·체격·무기로 한 단계 위로 보이게 한다. 에인하르는 광전사와 같은 도끼 계열이지만 실루엣이 겹치지 않게 원화 차이를 살린다.
- 보스·신격(오시리스·아문·라·토르·오딘·혹한의 거인·스킬라)은 원화의 위엄을 유지하되 초월자 정숙 톤은 쓰지 않는다(세이·시·츠쿠요미 전용). 거인은 SD 캔버스 안에서 덩치가 커 보이게 하되 발 y1220·머리 측정 규칙은 같다.
- 비인간형(히포캄푸스·심연의 공포·스킬라·씨앗·괴수류)은 SD_Spec의 동물형 SD 비율 규칙을 따른다.

## 얼음 변종 규칙(C 묶음)

원화 `{KEY}_CRYO_STANDING`과 기존 `{KEY}_STANDING`을 먼저 비교한다.
- 형태가 같고 색만 다르면: 기존 등록 SD(`Assets/Resources/Sprite/SD/{KEY}_SD_*.png`, 읽기만)를 원화 얼음 팔레트로 재채색해 `{KEY}_CRYO_SD_*` 5장을 만든다. 형태·발·머리값은 원본과 같아야 하고, 재채색은 색상만 바꾸며 윤곽선 굵기·명암 단계를 보존한다.
- 형태(뿔·결정·갑옷 등)가 다르면 해당 개체만 새로 생성한다. 어느 쪽으로 했는지 보고서에 적는다.

## 산출물

`sd_gen/style_c3_v10/{KEY}_SD_*.png`, `sheets/{KEY}_poses|silhouette|face|battle250.png`, `reports/{KEY}.md`(상태 "검수 대기"). 묶음별 로그는 `Logs/SDPoseV10_20261105/codex_{a|b|c|d|e}.md`(20줄 이내). 등록은 Root 검수 뒤 별도로 한다.
