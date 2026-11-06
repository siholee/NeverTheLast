# SCYLLA
상태: **재검수 대기** · 2026-11-05 · 묶음 e · Root HOLD: HIT 크기 인상 보정
근거: 60_enemies.yaml codes/reworkKit + 20_codes.yaml; N/U 13090 / P23090; theme122; Boss 원화
N: 적 전체·비접촉·여왕의 광선·ATK45%.
U: 적 전체·비접촉·광역 주문·ATK85%+물 부착.
P: 동료1명당 가하는 피해+3%, 최대12%. SP/별도 소환수 없음.
해석/원화 차이: 다두·짧은 해수 꼬리로 압축, 검정 하이넥으로 갑주 노출 보완; 광선/VFX 자체는 그리지 않음.
실제 프롬프트 요약: 이름 없이 큰 은발 중앙 머리·짧고 굵은 뱀 목·청백 갑주; 치켜들기→좌향 동시 조준/양손 투사. 좌향, 남색 먹선+회화 명암, transparent background, 무배경/무그림자/무VFX/무글자.
재생성 횟수: 최초 제작 1/0/0/0/0; 이번 추가 0/0/0/1/0(내장 image_gen, transparent). 전신 축소만으로는 머리 ±3%와 전체 크기를 동시에 맞출 수 없어 HIT만 편집.
보정: HIT 상체·꼬리를 압축하는 편집1회→1122×1402 결과 전신 균등0.92배→발 중심 보존·LockBits 정수 이동(-48,-11). 다른4포즈는 원본과 SHA256 일치하는 사본, 원본5장 보존.
실측: registered/ 5장 모두 1024×1280·8bit RGBA·모서리alpha0·alpha≥129 y1220·x30..994. 기존과 같은 수동 머리 ROI 외접 √(w×h); 보정 기록은 _work_SCYLLA/fix_review/registered_measurements.json, 경계는 head_roi 시트.
- IDLE: 발 y=1220; 머리 344×316; √=329.703px; IDLE 0.000%; x=168..856
- WINDUP: 발 y=1220; 머리 371×293; √=329.701px; IDLE 0.000%; x=138..884
- STRIKE: 발 y=1220; 머리 364×300; √=330.454px; IDLE +0.228%; x=145..877
- HIT: 발 y=1220; 머리 359×294; √=324.878px; IDLE −1.463%; x=128..810
- DOWN: 발 y=1220; 머리 324×336; √=329.945px; IDLE +0.074%; x=105..919
자체 시각 확인: IDLE 전체높이835px/HIT 기존1028px→840px; IDLE250px 공통 배율에서 HIT 약251.5px. 250px 비교에서 과대한 인상을 줄이고 좌향·피격 반동 유지. 보조두 가시 개수8/8/7/6/6과 세부 장식 차이는 남아 있으며 Root 최종 재검수 대기.
산출: registered/SCYLLA_SD_*.png 5장 + 기존 sheets/SCYLLA_poses·silhouette·face·battle250.png 갱신, head_roi 보조 시트 갱신. 변경 전 시트/보고서·추가 생성/프롬프트·배율 후보 비교는 _work_SCYLLA/fix_review/ 보존. Unity 등록/실행 미실시.
