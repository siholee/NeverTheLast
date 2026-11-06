# VOID_SCOUT
상태: **검수 대기** · 2026-11-05 · 묶음 e · 최종 승인 아님
근거: 60_enemies.yaml codes/reworkKit + 20_codes.yaml; N/U 11143 / P21143; theme122; Normal 원화
N: 적 단일·비접촉·렌즈 광선·ATK90%.
U: 적 단일·비접촉·포격·ATK165%+번개 부착.
P: 추가 효과 없음. SP/별도 소환수 없음.
해석/원화 차이: 별빛 노심을 좌향 타원 렌즈로 압축; 부유 결정/광환 생략.
실제 프롬프트 요약: 이름 없이 삼각 종형 갑주·굵은 리본 촉수; 치켜듦→후방 모음으로 발사, 구부림/납작 붕괴. 좌향, 남색 먹선+회화 명암, transparent background, 무배경/무그림자/무VFX/무글자.
재생성 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 0/0/0/0/0; 내장 image_gen. 최초 생성 제외, 각 포즈 최대2회 이내.
후처리: 1122×1402 생성 원본 보존→머리 ROI 기준 전신 균등 배율로 캔버스 규격화→밝고 무채색인 1px 알파 경계 흰 번짐만 제거→LockBits 정수 이동. 발 정렬 재샘플링 없음.
실측: 최종 1024×1280·8bit RGBA·모서리alpha0·alpha≥129 x30..994. 머리는 수동 지정 보이는 덩어리 ROI 외접 w×h; rawROI/배율/이동/SHA256은 작업 폴더 measurements.json, 경계는 head_roi 시트 공개.
- IDLE: 발 y=1220; 머리 777×463; √=599.792px; IDLE 0.000%; x=120..903
- WINDUP: 발 y=1220; 머리 766×470; √=600.017px; IDLE +0.037%; x=130..894
- STRIKE: 발 y=1220; 머리 698×516; √=600.14px; IDLE +0.058%; x=86..936
- HIT: 발 y=1220; 머리 685×525; √=599.687px; IDLE -0.018%; x=130..894
- DOWN: 발 y=1220; 머리 832×433; √=600.213px; IDLE +0.070%; x=97..926
자체 시각 확인/남은 항목: IDLE/WINDUP은 촉수 간극 차이가 작음. 종형 셸과 렌즈의 머리 경계는 Root 재확인 필요. 수치±3% 이내를 육안 PASS로 확대하지 않음.
산출: 원본5장 + sheets/{KEY}_poses·silhouette·face·battle250.png, 추가 head_roi.png. battle250은 IDLE alpha높이250px를 기준으로 5장에 같은 배율. 흰 번짐 전후2배 비교/생성 원본은 _work_{KEY}/ 보존. Unity 등록/실행 검수 미실시.
