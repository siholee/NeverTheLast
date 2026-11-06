# THRAEX · 11022 · SD 12차 H3
상태: **검수 대기** · 2026-11-05
사용자 요청: 테세우스처럼 몸이 더 회전하도록.
반영: STRIKE만 교체: 앞발 디딤·뒷발 회전, 골반과 어깨를 좌측으로 돌려 곡도 팔을 몸 앞으로 보냄. 방패는 몸 옆 뒤로. IDLE/WINDUP/HIT/DOWN은 등록본 바이트 복사.
키트: N11022: 적 단일 / 접촉 / 타격 90%.
SP 없음 / U11022: 적 단일 / 접촉 / 타격 165%.
P21022: 별도 능동 효과 없음.
콘셉트·키트: 곡도·소형 방패를 유지한 회전 타격. 접촉 키트 유지.
근거: 60_enemies.yaml의 실제 원화 키·reworkKit 및 20_codes.yaml. 적별 Characters 문서 없음. 등록 IDLE·원화·테세우스/바스테트 등록본 읽기 대조.
생성: 내장 image_gen, 투명 배경, 등록 IDLE 참조, 이름 없는 외형 묘사. 재생성(최초 제외): STRIKE 0, 나머지 4포즈 미생성(등록본 복사).
실제 프롬프트·선택 이력: ../_work_THRAEX/prompts_final.json (의상·얼굴 고정, 좌향, 포즈·무기 동작만 변경).
발 y(alpha≥129): IDLE 1220, WINDUP 1220, STRIKE 1220, HIT 1220, DOWN 1220.
머리 √(w×h), 새 IDLE 대비: IDLE 337.85px(0.00%), WINDUP 337.85px(0.00%), STRIKE 337.87px(0.00%), HIT 337.99px(+0.04%), DOWN 337.85px(0.00%).
측정: 귀·장발·장식 제외 두개부, 투구는 셸~턱(볏·가리개 끝 제외). ROI·배율·정수 이동: ../_work_THRAEX/measurements.json 및 head_roi 시트.
규격 수치 확인: 5장 1024×1280·8bit RGBA·모서리 alpha 0·가로 30..994·머리 ±3%. 좌향·실루엣·원화 대조 시트 육안 확인.
후처리: System.Drawing 균등 크기 정규화 후 LockBits 행 복사로 정수 이동. 경계 안쪽 6px·휘도>185·채도<0.18만 alpha 0; 내부 보존, edge_cleanup_2x 시트 첨부.
시트: poses·silhouette·face·battle250·before_after + head_roi·edge_cleanup_2x. battle250은 IDLE 두개부 위~발 250px, 전 포즈 동일 배율.
남은 결함/판정: 새 STRIKE의 허리 얼굴 장식·가죽끈 세부가 등록본과 미세하게 다름. 방패 위치와 회전 강도는 Root 최종 대조 필요.
Assets/Library 쓰기·Unity 실행·커밋·파일 삭제 없음. 실제 게임 모션·표시·최종 미술 승인 미실행; 이 산출물은 등록 전 제작본.
비례 참고: IDLE 2.64, STRIKE 2.72(두개부 ROI 높이 기준). 최종 등신은 정수리·턱 직접 대조 필요.
