# OSIRIS
상태: **검수 대기** · 2026-11-05 · Root 최종 승인 전.
근거: 60_enemies.yaml themeId108 codes·reworkKit + 20_codes.yaml; N/U 13011 · P 23011.
N: 적 전체·비접촉·바위 주문 ATK45%와 바위 부착(효과명중).
U: 적 전체·비접촉·광역 주문 ATK85%.
P: 자신·비접촉·추가 효과 없음.
WINDUP→STRIKE: 창백한 붕대 왕·짧은 굽은 왕홀, 넓게 내리는 지배 동작; 가장 빈번한 N 기준, SD_Spec §6의 기존 무기·소품과 구분.
원화·키트 차이: 붕대의 봉합과 장식만 유지, 문자 삭제; 창백한 붕대 피부·붉은 눈·검정/금 머리천·왕홀 유지; 문자 제거.
프롬프트: 외형 묘사·2.5등신·좌향·먹선/회화 명암·무문자/무VFX/투명; 실제 문장 `_work_OSIRIS/prompts_final.json`.
재생성 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 2/2/2/2/2; 채택 원본은 metrics.json.source.
후처리: raw 보존 → 전체 균등 배율 규격화·머리 맞춤 → 경계 6px 이내 휘도>185/채도<0.18만 제거 → LockBits 정수 이동.
5장 모두 1024×1280·8bit RGBA·모서리 α0·α≥129 최하단 y1220; x범위 IDLE:174..849 / WINDUP:109..913 / STRIKE:83..940 / HIT:107..916 / DOWN:38..985.
IDLE: 머리 ROI(x,y,w,h)=(363,441,289,285), √(w×h)=286.99px, IDLE 대비 0%, 배율 0.78024, y 정수이동 239.
WINDUP: 머리 ROI(x,y,w,h)=(270,540,310,265), √(w×h)=286.62px, IDLE 대비 -0.13%, 배율 0.82773, y 정수이동 215.
STRIKE: 머리 ROI(x,y,w,h)=(330,529,304,271), √(w×h)=287.03px, IDLE 대비 0.01%, 배율 0.82058, y 정수이동 266.
HIT: 머리 ROI(x,y,w,h)=(379,472,305,269), √(w×h)=286.43px, IDLE 대비 -0.19%, 배율 0.80364, y 정수이동 278.
DOWN: 머리 ROI(x,y,w,h)=(321,731,311,264), √(w×h)=286.54px, IDLE 대비 -0.16%, 배율 0.94328, y 정수이동 479.
시트: sheets/OSIRIS_{poses,silhouette,face,battle250,head_roi}.png; IDLE 높이250 공통 배율. 경계 전후2배: _work_OSIRIS/edge_before_after_2x.png.
남은 결함/판단: 봉합선 위치·붕대 감김·왕관 금띠 세부 미세 차이. 일반행동의 광역 원격 손짓 및 250px 구분 확인, 최종 승인 전.
등신 보조값(보이는 머리 ROI 상단~발 ÷ ROI 높이): 2.74; 수동 ROI이며 Root가 원화/250px 시트와 재검수. Unity 등록·실행 미수행.
