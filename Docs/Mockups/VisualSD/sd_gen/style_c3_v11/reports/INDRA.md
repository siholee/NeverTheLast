# INDRA · SD 11차 F3

- 수정일: 2026-11-05 · 상태: **검수 대기** · 사용자 요청: 하체의 다리 길이·관절·체중 지지·발 접지 교정.
- 반영: IDLE 양발 분리, WINDUP 안정된 벌림, STRIKE 짧은 지지 자세, HIT 무릎 완충으로 재제작.
- 유지: DOWN은 등록본 바이트 그대로 복사.
- 키트 N63: 적 단일·비접촉 주문 → 바즈라를 들어 준비하고 우측 전방으로 방출.
- 키트 SP63: 적 전체·비접촉 주문 → 공용 방출 포즈.
- 키트 U63: 로카팔라 아군 묶음 개방·비공격 → 동일 SD가 쓰이는 현행 구조.
- 키트 P263: 자신 마나 준비·아군 특수 피해 오라, 별도 접촉 동작 없음.
- 키트 차이: 일반행동 기준 비접촉 주문; 바즈라를 실제 적과 접촉시키지 않음.
- 내장 image_gen 프롬프트 요약: 은백색 머리·작은 왕관·보라/검정/금색 로브 고정, 짧고 정상 연결된 다리, 우향 주문. 실제 전문: ../_work_INDRA/prompts.json
- 생성 4장(각 1회), 자체 재생성 0회. transparent_background=true, 등록 IDLE 참조.
- 후처리: PowerShell System.Drawing 균등 축소 → LockBits 정수 이동. 배율: IDLE 0.87905, WINDUP 0.86640, STRIKE 0.88921, HIT 0.87266, DOWN 1
- 발 y(IDLE/WINDUP/STRIKE/HIT/DOWN): 1220/1220/1220/1220/1220; 1024×1280 8bit RGBA, 모서리 α0, x30..994.
- 머리 ROI 폭×높이(장발/장식/모자/후드 제외): IDLE 387×299, WINDUP 381×303, STRIKE 382×302, HIT 384×301, DOWN 340×340
- √(w×h), IDLE 대비: IDLE 340.2 (0%), WINDUP 339.8 (-0.12%), STRIKE 339.7 (-0.15%), HIT 340.0 (-0.06%), DOWN 340 (-0.05%)
- 머리 ROI는 시각 지정값이며 ../_work_INDRA/head_roi.png 및 measurements.json에 좌표 보존.
- 생성 알파 보존. 흰 외곽 제거 시험은 정상 머리 경계를 깎아 미채택; fringe_trial_rejected_2x.png에 보존.
- 시트: poses / silhouette / face / battle250 / before_after. 우향·실루엣·얼굴·250px 자체 확인; 최종 승인 아님.
- 남은 결함/확인: DOWN의 닫힌 눈은 등록본 유지. 금색 의복 문양의 세부 차이는 Root 확인 필요. Unity 실행/등록/커밋/파일 삭제 없음.
