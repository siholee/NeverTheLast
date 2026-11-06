# FALLEN_PRIEST
상태: **검수 대기** · 2026-11-05 · Root 최종 승인 전.
근거: 60_enemies.yaml themeId108 codes·reworkKit + 20_codes.yaml; N/U 11053 · P 21053.
N: 적 단일·비접촉·불 주문 ATK90%와 불 부착(효과명중).
U: 아군 전체·비접촉·시전자 DEF150% 보호막.
P: 자신·비접촉·동료 1명당 가피증3%, 최대12%.
WINDUP→STRIKE: 검정 베일·고리 지팡이를 돌려 명령하는 주문 동작; 가장 빈번한 N 기준, SD_Spec §6의 기존 무기·소품과 구분.
원화·키트 차이: 원화 장식의 글자 제거; 노출을 줄인 안감; 노출을 줄인 안감 추가; 후드·검은 베일·고리/날개 지팡이 유지.
프롬프트: 외형 묘사·2.5등신·좌향·먹선/회화 명암·무문자/무VFX/투명; 실제 문장 `_work_FALLEN_PRIEST/prompts_final.json`.
재생성 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 1/2/1/1/1; 채택 원본은 metrics.json.source.
후처리: raw 보존 → 전체 균등 배율 규격화·머리 맞춤 → 경계 6px 이내 휘도>185/채도<0.18만 제거 → LockBits 정수 이동.
5장 모두 1024×1280·8bit RGBA·모서리 α0·α≥129 최하단 y1220; x범위 IDLE:188..835 / WINDUP:63..959 / STRIKE:131..891 / HIT:170..853 / DOWN:197..825.
IDLE: 머리 ROI(x,y,w,h)=(265,487,278,274), √(w×h)=275.99px, IDLE 대비 0%, 배율 0.79323, y 정수이동 212.
WINDUP: 머리 ROI(x,y,w,h)=(243,370,278,273), √(w×h)=275.49px, IDLE 대비 -0.18%, 배율 0.91123, y 정수이동 41.
STRIKE: 머리 ROI(x,y,w,h)=(501,495,284,268), √(w×h)=275.88px, IDLE 대비 -0.04%, 배율 0.81105, y 정수이동 232.
HIT: 머리 ROI(x,y,w,h)=(268,491,278,274), √(w×h)=275.99px, IDLE 대비 0%, 배율 0.81673, y 정수이동 244.
DOWN: 머리 ROI(x,y,w,h)=(313,698,280,271), √(w×h)=275.46px, IDLE 대비 -0.19%, 배율 0.95062, y 정수이동 417.
시트: sheets/FALLEN_PRIEST_{poses,silhouette,face,battle250,head_roi}.png; IDLE 높이250 공통 배율. 경계 전후2배: _work_FALLEN_PRIEST/edge_before_after_2x.png.
남은 결함/판단: WINDUP r2의 막힌 고리는 제외하고 열린 고리 r1 채택. WINDUP의 지팡이 길이와 소매/주름 밀도 차이를 Root 확인.
등신 보조값(보이는 머리 ROI 상단~발 ÷ ROI 높이): 2.68; 수동 ROI이며 Root가 원화/250px 시트와 재검수. Unity 등록·실행 미수행.
