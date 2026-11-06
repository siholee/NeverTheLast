# BERSERKER
상태: 재검수 대기
보정일: 2026-11-05 · 근거: Logs/SDPoseV10_20261105/root_review_pm.md
근거: themeId117, 60_enemies.yaml codes/reworkKit + 20_codes.yaml (N/U=11120, P=21120).
N 적 단일·접촉·도끼 Pyro 90%; U 적 단일·접촉·4타 총200%; P 잃은 HP에 따른 가속/피해 및 직접 피해 흡혈.
SP/소환수: 별도 SP 및 실제 소환 개체 없음.
HOLD 보정: HIT의 큰 몸/후드 비례를 재생성으로 축소. 다른4포즈는 픽셀 해시 동일. HIT alpha높이807→636px, IDLE670px 대비 약95%(무릎 굽힌 피격 자세).
System.Drawing 후처리는 전신 균등 배율과 LockBits 정수 행 복사 정렬만 적용. 후처리에서 국소 리사이즈/비균등 변형 없음.
추가 생성: 내장 image_gen·투명 배경·시각 묘사만. 원문 _work_BERSERKER/fix_20261105/*_prompt.txt.
추가 재생성(IDLE/WINDUP/STRIKE/HIT/DOWN): 0/0/0/2/0; 기존 회차 1/0/2/0/0. 이번 포즈당2회 이내.
원본 5장 보존. 이전 registered/시트/보고서는 fix_20261105/before에 보존. 보정본은 registered에만 작성.
PNG 실측 5장: IDLE/WINDUP/STRIKE/HIT/DOWN 모두1024×1280,8bit RGBA,모서리alpha0,x30..994,발(alpha≥129최하단)y1220.
보이는 머리 수동ROI(px): IDLE=174×166,√170.0(0.0%); WINDUP=161×179,√169.8(-0.1%); STRIKE=166×174,√170.0(0.0%); HIT=172×168,√170.0(0.0%); DOWN=179×161,√169.8(-0.1%)
ROI는 투구/가면 셸 또는 노출 머리(주 수염 포함), 후드·왕관·긴 머리/땋은머리 제외. 좌표·배율·실측은 fix_20261105/measurements_fix.json.
시트: poses/silhouette/face/battle250 및 head_roi 갱신. head_roi는 포즈마다 동일1:1 배율, battle250은 IDLE높이250px 공통배율.
공통 비교: _work_THOR/fix_20261105/nord_common250.png. 광전사250·에인하르273.9·토르314.9px; 토르 alpha면적428471px, 최대 일반병 대비1.22배.
좌향·키트 행동 유지. VFX/글자/배경 추가 없음. 굵은 흰 외곽 미관찰, alpha제거 후처리 미실시.
남은 차이: 호흡기 홈·도끼 실린더/날·체인 세부 차이는 남음. HIT의 반동/무릎 굽힘에 따른 높이 차이는 유지.
정지 화면 자체 검수만 수행. 최종 Root 승인/Unity·모션 검수는 남음. PASS 대상·원본 해시 보존 확인, Assets/Library 쓰기·Unity 실행·커밋·삭제 없음.
