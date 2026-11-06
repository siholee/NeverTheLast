# VAYU · SD 13차 p1 HOLD1 보정

- 상태: 검수 대기 · 2026-11-06
- 키트: N64 적 단일·비접촉·주문/바람 부착. U64 적 전체·비접촉·주문/방어력 감소; P264 확산 대상 원소 취약; SP64 적 전체·비접촉·주문/7원소 취약.
- 기준: Root HOLD 결과 + 새 IDLE 비율 참조 + 해당 포즈 참조. 등록본·원화·키트는 읽기만 함.
- 수정 전 p1 IDLE: crownY=302, chinY=636, feetY=1220; 2.7485등신.
- 목표 2.69(합격 2.66~2.72, 운영 상한 2.74): 몸 유지 가정 머리 배율 (전−1)/1.69=1.0346. 생성 결과를 재측정해 선택.
- 수정 후 IDLE: crownY=234, chinY=599, feetY=1220; (1220−234)/(599−234)=2.701370등신. 목표 범위 충족.
- 보정: 머리 상대 크기를 조금 늘리는 방향. STRIKE의 긴 몸을 2회 생성으로 줄여 새 IDLE의 튜닉·바지·신발 체격에 맞춤. 팔·보주 각도·헤드폰·얼굴 유지.
- 이번 회차 생성/선택(IDLE/WINDUP/STRIKE/HIT/DOWN): 1/1/2/1/1 / 1/1/2/1/1; 재생성 0/0/1/0/0. 포즈당 최대 3회 이내.
- 생성: 내장 image_gen, transparent background. 외형 묘사 프롬프트 전문 hold1_*_prompt.txt. IDLE 확정 후 동작 4장의 비율 참조.
- 후처리: PowerShell System.Drawing 전체 균등 배율 + LockBits 정수 이동만. 5장 발 y1220, 1024×1280 PNG 8bit RGBA, 모서리 alpha0, alpha≥129 x30..994 안.
- 머리 √(w·h) IDLE 397.55px; 최대 편차 0.090%. 수동 경계 오차 ±10px 별도; final_measure.csv·measure_VAYU.png 근거.
- 직접 확인: 키트/우향·원화 외형/5포즈 실루엣/금지 요소/화풍/무기 동작/250px 체격/IDLE 등신. poses·silhouette·face·battle250·before_after·measure·lineup 확인.
- 남은 검토: 보주 프레임·자수 세부 미세차. STRIKE 몸 길이는 250px 시트로 Root 재검수 대기.
- 보존: 이전 5장·시트·measure·report·작업 파일은 _work_KEY/_superseded_hold1/. 삭제 없음. SEI·SHI·ISHTAR 관련 134개 해시 불변.
- 산출: 루트 5 PNG·sheets 5종·measure·lineup_p1.png 갱신. _work_KEY/hold1_before_after.png는 이전 HOLD와 이번 보정 추가 비교.
- 제한: 최종 Root 검수 대기. Assets·Library 쓰기, Unity 실행, 커밋 없음.
