# ERESHKIGAL · SD 13차 p1 HOLD1 보정

- 상태: 검수 대기 · 2026-11-06
- 키트: N167 적 단일·비접촉 주문 해석·최대HP 피해/출혈. U167 아군 전체·비접촉·치유 + 적 전체 출혈; P367 아군 DoT 후 전원 치유; SP 없음.
- 기준: Root HOLD 결과 + 새 IDLE 비율 참조 + 해당 포즈 참조. 등록본·원화·키트는 읽기만 함.
- 수정 전 p1 IDLE: crownY=613, chinY=849, feetY=1220; 2.5720등신.
- 목표 2.69(합격 2.66~2.72, 운영 상한 2.74): 몸 유지 가정 머리 배율 (전−1)/1.69=0.9302. 생성 결과를 재측정해 선택.
- 수정 후 IDLE: crownY=576, chinY=814, feetY=1220; (1220−576)/(814−576)=2.705882등신. 목표 범위 충족.
- 보정: 머리 축소 우선으로 IDLE 생성 후 몸 길이 미세 조정. DOWN의 몸통·소매·다리 체격을 줄여 앉은 실루엣을 낮춤. 등불·단발·얼굴·의상 유지.
- 이번 회차 생성/선택(IDLE/WINDUP/STRIKE/HIT/DOWN): 2/2/1/1/1 / 2/2/1/1/1; 재생성 1/1/0/0/0. 포즈당 최대 3회 이내.
- 생성: 내장 image_gen, transparent background. 외형 묘사 프롬프트 전문 hold1_*_prompt.txt. IDLE 확정 후 동작 4장의 비율 참조.
- 후처리: PowerShell System.Drawing 전체 균등 배율 + LockBits 정수 이동만. 5장 발 y1220, 1024×1280 PNG 8bit RGBA, 모서리 alpha0, alpha≥129 x30..994 안.
- 머리 √(w·h) IDLE 267.65px; 최대 편차 0.127%. 수동 경계 오차 ±10px 별도; final_measure.csv·measure_ERESHKIGAL.png 근거.
- 직접 확인: 키트/우향·원화 외형/5포즈 실루엣/금지 요소/화풍/무기 동작/250px 체격/IDLE 등신. poses·silhouette·face·battle250·before_after·measure·lineup 확인.
- 남은 검토: 금장·주름 세부 미세차. WINDUP의 외곽 변화는 작음(등불 상승·손목 교차 유지).
- 보존: 이전 5장·시트·measure·report·작업 파일은 _work_KEY/_superseded_hold1/. 삭제 없음. SEI·SHI·ISHTAR 관련 134개 해시 불변.
- 산출: 루트 5 PNG·sheets 5종·measure·lineup_p1.png 갱신. _work_KEY/hold1_before_after.png는 이전 HOLD와 이번 보정 추가 비교.
- 제한: 최종 Root 검수 대기. Assets·Library 쓰기, Unity 실행, 커밋 없음.
