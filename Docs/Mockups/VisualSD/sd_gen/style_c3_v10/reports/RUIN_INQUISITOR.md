# RUIN_INQUISITOR
상태: **검수 대기** · 2026-11-05 · Root 최종 승인 전.
근거: 60_enemies.yaml themeId108 codes·reworkKit + 20_codes.yaml; N/U 11051 · P 21051.
N: 아군 전체·비접촉·시전자 최대 HP 2.5% 치유.
U: 적 전체·비접촉·불 주문, ATK 85%.
P: 자신·비접촉·동료 1명당 가피증 3%, 최대12%.
WINDUP→STRIKE: 백색 후드·책을 통한 축복; 공격 없는 N 기준; 가장 빈번한 N 기준, SD_Spec §6의 기존 무기·소품과 구분.
원화·키트 차이: 원화의 검을 칼집에 넣음; 페이지 무문자; 후드를 뒤로 밀어 눈과 보이는 머리를 노출, 칼집 추가.
프롬프트: 외형 묘사·2.5등신·좌향·먹선/회화 명암·무문자/무VFX/투명; 실제 문장 `_work_RUIN_INQUISITOR/prompts_final.json`.
재생성 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 1/0/0/0/0; 채택 원본은 metrics.json.source.
후처리: raw 보존 → 전체 균등 배율 규격화·머리 맞춤 → 경계 6px 이내 휘도>185/채도<0.18만 제거 → LockBits 정수 이동.
5장 모두 1024×1280·8bit RGBA·모서리 α0·α≥129 최하단 y1220; x범위 IDLE:201..822 / WINDUP:163..859 / STRIKE:135..888 / HIT:93..929 / DOWN:148..875.
IDLE: 머리 ROI(x,y,w,h)=(266,203,461,408), √(w×h)=433.69px, IDLE 대비 0%, 배율 0.88685, y 정수이동 46.
WINDUP: 머리 ROI(x,y,w,h)=(232,202,461,408), √(w×h)=433.69px, IDLE 대비 0%, 배율 0.88685, y 정수이동 41.
STRIKE: 머리 ROI(x,y,w,h)=(332,203,461,408), √(w×h)=433.69px, IDLE 대비 0%, 배율 0.88685, y 정수이동 46.
HIT: 머리 ROI(x,y,w,h)=(369,213,464,406), √(w×h)=434.03px, IDLE 대비 0.08%, 배율 0.89171, y 정수이동 53.
DOWN: 머리 ROI(x,y,w,h)=(257,440,462,407), √(w×h)=433.63px, IDLE 대비 -0.01%, 배율 0.90540, y 정수이동 246.
시트: sheets/RUIN_INQUISITOR_{poses,silhouette,face,battle250,head_roi}.png; IDLE 높이250 공통 배율. 경계 전후2배: _work_RUIN_INQUISITOR/edge_before_after_2x.png.
남은 결함/판단: 원화보다 후드가 뒤로 밀리고 머리 노출이 큼; 책 크기·금장 장식 미세 차이. 지원 STRIKE 확인 완료, 최종 승인 전.
등신 보조값(보이는 머리 ROI 상단~발 ÷ ROI 높이): 2.5; 수동 ROI이며 Root가 원화/250px 시트와 재검수. Unity 등록·실행 미수행.
