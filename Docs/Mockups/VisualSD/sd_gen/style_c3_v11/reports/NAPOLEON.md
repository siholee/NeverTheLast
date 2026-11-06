# NAPOLEON · SD 11차 F3

- 수정일: 2026-11-05 · 상태: **검수 대기** · 사용자 요청: 5포즈 모두 돌격소총으로 교체, 사격 STRIKE.
- 반영: 5장 재제작. 낮게 든 IDLE→견착 준비→수평 사격, 피격 보유, DOWN 무릎 위 소총.
- 유지: 보존 포즈 없음. 등록 IDLE의 얼굴·안경·호흡기·후드·청색 장식 고정.
- 키트 N161: 적 단일 지정·비접촉 지휘·자신 피해 0.
- 키트 SP161: 지정 적에게 공격형 아군의 비접촉/접촉 고유 일반행동 전달을 지휘, 자신 제외.
- 키트 U161: 아군 전체 일반 피해 강화·행동 게이지 전진, 비공격 지원.
- 키트 P361: 자신 선행·아군 일반 피해 오라.
- 키트 차이: 사용자 v11 요청이 최우선이라 사격 포즈 제작. 현행 직접 피해 없는 지휘 키트와 시각 차이 명시; 코드 변경 없음.
- 내장 image_gen 프롬프트 요약: 검정 머리·안경·방독 호흡기·검정/코발트 후드 고정, 개머리판·상자 탄창·손잡이 있는 동일 돌격소총. 실제 전문: ../_work_NAPOLEON/prompts.json
- 생성 5장(각 1회), 자체 재생성 0회. transparent_background=true, 등록 IDLE 참조.
- 후처리: PowerShell System.Drawing 균등 축소 → LockBits 정수 이동. 배율: IDLE 0.90116, WINDUP 0.90116, STRIKE 0.88431, HIT 0.89543, DOWN 0.80006
- 발 y(IDLE/WINDUP/STRIKE/HIT/DOWN): 1220/1220/1220/1220/1220; 1024×1280 8bit RGBA, 모서리 α0, x30..994.
- 머리 ROI 폭×높이(장발/장식/모자/후드 제외): IDLE 351×329, WINDUP 351×329, STRIKE 358×323, HIT 354×327, DOWN 336×344
- √(w×h), IDLE 대비: IDLE 339.8 (0%), WINDUP 339.8 (0%), STRIKE 340.0 (0.07%), HIT 340.2 (0.12%), DOWN 340.0 (0.05%)
- 머리 ROI는 시각 지정값이며 ../_work_NAPOLEON/head_roi.png 및 measurements.json에 좌표 보존.
- 생성 알파 보존. 흰 외곽 제거 시험은 정상 머리 경계를 깎아 미채택; fringe_trial_rejected_2x.png에 보존.
- 시트: poses / silhouette / face / battle250 / before_after. 우향·실루엣·얼굴·250px 자체 확인; 최종 승인 아님.
- 남은 결함/확인: 현행 지휘 키트와 사격 시각의 차이는 Root 확인 필요. 소총 세부 슬롯/탄창 원근도 확인 대상. Unity 실행/등록/커밋/파일 삭제 없음.
