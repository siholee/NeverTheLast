# ABYSS_HORROR
상태: **검수 대기** · 2026-11-05 · 묶음 e · 최종 승인 아님
근거: 60_enemies.yaml codes/reworkKit + 20_codes.yaml; N/U 12071 / P22071; YAML theme121; Elite 원화
N: 적2명·접촉·촉수 타격·ATK60%+바람 부착.
U: 비접촉 집중 준비→차회 일반행동 전체 ATK165%(20_codes 설명).
P: 전투 시작 DEF+25%·DEF300% 보호막; 제어 시 해제. SP/별도 소환수 없음.
해석/원화 차이: 원화 키는 발주대로; theme121을122로 수정하지 않음. 집중은 촉수 모으는 준비와 공용.
실제 프롬프트 요약: 이름 없이 청록 결정 셸·굵고 짧은 촉수, 두 촉수 치켜듦→왼쪽 두 갈래 뻗음. 좌향, 남색 먹선+회화 명암, transparent background, 무배경/무그림자/무VFX/무글자.
재생성 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 0/0/0/0/0; 내장 image_gen. 최초 생성 제외, 각 포즈 최대2회 이내.
후처리: 1122×1402 생성 원본 보존→머리 ROI 기준 전신 균등 배율로 캔버스 규격화→밝고 무채색인 1px 알파 경계 흰 번짐만 제거→LockBits 정수 이동. 발 정렬 재샘플링 없음.
실측: 최종 1024×1280·8bit RGBA·모서리alpha0·alpha≥129 x30..994. 머리는 수동 지정 보이는 덩어리 ROI 외접 w×h; rawROI/배율/이동/SHA256은 작업 폴더 measurements.json, 경계는 head_roi 시트 공개.
- IDLE: 발 y=1220; 머리 612×377; √=480.337px; IDLE 0.000%; x=137..887
- WINDUP: 발 y=1220; 머리 620×372; √=480.25px; IDLE -0.018%; x=90..933
- STRIKE: 발 y=1220; 머리 612×377; √=480.337px; IDLE 0.000%; x=85..938
- HIT: 발 y=1220; 머리 610×378; √=480.187px; IDLE -0.031%; x=110..912
- DOWN: 발 y=1220; 머리 715×322; √=479.823px; IDLE -0.107%; x=53..969
자체 시각 확인/남은 항목: 가림에 따른 촉수 연결/가시 개수와 머리 셸 대 돌출 결정의 경계 재확인 필요. 수치±3% 이내를 육안 PASS로 확대하지 않음.
산출: 원본5장 + sheets/{KEY}_poses·silhouette·face·battle250.png, 추가 head_roi.png. battle250은 IDLE alpha높이250px를 기준으로 5장에 같은 배율. 흰 번짐 전후2배 비교/생성 원본은 _work_{KEY}/ 보존. Unity 등록/실행 검수 미실시.
