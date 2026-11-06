# SABINA · 12005 · SD 12차 H3
상태: **검수 대기** · 2026-11-05
사용자 요청: 5포즈 거북목 교정, STRIKE는 바스테트처럼 몸통 회전.
반영: 전 포즈 턱을 당기고 머리를 어깨 위로 정렬. WINDUP 투척 날 후인 / STRIKE 골반·어깨 회전과 앞발 디딤으로 좌향 투척. HIT·DOWN도 목 전방 돌출 완화.
키트: N12005: 적 단일 / 비접촉 / 사격·투척 90%.
SP 없음 / U12005: 적 단일 / 비접촉 / 사격·투척 165%.
P22005: 별도 능동 효과 없음.
콘셉트·키트: 몸통 회전은 투척의 힘 전달로 해석해 비접촉 키트 유지. 밧줄·기존 투척 날 유지.
근거: 60_enemies.yaml의 실제 원화 키·reworkKit 및 20_codes.yaml. 적별 Characters 문서 없음. 등록 IDLE·원화·테세우스/바스테트 등록본 읽기 대조.
생성: 내장 image_gen, 투명 배경, 등록 IDLE 참조, 이름 없는 외형 묘사. 재생성(최초 제외): IDLE 0, WINDUP 0, STRIKE 0, HIT 0, DOWN 0.
실제 프롬프트·선택 이력: ../_work_SABINA/prompts_final.json (의상·얼굴 고정, 좌향, 포즈·무기 동작만 변경).
발 y(alpha≥129): IDLE 1220, WINDUP 1220, STRIKE 1220, HIT 1220, DOWN 1220.
머리 √(w×h), 새 IDLE 대비: IDLE 259.32px(0.00%), WINDUP 259.23px(-0.04%), STRIKE 259.35px(+0.01%), HIT 259.26px(-0.02%), DOWN 259.28px(-0.02%).
측정: 귀·장발·장식 제외 두개부, 투구는 셸~턱(볏·가리개 끝 제외). ROI·배율·정수 이동: ../_work_SABINA/measurements.json 및 head_roi 시트.
규격 수치 확인: 5장 1024×1280·8bit RGBA·모서리 alpha 0·가로 30..994·머리 ±3%. 좌향·실루엣·원화 대조 시트 육안 확인.
후처리: System.Drawing 균등 크기 정규화 후 LockBits 행 복사로 정수 이동. 경계 안쪽 6px·휘도>185·채도<0.18만 alpha 0; 내부 보존, edge_cleanup_2x 시트 첨부.
시트: poses·silhouette·face·battle250·before_after + head_roi·edge_cleanup_2x. battle250은 IDLE 두개부 위~발 250px, 전 포즈 동일 배율.
남은 결함/판정: 땋은 머리·허리끈 세부의 포즈별 차이. 목 정렬 및 몸통 회전 강도는 Root 최종 대조 필요.
Assets/Library 쓰기·Unity 실행·커밋·파일 삭제 없음. 실제 게임 모션·표시·최종 미술 승인 미실행; 이 산출물은 등록 전 제작본.
남은 비례 결함: IDLE/WINDUP/STRIKE 약 2.91~2.95(두개부 ROI 높이 참고), 2.9 이상 HOLD 후보. Root 정수리·턱 재측정 및 판정 필요.
