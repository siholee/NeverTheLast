# YAMA · 야마 · SD v11 F1
수정일: 2026-11-05 · 상태: **검수 대기**
사용자 요청 반영: 지시하는 빈손을 없애고 두 손으로 낫 자루를 잡음. 몸통·골반을 돌려 낮은 오른쪽 전방까지 낫날이 도달한 베기 종료 자세.
N61: 단일 적 / 비접촉 / 주문·번개 부착·지속피해 즉시 발동.
P261: 감전 적 / 비접촉 / 받는 피해 증가 선고.
U61: 적 전체 / 비접촉 / 주문·번개·지속피해 즉시 발동. SP61: 적 전체 / 비접촉 / 주문.
키트 차이/해석: 사용자 최우선 요청에 따라 실제로 크게 베는 그림 채택. N/U/SP의 비접촉 주문 판정은 변경하지 않음.
생성: 내장 image_gen, transparent_background=true, 등록본 IDLE을 외형 마스터로 사용.
실제 프롬프트 요약: charcoal straight-haired adult woman, lavender eyes, black-ivory-violet gold-embroidered robes; both hands grip straight scythe shaft, full twisting cut finishing low right; slender crescent purple-jewel scythe, no trails.
재생성: STRIKE 2회(후보3, 한도 도달); 후보1 머리 작음, 후보2 머리 과대. 총 생성3회.
유지 포즈: 등록본을 _work_YAMA/registered_*.png에 원본 복사. 최종 5장에는 전체 등방 크기 보정만 적용(아스톨포 유지 4장은 원본 그대로).
변환: I/W/S/H/D 배율 0.909949 / 0.923234 / 0.950411 / 0.916335 / 0.903695; 배율 처리 뒤 발 정렬은 LockBits 정수 행 복사, 발 정렬 재샘플링 없음.
머리 √(폭×높이) px 및 IDLE 대비: I 323.07(+0.00%) / W 322.97(-0.03%) / S 322.89(-0.06%) / H 322.96(-0.04%) / D 322.97(-0.03%)
측정: 귀·장발·베일·장식 제외, 투구는 보이는 셸~턱. 수동 ROI이므로 정밀 해부학 확정 아님; _work_YAMA/metrics.json·head_roi_final.png 참조.
발 y(alpha≥129): I/W/S/H/D=1220/1220/1220/1220/1220. 5장 1024×1280 RGBA·모서리 alpha0·x30..994 확인.
후처리: 생성 포즈에 경계 안쪽6px·휘도>185·채도<0.18 조건으로만 alpha0 처리, 제거 픽셀 I/W/S/H/D=0/0/2380/0/0; sheets/YAMA_fringe_2x.png에 비교.
시트: sheets/YAMA_{poses,silhouette,face,battle250,before_after}.png. battle250은 IDLE 두개부 상단~발250px의 공통 배율이며 포즈별 확대 없음.
남은 결함: 낫날이 등록본보다 넓고 자루가 짧아진 차이, 머리카락 끝의 곡률이 남음. 무기 동일성·큰 베기의 강도는 Root 최종 검수 필요.
인계: 정지 시트 자체 확인만 수행. Assets 등록·Unity 실행·게임 검증·커밋·파일 삭제 없음. Root 독립 실측·최종 승인 대기.
