# ANUBIS · 아누비스 · SD v11 F1
수정일: 2026-11-05 · 상태: **검수 대기**
사용자 요청 반영: 지팡이창의 금색 창끝을 전방으로 더 내밀고 양손·어깨·골반이 함께 밀어내는 깊은 런지. 장식 원은 후방으로 돌려 창끝 방향 명확화.
N121: 단일 적 / 접촉 / 지팡이창 찌르기·자기 회복.
P321: 쓰러진 유닛 / 비접촉 / 영혼 수확·영구 성장.
U121: 자신 / 비접촉 / 일반행동 피해·회복·바위 부착 강화. SP 없음.
키트 차이/해석: N의 접촉 찌르기에 대응. 창끝과 후방 장식은 기존 소품의 양 끝을 유지.
생성: 내장 image_gen, transparent_background=true, 등록본 IDLE을 외형 마스터로 사용.
실제 프롬프트 요약: black-haired adult man, tall black-gold jackal helmet, ivory-lined cape, gold collar/purple gem; fully extended forward two-handed staff-spear thrust right; circular ornament rear, gold point leading, folded cape.
재생성: STRIKE 1회(후보2); 첫 후보의 망토 좌측 접촉/잘림으로 교체. 총 생성2회.
유지 포즈: 등록본을 _work_ANUBIS/registered_*.png에 원본 복사. 최종 5장에는 전체 등방 크기 보정만 적용(아스톨포 유지 4장은 원본 그대로).
변환: I/W/S/H/D 배율 0.918156 / 0.918156 / 0.993058 / 0.941210 / 0.903226; 배율 처리 뒤 발 정렬은 LockBits 정수 행 복사, 발 정렬 재샘플링 없음.
머리 √(폭×높이) px 및 IDLE 대비: I 279.94(+0.00%) / W 279.94(+0.00%) / S 279.95(+0.00%) / H 280.06(+0.04%) / D 280.00(+0.02%)
측정: 귀·장발·베일·장식 제외, 투구는 보이는 셸~턱. 수동 ROI이므로 정밀 해부학 확정 아님; _work_ANUBIS/metrics.json·head_roi_final.png 참조.
발 y(alpha≥129): I/W/S/H/D=1220/1220/1220/1220/1220. 5장 1024×1280 RGBA·모서리 alpha0·x30..994 확인.
후처리: 생성 포즈에 경계 안쪽6px·휘도>185·채도<0.18 조건으로만 alpha0 처리, 제거 픽셀 I/W/S/H/D=0/0/377/0/0; sheets/ANUBIS_fringe_2x.png에 비교.
시트: sheets/ANUBIS_{poses,silhouette,face,battle250,before_after}.png. battle250은 IDLE 두개부 상단~발250px의 공통 배율이며 포즈별 확대 없음.
남은 결함: 자루가 여백·원근 때문에 IDLE보다 짧고, 후방 원 장식과 어깨 장식 세부가 조금 다름. 실제 도달감은 Root 검수 필요.
인계: 정지 시트 자체 확인만 수행. Assets 등록·Unity 실행·게임 검증·커밋·파일 삭제 없음. Root 독립 실측·최종 승인 대기.
