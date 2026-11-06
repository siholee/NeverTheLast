# SIREN
상태: **검수 대기** · 2026-11-05 · 묶음 e · 최종 승인 아님
근거: 60_enemies.yaml codes/reworkKit + 20_codes.yaml; N/U 11145 / P21145; theme122; Normal 원화
N: 적 단일·비접촉·광선·ATK90%.
U: 적 전체·비접촉·돌림노래·ATK85%.
P: 추가 효과 없음. SP/별도 소환수 없음.
해석/원화 차이: 지시서 노래 단독보다 실제 N 광선 우선; 노래하듯 입을 열고 손바닥 투사. 검정 하이넥으로 갑주 노출 보완.
실제 프롬프트 요약: 이름 없이 은발·푸른 눈·백골 갑주·짧은 물고기 꼬리, 양팔 바깥 V 준비→좌향 양손 투사. 좌향, 남색 먹선+회화 명암, transparent background, 무배경/무그림자/무VFX/무글자.
재생성 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 2/1/0/0/0; 내장 image_gen. 최초 생성 제외, 각 포즈 최대2회 이내.
후처리: 1122×1402 생성 원본 보존→머리 ROI 기준 전신 균등 배율로 캔버스 규격화→밝고 무채색인 1px 알파 경계 흰 번짐만 제거→LockBits 정수 이동. 발 정렬 재샘플링 없음.
실측: 최종 1024×1280·8bit RGBA·모서리alpha0·alpha≥129 x30..994. 머리는 수동 지정 보이는 덩어리 ROI 외접 w×h; rawROI/배율/이동/SHA256은 작업 폴더 measurements.json, 경계는 head_roi 시트 공개.
- IDLE: 발 y=1220; 머리 450×374; √=410.244px; IDLE 0.000%; x=237..785
- WINDUP: 발 y=1220; 머리 449×374; √=409.788px; IDLE -0.111%; x=167..856
- STRIKE: 발 y=1220; 머리 446×377; √=410.051px; IDLE -0.047%; x=122..902
- HIT: 발 y=1220; 머리 469×358; √=409.758px; IDLE -0.118%; x=107..915
- DOWN: 발 y=1220; 머리 421×400; √=410.366px; IDLE +0.030%; x=62..962
자체 시각 확인/남은 항목: WINDUP 실루엣 보완 완료. HIT/DOWN의 두개부 회전·머리카락 볼륨 차이와 등신은 Root 육안 재확인. 수치±3% 이내를 육안 PASS로 확대하지 않음.
산출: 원본5장 + sheets/{KEY}_poses·silhouette·face·battle250.png, 추가 head_roi.png. battle250은 IDLE alpha높이250px를 기준으로 5장에 같은 배율. 흰 번짐 전후2배 비교/생성 원본은 _work_{KEY}/ 보존. Unity 등록/실행 검수 미실시.
