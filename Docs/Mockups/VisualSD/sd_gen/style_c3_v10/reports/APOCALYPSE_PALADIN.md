# APOCALYPSE_PALADIN
상태: **검수 대기** · 2026-11-05 · Root 최종 승인 전.
근거: 60_enemies.yaml themeId108 codes·reworkKit + 20_codes.yaml; N/U 12020 · P 22020.
N: 적 단일·접촉·검 베기, ATK 90%.
U: 적 전체·접촉 설정·광역 공격, ATK 85%.
P: 자신·비접촉·추가 효과 없음.
WINDUP→STRIKE: 흑철 중갑·붉은 검 홈·중층 견갑; 강한 내려베기; 가장 빈번한 N 기준, SD_Spec §6의 기존 무기·소품과 구분.
원화·키트 차이: 없음; 일반보다 중층 흑철 갑주·붉은 검 홈·큰 대검/방패로 강화.
프롬프트: 외형 묘사·2.5등신·좌향·먹선/회화 명암·무문자/무VFX/투명; 실제 문장 `_work_APOCALYPSE_PALADIN/prompts_final.json`.
재생성 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 1/1/1/2/1; 채택 원본은 metrics.json.source.
후처리: raw 보존 → 전체 균등 배율 규격화·머리 맞춤 → 경계 6px 이내 휘도>185/채도<0.18만 제거 → LockBits 정수 이동.
5장 모두 1024×1280·8bit RGBA·모서리 α0·α≥129 최하단 y1220; x범위 IDLE:146..876 / WINDUP:132..891 / STRIKE:120..902 / HIT:101..922 / DOWN:38..985.
IDLE: 머리 ROI(x,y,w,h)=(314,528,323,290), √(w×h)=306.06px, IDLE 대비 0%, 배율 0.72550, y 정수이동 372.
WINDUP: 머리 ROI(x,y,w,h)=(285,624,323,290), √(w×h)=306.06px, IDLE 대비 0%, 배율 0.74314, y 정수이동 307.
STRIKE: 머리 ROI(x,y,w,h)=(327,633,324,289), √(w×h)=306px, IDLE 대비 -0.02%, 배율 0.76166, y 정수이동 460.
HIT: 머리 ROI(x,y,w,h)=(564,459,325,288), √(w×h)=305.94px, IDLE 대비 -0.04%, 배율 0.82322, y 정수이동 328.
DOWN: 머리 ROI(x,y,w,h)=(313,673,327,287), √(w×h)=306.35px, IDLE 대비 0.10%, 배율 0.88268, y 정수이동 530.
시트: sheets/APOCALYPSE_PALADIN_{poses,silhouette,face,battle250,head_roi}.png; IDLE 높이250 공통 배율. 경계 전후2배: _work_APOCALYPSE_PALADIN/edge_before_after_2x.png.
남은 결함/판단: 검/방패 금장·사슬 배치의 미세 차이; HIT 투구 기울기에 따른 가로 인상은 250px 시트로 Root 확인.
등신 보조값(보이는 머리 ROI 상단~발 ÷ ROI 높이): 2.39; 수동 ROI이며 Root가 원화/250px 시트와 재검수. Unity 등록·실행 미수행.
