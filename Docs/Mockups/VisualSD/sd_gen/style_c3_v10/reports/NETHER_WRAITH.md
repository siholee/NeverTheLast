# NETHER_WRAITH
상태: **재검수 대기** · 2026-11-05 · Root 재검수 전.
근거: 60_enemies.yaml themeId108 codes·reworkKit + 20_codes.yaml; N/U 11052 · P 21052.
N: 적 단일·접촉·검 공격 ATK90%와 저주 2일반행동(효과명중).
U: 아군 전체·비접촉·가피증15%(2일반행동), 다른 아군 AV15% 당김.
P: 자신·비접촉·전투당1회 치명피해 방지+최대HP35% 회복.
WINDUP→STRIKE: 검 키트에 맞춰 장갑 전완 일체형 짧은 직선 칼날; 부유 망령; 가장 빈번한 N 기준, SD_Spec §6의 기존 무기·소품과 구분.
Root HOLD 보정: WINDUP만 IDLE의 짧은 천 꼬리·왕관 비율로 재제작. 가슴 앞 전완 칼날 준비, 부유형·다리 없음 유지.
프롬프트: 이름 없이 외형·좌향·투명·먹선/회화 명암·무문자/무VFX; 실제 전문 `_work_NETHER_WRAITH/fix1/prompts.json`.
이번 추가 생성 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 0/2/0/0/0; 포즈당 최대2회 준수. 이전 회차 이력은 fix1/_superseded/report.md 보존.
처리: 전신만 균등 배율(발 중심 고정, 가로 여백을 위한 정수 x이동) → 생성본의 경계6px/휘도>185/채도<0.18만 제거 → LockBits 정수 y이동; 두개부만 변형하지 않음.
등록 사본 5장: 1024×1280·8bit RGBA·모서리 α0·좌향·α≥129 최하단 y1220; x범위 IDLE:169..854 / WINDUP:152..834 / STRIKE:84..938 / HIT:85..938 / DOWN:117..906.
IDLE: 보이는 두개부 ROI(후드/장식 제외) = (323,622,251,248), √(w×h)=249.50px, IDLE 대비 0%, 전신 배율 1, 정수 y이동 0.
WINDUP: 보이는 두개부 ROI(후드/장식 제외) = (324,622,251,248), √(w×h)=249.50px, IDLE 대비 0%, 전신 배율 0.91225, 정수 y이동 337.
STRIKE: 보이는 두개부 ROI(후드/장식 제외) = (328,695,272,229), √(w×h)=249.58px, IDLE 대비 0.03%, 전신 배율 1, 정수 y이동 0.
HIT: 보이는 두개부 ROI(후드/장식 제외) = (418,506,282,221), √(w×h)=249.64px, IDLE 대비 0.06%, 전신 배율 1, 정수 y이동 0.
DOWN: 보이는 두개부 ROI(후드/장식 제외) = (314,799,260,239), √(w×h)=249.28px, IDLE 대비 -0.09%, 전신 배율 1, 정수 y이동 0.
산출: registered/NETHER_WRAITH_SD_*.png; sheets/NETHER_WRAITH_{poses,silhouette,face,battle250}.png(전부 등록 사본 기준·IDLE 높이250 공통 배율).
원본 PNG 불변. 수동 ROI/배율/실측과 경계 전후2배는 `_work_NETHER_WRAITH/fix1/`; 250px·실루엣·초상 비교 자체 확인, 의상 장식 미세 차이는 Root 판단.
남은 결함: 이번 HOLD 지적은 보정함. 최종 승인 전이며 수동 두개부 ROI와 크기 인상 재검수 필요. Assets/Library 쓰기·Unity·커밋·파일 삭제 없음.
