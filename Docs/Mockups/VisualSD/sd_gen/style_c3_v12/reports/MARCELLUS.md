# MARCELLUS · 12004 · SD 12차 H3
상태: **검수 대기** · 2026-11-05
사용자 요청: 5포즈 모두 작은 검을 원화 비율로 확대.
반영: 전 포즈 장검화. IDLE 세운 검 / WINDUP 뒤로 감아 준비 / STRIKE 전진 손잡이 타격 / HIT 하향 검 반동 / DOWN 무릎 위 수평 장검.
키트: N12004: 적 단일 / 접촉 / 타격 90%.
SP 없음 / U12004: 적 단일 / 접촉 / 타격 165%.
P22004: 자신 / 비접촉 / 잃은 체력 기반 속도·피해 강화 및 직접 체력 피해 흡혈.
콘셉트·키트: 장검을 확대한 뒤 일반행동의 근접 타격 해석 유지. 소환수·새 장비 없음.
근거: 60_enemies.yaml의 실제 원화 키·reworkKit 및 20_codes.yaml. 적별 Characters 문서 없음. 등록 IDLE·원화·테세우스/바스테트 등록본 읽기 대조.
생성: 내장 image_gen, 투명 배경, 등록 IDLE 참조, 이름 없는 외형 묘사. 재생성(최초 제외): IDLE 2, WINDUP 0, STRIKE 0, HIT 0, DOWN 1.
실제 프롬프트·선택 이력: ../_work_MARCELLUS/prompts_final.json (의상·얼굴 고정, 좌향, 포즈·무기 동작만 변경).
발 y(alpha≥129): IDLE 1220, WINDUP 1220, STRIKE 1220, HIT 1220, DOWN 1220.
머리 √(w×h), 새 IDLE 대비: IDLE 243.73px(0.00%), WINDUP 243.87px(+0.06%), STRIKE 243.79px(+0.03%), HIT 243.74px(+0.01%), DOWN 243.79px(+0.03%).
측정: 귀·장발·장식 제외 두개부, 투구는 셸~턱(볏·가리개 끝 제외). ROI·배율·정수 이동: ../_work_MARCELLUS/measurements.json 및 head_roi 시트.
규격 수치 확인: 5장 1024×1280·8bit RGBA·모서리 alpha 0·가로 30..994·머리 ±3%. 좌향·실루엣·원화 대조 시트 육안 확인.
후처리: System.Drawing 균등 크기 정규화 후 LockBits 행 복사로 정수 이동. 경계 안쪽 6px·휘도>185·채도<0.18만 alpha 0; 내부 보존, edge_cleanup_2x 시트 첨부.
시트: poses·silhouette·face·battle250·before_after + head_roi·edge_cleanup_2x. battle250은 IDLE 두개부 위~발 250px, 전 포즈 동일 배율.
남은 결함/판정: 검 가드의 얼굴 장식·코트 금색 문양의 미세 차이와 원근상 검 길이 차이. 장검 최종 비례는 Root 대조 필요.
Assets/Library 쓰기·Unity 실행·커밋·파일 삭제 없음. 실제 게임 모션·표시·최종 미술 승인 미실행; 이 산출물은 등록 전 제작본.
남은 비례 결함: IDLE/WINDUP/STRIKE/HIT 약 2.96~3.03(두개부 ROI 높이 참고). 2.9 이상 HOLD 후보; IDLE은 재생성 2회 상한에 도달. Root 정수리·턱 재측정 및 판정 필요.
