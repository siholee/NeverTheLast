# ABYSS_HUNTER
상태: **검수 대기** · 2026-11-05 · 묶음 e · 최종 승인 아님
근거: 60_enemies.yaml codes/reworkKit + 20_codes.yaml; N/U 11142 / P21142; YAML theme121·tier normal, Elite 원화
N: 적 단일·접촉·물기/추격·ATK90%.
U: 적 전체·접촉·돌진·ATK85%+불 부착.
P: 전투 시작 DEF+25%·DEF150% 보호막; 제어 시 해제. SP/별도 소환수 없음.
해석/원화 차이: 지시서 사격보다 YAML contact:true 우선. 원화 키는 발주대로, theme121을122로 수정하지 않음.
실제 프롬프트 요약: 이름 없이 백골/먹색/붉은 갑주 상어; 큰 머리·짧은 지느러미, 꼬리 감기→낮은 물기. 좌향, 남색 먹선+회화 명암, transparent background, 무배경/무그림자/무VFX/무글자.
재생성 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 0/1/0/0/1; 내장 image_gen. 최초 생성 제외, 각 포즈 최대2회 이내.
후처리: 1122×1402 생성 원본 보존→머리 ROI 기준 전신 균등 배율로 캔버스 규격화→밝고 무채색인 1px 알파 경계 흰 번짐만 제거→LockBits 정수 이동. 발 정렬 재샘플링 없음.
실측: 최종 1024×1280·8bit RGBA·모서리alpha0·alpha≥129 x30..994. 머리는 수동 지정 보이는 덩어리 ROI 외접 w×h; rawROI/배율/이동/SHA256은 작업 폴더 measurements.json, 경계는 head_roi 시트 공개.
- IDLE: 발 y=1220; 머리 475×273; √=360.104px; IDLE 0.000%; x=110..913
- WINDUP: 발 y=1220; 머리 487×266; √=359.919px; IDLE -0.051%; x=162..861
- STRIKE: 발 y=1220; 머리 464×279; √=359.8px; IDLE -0.084%; x=128..894
- HIT: 발 y=1220; 머리 426×304; √=359.867px; IDLE -0.066%; x=172..852
- DOWN: 발 y=1220; 머리 527×246; √=360.058px; IDLE -0.013%; x=56..967
자체 시각 확인/남은 항목: 턱 개방·머리 기울기에 따른 가로/세로 인상 차이. 상악/하악을 포함한 보이는 머리 ROI를 Root가 다시 확인할 것. 수치±3% 이내를 육안 PASS로 확대하지 않음.
산출: 원본5장 + sheets/{KEY}_poses·silhouette·face·battle250.png, 추가 head_roi.png. battle250은 IDLE alpha높이250px를 기준으로 5장에 같은 배율. 흰 번짐 전후2배 비교/생성 원본은 _work_{KEY}/ 보존. Unity 등록/실행 검수 미실시.
