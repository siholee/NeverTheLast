# APOCALYPSE_INQUISITOR
상태: **재검수 대기** · 2026-11-05 · Root 재검수 전.
근거: 60_enemies.yaml themeId108 codes·reworkKit + 20_codes.yaml; N/U 12021 · P 22021.
N: 아군 전체·비접촉·시전자 최대 HP 2.5% 치유.
U: 적 전체·비접촉·불 주문, ATK 85%.
P: 자신·비접촉·동료 1명당 가피증 3%, 최대12%.
WINDUP→STRIKE: 흑색 후드·큰 책·장식 사슬의 치유 집전; 공격 없는 N 기준; 가장 빈번한 N 기준, SD_Spec §6의 기존 무기·소품과 구분.
Root HOLD 보정: 5포즈 모두 흑색 후드 착용·붉은 안감·금속 테두리로 수정. 책·사슬·칼집 유지, STRIKE는 펼친 손의 치유 지원 동작.
프롬프트: 이름 없이 외형·좌향·투명·먹선/회화 명암·무문자/무VFX; 실제 전문 `_work_APOCALYPSE_INQUISITOR/fix1/prompts.json`.
이번 추가 생성 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 1/1/1/1/1; 포즈당 최대2회 준수. 이전 회차 이력은 fix1/_superseded/report.md 보존.
처리: 전신만 균등 배율(발 중심 고정, 가로 여백을 위한 정수 x이동) → 생성본의 경계6px/휘도>185/채도<0.18만 제거 → LockBits 정수 y이동; 두개부만 변형하지 않음.
등록 사본 5장: 1024×1280·8bit RGBA·모서리 α0·좌향·α≥129 최하단 y1220; x범위 IDLE:172..853 / WINDUP:124..903 / STRIKE:134..892 / HIT:78..907 / DOWN:75..959.
IDLE: 보이는 두개부 ROI(후드/장식 제외) = (273,235,379,295), √(w×h)=334.37px, IDLE 대비 0%, 전신 배율 0.92203, 정수 y이동 56.
WINDUP: 보이는 두개부 ROI(후드/장식 제외) = (261,219,366,306), √(w×h)=334.66px, IDLE 대비 0.09%, 전신 배율 0.93252, 정수 y이동 40.
STRIKE: 보이는 두개부 ROI(후드/장식 제외) = (306,237,378,296), √(w×h)=334.50px, IDLE 대비 0.04%, 전신 배율 0.91410, 정수 y이동 55.
HIT: 보이는 두개부 ROI(후드/장식 제외) = (385,185,368,304), √(w×h)=334.47px, IDLE 대비 0.03%, 전신 배율 0.94896, 정수 y이동 11.
DOWN: 보이는 두개부 ROI(후드/장식 제외) = (235,425,389,287), √(w×h)=334.13px, IDLE 대비 -0.07%, 전신 배율 1.00815, 정수 y이동 180.
산출: registered/APOCALYPSE_INQUISITOR_SD_*.png; sheets/APOCALYPSE_INQUISITOR_{poses,silhouette,face,battle250}.png(전부 등록 사본 기준·IDLE 높이250 공통 배율).
원본 PNG 불변. 수동 ROI/배율/실측과 경계 전후2배는 `_work_APOCALYPSE_INQUISITOR/fix1/`; 250px·실루엣·초상 비교 자체 확인, 의상 장식 미세 차이는 Root 판단.
남은 결함: 이번 HOLD 지적은 보정함. 최종 승인 전이며 수동 두개부 ROI와 크기 인상 재검수 필요. Assets/Library 쓰기·Unity·커밋·파일 삭제 없음.
