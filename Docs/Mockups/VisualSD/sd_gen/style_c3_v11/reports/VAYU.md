# VAYU · SD 11차 F3

- 수정일: 2026-11-05 · 상태: **검수 대기** · 사용자 요청: 5장이 같은 크기로 보이도록 머리·몸통·전체 인상을 IDLE에 맞춤.
- 반영: IDLE 유지, 나머지 4장 재제작. 긴 런지 대신 짧은 다리·일정한 몸통, 머리 균등 축소 보정.
- 유지: IDLE은 등록본 바이트 그대로 복사. battle250은 5장 모두 IDLE 기준 공통 배율.
- 키트 N64: 적 단일·비접촉 주문 → 오브를 몸 앞에서 준비한 뒤 우측 위로 쓸어 올림.
- 키트 SP64: 적 전체·비접촉 주문/취약 → 공용 방출 포즈.
- 키트 U64: 적 전체·비접촉 주문/방어 감소 → 공용 방출 포즈.
- 키트 P264: 확산된 원소 취약, 별도 접촉 동작 없음.
- 키트 차이: 부유/발광 VFX 없이 실물 오브 소품과 팔 동작으로 주문 표현.
- 내장 image_gen 프롬프트 요약: 흰 머리·갈색 피부·청록 눈·헤드폰·흰/청록 복식 고정, 동일 두개부·몸통·짧은 다리 강조. 실제 전문: ../_work_VAYU/prompts.json
- 생성 4장(각 1회), 자체 재생성 0회. transparent_background=true, 등록 IDLE 참조.
- 후처리: PowerShell System.Drawing 균등 축소 → LockBits 정수 이동. 배율: IDLE 1, WINDUP 0.92968, STRIKE 0.91215, HIT 0.89908, DOWN 0.87583
- 발 y(IDLE/WINDUP/STRIKE/HIT/DOWN): 1220/1220/1220/1220/1220; 1024×1280 8bit RGBA, 모서리 α0, x30..994.
- 머리 ROI 폭×높이(장발/장식/모자/후드 제외): IDLE 370×340, WINDUP 381×330, STRIKE 383×328, HIT 373×337, DOWN 359×350
- √(w×h), IDLE 대비: IDLE 354.7 (0%), WINDUP 354.6 (-0.03%), STRIKE 354.4 (-0.07%), HIT 354.5 (-0.04%), DOWN 354.5 (-0.06%)
- 머리 ROI는 시각 지정값이며 ../_work_VAYU/head_roi.png 및 measurements.json에 좌표 보존.
- 생성 알파 보존. 흰 외곽 제거 시험은 정상 머리 경계를 깎아 미채택; fringe_trial_rejected_2x.png에 보존.
- 시트: poses / silhouette / face / battle250 / before_after. 우향·실루엣·얼굴·250px 자체 확인; 최종 승인 아님.
- 남은 결함/확인: WINDUP은 절제된 준비 동작이라 IDLE과의 차이가 작음(양손·오브 위치로 구분). Root 확인 필요. Unity 실행/등록/커밋/파일 삭제 없음.
