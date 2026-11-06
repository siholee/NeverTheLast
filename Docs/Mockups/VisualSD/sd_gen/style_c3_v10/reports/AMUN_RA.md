# AMUN_RA
상태: **재검수 대기** · 2026-11-05 · Root 재검수 전.
근거: 60_enemies.yaml themeId108 codes·reworkKit + 20_codes.yaml; N/U 13012 · P 23012.
N: 적 2명·비접촉·검 형태 판정 ATK60%.
U: 적 전체·비접촉·광역 공격 ATK85%.
P: 자신·비접촉·추가 효과 없음.
WINDUP→STRIKE: 흰 대리석 피부·태양 금속 후광·굽은 왕홀로 거리 유지 지휘; 가장 빈번한 N 기준, SD_Spec §6의 기존 무기·소품과 구분.
Root HOLD 보정: WINDUP 왕홀을 다른 4장과 같은 흑금색 말린 고리·태양 접합부·두 줄 장식으로 통일.
프롬프트: 이름 없이 외형·좌향·투명·먹선/회화 명암·무문자/무VFX; 실제 전문 `_work_AMUN_RA/fix1/prompts.json`.
이번 추가 생성 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 0/1/0/0/0; 포즈당 최대2회 준수. 이전 회차 이력은 fix1/_superseded/report.md 보존.
처리: 전신만 균등 배율(발 중심 고정, 가로 여백을 위한 정수 x이동) → 생성본의 경계6px/휘도>185/채도<0.18만 제거 → LockBits 정수 y이동; 두개부만 변형하지 않음.
등록 사본 5장: 1024×1280·8bit RGBA·모서리 α0·좌향·α≥129 최하단 y1220; x범위 IDLE:164..859 / WINDUP:88..883 / STRIKE:47..975 / HIT:56..967 / DOWN:38..985.
IDLE: 보이는 두개부 ROI(후드/장식 제외) = (350,492,276,263), √(w×h)=269.42px, IDLE 대비 0%, 전신 배율 1, 정수 y이동 0.
WINDUP: 보이는 두개부 ROI(후드/장식 제외) = (291,500,280,259), √(w×h)=269.30px, IDLE 대비 -0.05%, 전신 배율 0.91244, 정수 y이동 121.
STRIKE: 보이는 두개부 ROI(후드/장식 제외) = (393,571,285,254), √(w×h)=269.05px, IDLE 대비 -0.14%, 전신 배율 1, 정수 y이동 0.
HIT: 보이는 두개부 ROI(후드/장식 제외) = (380,628,285,254), √(w×h)=269.05px, IDLE 대비 -0.14%, 전신 배율 1, 정수 y이동 0.
DOWN: 보이는 두개부 ROI(후드/장식 제외) = (253,762,289,251), √(w×h)=269.33px, IDLE 대비 -0.03%, 전신 배율 1, 정수 y이동 0.
산출: registered/AMUN_RA_SD_*.png; sheets/AMUN_RA_{poses,silhouette,face,battle250}.png(전부 등록 사본 기준·IDLE 높이250 공통 배율).
원본 PNG 불변. 수동 ROI/배율/실측과 경계 전후2배는 `_work_AMUN_RA/fix1/`; 250px·실루엣·초상 비교 자체 확인, 의상 장식 미세 차이는 Root 판단.
남은 결함: 이번 HOLD 지적은 보정함. 최종 승인 전이며 수동 두개부 ROI와 크기 인상 재검수 필요. Assets/Library 쓰기·Unity·커밋·파일 삭제 없음.
