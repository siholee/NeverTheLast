# THOR
상태: 재검수 대기
보정일: 2026-11-05 · 근거: Logs/SDPoseV10_20261105/root_review_pm.md
근거: themeId117, 60_enemies.yaml codes/reworkKit + 20_codes.yaml (N/U=13060, P=23060).
N 적 단일·접촉·망치 Electro 90%; U 적 전체·접촉 플래그·Electro 90% 및 치유감소25% 2턴; P 별도 효과 없음.
SP/소환수: 별도 SP 및 실제 소환 개체 없음.
HOLD 보정: IDLE 체격을 다른 포즈에 맞춰 재생성하고 기존 나머지4포즈를 전신1.25배 확대. IDLE alpha높이531→844px.
System.Drawing 후처리는 전신 균등 배율과 LockBits 정수 행 복사 정렬만 적용. 후처리에서 국소 리사이즈/비균등 변형 없음.
추가 생성: 내장 image_gen·투명 배경·시각 묘사만. 원문 _work_THOR/fix_20261105/*_prompt.txt.
추가 재생성(IDLE/WINDUP/STRIKE/HIT/DOWN): 1/0/0/0/0; 기존 회차 2/0/1/0/1. 이번 포즈당2회 이내.
원본 5장 보존. 이전 registered/시트/보고서는 fix_20261105/before에 보존. 보정본은 registered에만 작성.
PNG 실측 5장: IDLE/WINDUP/STRIKE/HIT/DOWN 모두1024×1280,8bit RGBA,모서리alpha0,x30..994,발(alpha≥129최하단)y1220.
보이는 머리 수동ROI(px): IDLE=244×254,√248.9(0.0%); WINDUP=258×239,√248.3(-0.3%); STRIKE=269×230,√248.7(-0.1%); HIT=270×229,√248.7(-0.1%); DOWN=251×246,√248.5(-0.2%)
ROI는 투구/가면 셸 또는 노출 머리(주 수염 포함), 후드·왕관·긴 머리/땋은머리 제외. 좌표·배율·실측은 fix_20261105/measurements_fix.json.
시트: poses/silhouette/face/battle250 및 head_roi 갱신. head_roi는 포즈마다 동일1:1 배율, battle250은 IDLE높이250px 공통배율.
공통 비교: _work_THOR/fix_20261105/nord_common250.png. 광전사250·에인하르273.9·토르314.9px; 토르 alpha면적428471px, 최대 일반병 대비1.22배.
좌향·키트 행동 유지. VFX/글자/배경 추가 없음. 굵은 흰 외곽 미관찰, alpha제거 후처리 미실시.
남은 차이: 왕관/망치/사자 장식 세부 및 HIT 어깨 사자 위치 차이는 남음. 장신 후드인 볼바와 전체높이는 비슷하지만 몸 폭/불투명 면적은 큼.
정지 화면 자체 검수만 수행. 최종 Root 승인/Unity·모션 검수는 남음. PASS 대상·원본 해시 보존 확인, Assets/Library 쓰기·Unity 실행·커밋·삭제 없음.
