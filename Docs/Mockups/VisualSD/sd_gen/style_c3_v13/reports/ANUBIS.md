# ANUBIS · SD v13 p3 HOLD 보정1 · 2026-11-06
상태: **검수 대기**. Root의 이전2.749 HOLD에 따른 5포즈 재작화; 다른 p3 네 명 PASS 파일은 그대로 보존.
목표: IDLE2.66~2.70(중심2.68). 투구의 긴 귀·지팡이 장식 제외, 보이는 주 투구 셸 윗선~인간 턱 끝 측정.
N121: 적 단일 / 접촉 / 지팡이창 찌르기·자기 회복. P321: 자신 / 비접촉 / 영혼 성장.
U121: 자신 / 비접촉 / 일반행동 강화. SP 없음. 캐릭터 문서·20_codes.yaml 대응 유지.
포즈: 등록된 승인 5포즈 참조. STRIKE 앞발 디딤·전진 런지 유지, 더 깊게 주저앉지 않도록 재작화. 자칼 투구·얼굴·검정/금색 의상 유지.
전→후: 등록본2.9600(crown480/chin730); HOLD2.7491(431/718) → 보정2.6890(crown537/chin791, head254, feet1220).
경계 민감도: 턱791 고정, crown537±8px만 변동하면2.6374~2.7439(기존2.60~2.76 안). 귀 끝을 정수리로 사용하지 않음.
이번 생성 I/W/S/H/D=3/1/1/2/1(총8회, 재생성3회). IDLE1·2 목표 미달, HIT1 투구 귀 누락으로 미선정; 후보 전부 작업 폴더 보존.
내장 image_gen, transparent_background=true. 외형만 묘사한 same pose/outfit/face + main shell-to-chin2.68, shorter body; 실제 프롬프트·모든 참조 경로 ../_work_ANUBIS/prompts_fix1.json.
후처리: PowerShell System.Drawing 전체 균등 배율만 적용, 발 정렬은 LockBits 정수 행 복사. 부위별 변형·합성·알파 삭제 없음.
머리 ROI 폭×높이→√: I331×254→289.96 / W327×257→289.89 / S334×252→290.12 / H347×242→289.78 / D323×260→289.79.
IDLE 대비 편차 I/W/S/H/D=0.00/-0.02/+0.06/-0.06/-0.06%. ROI·변환·수동 경계 근거 ../_work_ANUBIS/{raw_rois,metrics,after_measure}.json.
5/5 규격 실측: 1024×1280 8bit RGBA, 모서리alpha0, alpha≥129 최하단y1220, x30..994, 머리IDLE±3%; ../_work_ANUBIS/validation_fix1.csv.
직접 확인: poses·silhouette·face·battle250·before_after, ../measure_ANUBIS.png와 ../lineup_p3.png. battle250과 전후 시트는 DOWN 지팡이 전체가 보이도록 여백 확대.
STRIKE 높이: 250px IDLE 정수리 정규화에서 가슴 표식102.3→131.0px(+28.7), 허리72.8→96.1px(+23.3), 발 디딤·전진 육안 대조. ../_work_ANUBIS/strike_height_compare.{png,csv}.
남은 검토: 금장 자수·지팡이·장식의 미세 재해석과 투구 셸 수동 경계 오차. HIT 귀 누락 후보는 폐기 선택만 했으며 삭제하지 않음. Root 최종 승인 필요.
이전 결과30개는 ../_work_ANUBIS/_superseded_hold1/로 이동 보존, manifest SHA256 일치. PASS 네 명의129개 파일 해시 일치; 해당 파일 수정 없음.
범위: 정지 PNG·시트 자체 검토만 수행. Assets/Library 쓰기·Unity 실행·등록·게임 QA·커밋·파일 삭제 없음. 보조 PowerShell 완료, 별도 QA Player/서버 실행 없음.
