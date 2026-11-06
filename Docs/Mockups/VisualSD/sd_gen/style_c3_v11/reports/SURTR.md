# SURTR · SD 11차 F3

- 수정일: 2026-11-05 · 상태: **검수 대기** · 사용자 요청: 5포즈 모두 머리카락 곱슬기 감소.
- 반영: 5장 모두 긴 직선형 머리로 재제작. 반복 고리·회오리 앞머리 제거, 대검 준비/내려베기 유지.
- 유지: 보존 포즈 없음. 가로 한계를 넘지 않도록 5장 모두 작은 STRIKE에 맞춰 축소.
- 키트 N81: 적 단일·접촉 베기 → 양손 대검 준비/우측 내려베기.
- 키트 SP 없음.
- 키트 U81: 적 전체·비접촉 베기 및 자기 체력 비용.
- 키트 P281: 실제 직접 피해 뒤 아군 보호막 지원, 별도 공격 동작 없음.
- 키트 차이: 일반행동 기준 대검 동작. 불꽃·거대 낙검 VFX는 이미지에 넣지 않음.
- 내장 image_gen 프롬프트 요약: 붉은 눈·적발·검정/금색 코트 고정, 긴 매끈한 직선 머리, 문자 없는 기하 대검 무늬. 실제 전문: ../_work_SURTR/prompts.json
- 생성 5장(각 1회), 자체 재생성 0회. transparent_background=true, 등록 IDLE 참조.
- 후처리: PowerShell System.Drawing 균등 축소 → LockBits 정수 이동. 배율: IDLE 0.80025, WINDUP 0.79018, STRIKE 0.87679, HIT 0.85455, DOWN 0.83180
- 발 y(IDLE/WINDUP/STRIKE/HIT/DOWN): 1220/1220/1220/1220/1220; 1024×1280 8bit RGBA, 모서리 α0, x30..994.
- 머리 ROI 폭×높이(장발/장식/모자/후드 제외): IDLE 328×312, WINDUP 324×316, STRIKE 324×316, HIT 338×303, DOWN 308×333
- √(w×h), IDLE 대비: IDLE 319.9 (0%), WINDUP 320.0 (0.02%), STRIKE 320.0 (0.02%), HIT 320.0 (0.04%), DOWN 320.3 (0.11%)
- 머리 ROI는 시각 지정값이며 ../_work_SURTR/head_roi.png 및 measurements.json에 좌표 보존.
- 생성 알파 보존. 흰 외곽 제거 시험은 정상 머리 경계를 깎아 미채택; fringe_trial_rejected_2x.png에 보존.
- 시트: poses / silhouette / face / battle250 / before_after. 우향·실루엣·얼굴·250px 자체 확인; 최종 승인 아님.
- 남은 결함/확인: 머리 고리 없음. 검 길이/두께와 머리 끝 흐름의 원근 차이는 Root 확인 필요. Unity 실행/등록/커밋/파일 삭제 없음.
