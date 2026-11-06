# ASTOLFO · 아스톨포 · SD v11 F1
수정일: 2026-11-05 · 상태: **검수 대기**
사용자 요청 반영: 깃창을 수평으로 내리고 양손 간격을 벌려 전방으로 찌름. 앞 무릎을 굽힌 런지와 몸통 회전으로 기존 깃발 들기보다 타격감 강화.
N162: 적 전체 / 비접촉 / 무피해 도발·피해 감소·행동 가속.
P362: 자신 / 비접촉 / 타겟 우선도 감소·비약/제세동기 보존.
U162: 아군 전체 / 비접촉 / 추가행동·반격 치피 강화·치유. SP 없음.
키트 차이/해석: 사용자 최우선 요청에 따라 물리 찌르기 그림 채택. 실제 N은 무피해 지원이며 키트·피해 판정은 변경하지 않음.
생성: 내장 image_gen, transparent_background=true, 등록본 IDLE을 외형 마스터로 사용.
실제 프롬프트 요약: ivory-haired adult knight, cyan streak/braid/eyes, silver-gold armor, white scarf, blue cape; two-handed horizontal flag-lance thrust right, bent front knee; compact cape and shorter shaft for uncropped margins.
재생성: STRIKE 1회(후보2); 첫 후보의 망토 좌측 잘림으로 교체. 총 생성2회.
유지 포즈: 등록본을 _work_ASTOLFO/registered_*.png에 원본 복사. 최종 5장에는 전체 등방 크기 보정만 적용(아스톨포 유지 4장은 원본 그대로).
변환: I/W/S/H/D 배율 1.000000 / 1.000000 / 0.978678 / 1.000000 / 1.000000; 배율 처리 뒤 발 정렬은 LockBits 정수 행 복사, 발 정렬 재샘플링 없음.
머리 √(폭×높이) px 및 IDLE 대비: I 376.95(+0.00%) / W 368.79(-2.17%) / S 376.92(-0.01%) / H 376.77(-0.05%) / D 377.09(+0.04%)
측정: 귀·장발·베일·장식 제외, 투구는 보이는 셸~턱. 수동 ROI이므로 정밀 해부학 확정 아님; _work_ASTOLFO/metrics.json·head_roi_final.png 참조.
발 y(alpha≥129): I/W/S/H/D=1220/1220/1220/1220/1220. 5장 1024×1280 RGBA·모서리 alpha0·x30..994 확인.
후처리: 생성 포즈에 경계 안쪽6px·휘도>185·채도<0.18 조건으로만 alpha0 처리, 제거 픽셀 I/W/S/H/D=0/0/4417/0/0; sheets/ASTOLFO_fringe_2x.png에 비교.
시트: sheets/ASTOLFO_{poses,silhouette,face,battle250,before_after}.png. battle250은 IDLE 두개부 상단~발250px의 공통 배율이며 포즈별 확대 없음.
남은 결함: 여백 확보로 깃창 자루가 IDLE보다 짧아짐; 전방 팔의 완전 신전은 다소 약함. 밝은 머리 경계 2배 확대에서 미세한 점각 남음.
인계: 정지 시트 자체 확인만 수행. Assets 등록·Unity 실행·게임 검증·커밋·파일 삭제 없음. Root 독립 실측·최종 승인 대기.
