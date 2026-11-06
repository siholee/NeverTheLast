# RUIN_PALADIN
상태: **재검수 대기** · 2026-11-05 · Root 재검수 전.
근거: 60_enemies.yaml themeId108 codes·reworkKit + 20_codes.yaml; N/U 11050 · P 21050.
N: 적 단일·접촉·검 베기, ATK 90%.
U: 적 전체·접촉 설정·광역 공격, ATK 85%.
P: 자신·비접촉·추가 효과 없음.
WINDUP→STRIKE: 검·방패, 백색 갑주와 적색 천; 수평 절단; 가장 빈번한 N 기준, SD_Spec §6의 기존 무기·소품과 구분.
Root HOLD 보정: STRIKE의 긴 수평 검·선 자세 몸통/다리 비율 재제작. 가로 한계에 맞춰 나머지 4장도 전신 균등 축소, 원본 보존.
프롬프트: 이름 없이 외형·좌향·투명·먹선/회화 명암·무문자/무VFX; 실제 전문 `_work_RUIN_PALADIN/fix1/prompts.json`.
이번 추가 생성 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 0/0/2/0/0; 포즈당 최대2회 준수. 이전 회차 이력은 fix1/_superseded/report.md 보존.
처리: 전신만 균등 배율(발 중심 고정, 가로 여백을 위한 정수 x이동) → 생성본의 경계6px/휘도>185/채도<0.18만 제거 → LockBits 정수 y이동; 두개부만 변형하지 않음.
등록 사본 5장: 1024×1280·8bit RGBA·모서리 α0·좌향·α≥129 최하단 y1220; x범위 IDLE:128..918 / WINDUP:105..888 / STRIKE:32..991 / HIT:89..961 / DOWN:39..934.
IDLE: 보이는 두개부 ROI(후드/장식 제외) = (427,394,352,313), √(w×h)=331.93px, IDLE 대비 0%, 전신 배율 0.94553, 정수 y이동 267.
WINDUP: 보이는 두개부 ROI(후드/장식 제외) = (284,418,352,312), √(w×h)=331.40px, IDLE 대비 -0.16%, 전신 배율 0.94696, 정수 y이동 278.
STRIKE: 보이는 두개부 ROI(후드/장식 제외) = (542,357,341,307), √(w×h)=323.55px, IDLE 대비 -2.52%, 전신 배율 0.87671, 정수 y이동 227.
HIT: 보이는 두개부 ROI(후드/장식 제외) = (545,328,374,294), √(w×h)=331.60px, IDLE 대비 -0.10%, 전신 배율 0.94664, 정수 y이동 194.
DOWN: 보이는 두개부 ROI(후드/장식 제외) = (315,551,325,338), √(w×h)=331.44px, IDLE 대비 -0.15%, 전신 배율 0.94545, 정수 y이동 447.
산출: registered/RUIN_PALADIN_SD_*.png; sheets/RUIN_PALADIN_{poses,silhouette,face,battle250}.png(전부 등록 사본 기준·IDLE 높이250 공통 배율).
원본 PNG 불변. 수동 ROI/배율/실측과 경계 전후2배는 `_work_RUIN_PALADIN/fix1/`; 250px·실루엣·초상 비교 자체 확인, 의상 장식 미세 차이는 Root 판단.
남은 결함: 이번 HOLD 지적은 보정함. 최종 승인 전이며 수동 두개부 ROI와 크기 인상 재검수 필요. Assets/Library 쓰기·Unity·커밋·파일 삭제 없음.
