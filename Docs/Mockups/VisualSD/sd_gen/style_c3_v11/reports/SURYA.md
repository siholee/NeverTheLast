# SURYA · SD 11차 F3

- 수정일: 2026-11-05 · 상태: **검수 대기** · 사용자 요청: IDLE을 포함한 5포즈 전반의 어색한 하체 교정.
- 반영: 5장 모두 재제작. IDLE 수직 지지, 준비/사격 양발 접지, HIT 무릎 굽힘, DOWN 두 다리 연결 정리.
- 유지: 보존 포즈 없음; 등록 IDLE의 얼굴·의상·색·리볼버를 참조.
- 키트 N67: 적 단일·비접촉 사격 → 한 손 리볼버 준비/수평 사격.
- 키트 SP67: 적 전체·비접촉 사격 → 공용 사격 포즈.
- 키트 U67: 적 전체 표식·자신 강화, 무피해 비접촉 지원.
- 키트 P267: 대상별 종언 표식 축적, 별도 동작 없음.
- 키트 차이: 일반 사격 기준이며 총구 화염·탄환·표식 VFX를 이미지에 넣지 않음.
- 내장 image_gen 프롬프트 요약: 갈색 피부·흰 단발·크림 모자·붉은 금색 복식 고정, 정상 무릎/발목과 안정된 리볼버 사격. 실제 전문: ../_work_SURYA/prompts.json
- 생성 5장(각 1회), 자체 재생성 0회. transparent_background=true, 등록 IDLE 참조.
- 후처리: PowerShell System.Drawing 균등 축소 → LockBits 정수 이동. 배율: IDLE 0.94423, WINDUP 0.94924, STRIKE 0.93385, HIT 0.93756, DOWN 0.90122
- 발 y(IDLE/WINDUP/STRIKE/HIT/DOWN): 1220/1220/1220/1220/1220; 1024×1280 8bit RGBA, 모서리 α0, x30..994.
- 머리 ROI 폭×높이(장발/장식/모자/후드 제외): IDLE 449×330, WINDUP 446×332, STRIKE 444×327, HIT 445×333, DOWN 451×329
- √(w×h), IDLE 대비: IDLE 384.9 (0%), WINDUP 384.8 (-0.03%), STRIKE 381.0 (-1.01%), HIT 384.9 (0.01%), DOWN 385.2 (0.07%)
- 머리 ROI는 시각 지정값이며 ../_work_SURYA/head_roi.png 및 measurements.json에 좌표 보존.
- 생성 알파 보존. 흰 외곽 제거 시험은 정상 머리 경계를 깎아 미채택; fringe_trial_rejected_2x.png에 보존.
- 시트: poses / silhouette / face / battle250 / before_after. 우향·실루엣·얼굴·250px 자체 확인; 최종 승인 아님.
- 남은 결함/확인: 의상 자수·리볼버 장식의 미세 차이와 HIT 무릎의 자연스러움은 Root 확인 필요. Unity 실행/등록/커밋/파일 삭제 없음.
