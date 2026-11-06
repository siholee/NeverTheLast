# EQUITES · 11033 · SD 12차 H3
상태: **검수 대기** · 2026-11-05 · Root HOLD 지적 STRIKE 재작업본
사용자 추가 요청: 낮게 내린 STRIKE를 테세우스 등록 STRIKE처럼 골반·어깨 회전으로 가슴~어깨 높이에서 왼쪽으로 크게 베는 순간으로 교체.
반영: STRIKE의 검과 손을 어깨 높이로 올린 수평 베기, 좌측 앞무릎 디딤·골반/어깨 회전·뒷발 밀기·오른쪽 망토 전개. 검 디자인·투구·색 유지. IDLE/WINDUP/HIT/DOWN은 변경 없음.
키트: N11033: 적 단일 / 비접촉 / 주문 90%.
SP 없음 / U11033: 적 2명 / 비접촉 / 주문 각 110%.
P21033: 별도 능동 효과 없음.
콘셉트·키트: 사용자 무기 변경 지시를 우선해 검 연출을 채택. 현행 비접촉 주문·쌍기창 명칭과 차이 있음(데이터 수정 없음).
근거: 60_enemies.yaml의 실제 원화 키·reworkKit 및 20_codes.yaml. 적별 Characters 문서 없음. 등록 IDLE·원화·테세우스/바스테트 등록본 읽기 대조.
생성: 내장 image_gen·투명 배경. 이번 Root HOLD 후 재작업 1회/최대 2회, 추가 재생성 0회. 등록 IDLE·이번 IDLE·등록 공격 동작 참조, 이름 없는 외형 프롬프트. 이전 제작 이력은 보존.
실제 프롬프트·선택 이력: ../_work_EQUITES/root_fix_prompt.json 및 prompts_final.json. 이전 HOLD PNG·시트·보고서는 _superseded/root_fix_before/ 보존.
발 y(alpha≥129): IDLE 1220, WINDUP 1220, STRIKE 1220, HIT 1220, DOWN 1220.
머리 √(w×h), 새 IDLE 대비: IDLE 260.99px(0.00%), WINDUP 260.92px(-0.03%), STRIKE 261.07px(+0.03%), HIT 260.89px(-0.04%), DOWN 260.89px(-0.04%).
측정: 투구 셸~턱(볏·가리개 끝 제외). 새 STRIKE ROI [383,533,278,245], 균등 배율 0.925640 후 정수 y -22. 정확한 값은 measurements.json·root_fix_1_measurements.json·head_roi 시트.
규격 수치 확인: 5장 1024×1280·8bit RGBA·모서리 alpha 0·가로 30..994·머리 ±3%. 좌향·실루엣·원화 대조 시트 육안 확인.
후처리: System.Drawing 균등 크기 정규화 후 LockBits 행 복사로 정수 이동. 경계 안쪽 6px·휘도>185·채도<0.18만 alpha 0; 내부 보존, edge_cleanup_2x 시트 첨부.
시트 갱신: poses·silhouette·face·battle250·before_after·head_roi·edge_cleanup_2x, 추가 root_fix_before_after. battle250은 전 포즈 동일 IDLE 기준 배율.
남은 결함/판정: 갑옷 리벳·망토 테두리의 미세 문양 차이 및 기존 비접촉 키트와 검 연출 차이. 이번 높은 베기의 공격 읽힘은 Root 재검수 대기, 자체 최종 PASS 미부여.
Assets/Library 쓰기·Unity 실행·커밋·파일 삭제 없음. 실제 게임 모션·표시·최종 미술 승인 미실행; 이 산출물은 등록 전 제작본.
비례 참고: IDLE 2.73, HIT 3.01(기울어진 투구 ROI 영향 포함). HIT 등신 경계는 Root 정수리·턱 재측정 필요.
