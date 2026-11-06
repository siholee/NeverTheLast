# THESEUS · SD 11차 F3

- 수정일: 2026-11-05 · 상태: **검수 대기** · 사용자 요청: 흐림·확대 흔적 해결, STRIKE 몸통 회전 강화.
- 반영: 5장 모두 1122×1402 native 생성. STRIKE 앞 어깨 전진/반대 팔 후퇴/허리 비틀림/뒷발 회전 추가.
- 유지: 보존 포즈 없음. 모든 최종 자산은 생성 원본에서 축소만 적용(확대 0회).
- 키트 N21: 적 단일·비접촉 베기 → 짧은 검 준비/우측 회전 베기.
- 키트 SP 없음. 파장은 P221의 비접촉 광역 주문 후속이며 별도 행동 아님.
- 키트 U21: 적 단일·비접촉 베기, 자기 치유 및 파장 준비.
- 키트 P221: 초월 후 일반 본타 뒤 적 전체 비접촉 주문 파장.
- 키트 차이: 접촉 돌진 대신 비접촉 검격 정점. 파장 VFX를 이미지에 넣지 않음.
- 내장 image_gen 프롬프트 요약: 갈색 머리·금테 안경·크림/군청/금색 복식 고정, native 선명도와 어깨·허리 회전 강조. 실제 전문: ../_work_THESEUS/prompts.json
- 생성 5장(각 1회), 자체 재생성 0회. transparent_background=true, 등록 IDLE 참조.
- 후처리: PowerShell System.Drawing 균등 축소 → LockBits 정수 이동. 배율: IDLE 0.95982, WINDUP 0.95982, STRIKE 0.94141, HIT 0.83187, DOWN 0.91409
- 발 y(IDLE/WINDUP/STRIKE/HIT/DOWN): 1220/1220/1220/1220/1220; 1024×1280 8bit RGBA, 모서리 α0, x30..994.
- 머리 ROI 폭×높이(장발/장식/모자/후드 제외): IDLE 365×326, WINDUP 365×326, STRIKE 372×320, HIT 358×333, DOWN 352×338
- √(w×h), IDLE 대비: IDLE 344.9 (0%), WINDUP 344.9 (0%), STRIKE 345.0 (0.02%), HIT 345.3 (0.09%), DOWN 344.9 (-0.01%)
- 머리 ROI는 시각 지정값이며 ../_work_THESEUS/head_roi.png 및 measurements.json에 좌표 보존.
- 생성 알파 보존. 흰 외곽 제거 시험은 정상 머리 경계를 깎아 미채택; fringe_trial_rejected_2x.png에 보존.
- 시트: poses / silhouette / face / battle250 / before_after. 우향·실루엣·얼굴·250px 자체 확인; 최종 승인 아님.
- 남은 결함/확인: 큰 흐림·확대 흔적은 시트에서 미관찰. 새 자수·금속 장식 세부 차이는 Root 확인 필요. Unity 실행/등록/커밋/파일 삭제 없음.
