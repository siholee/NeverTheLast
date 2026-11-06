# NEITH · p4 · 2026-11-06
상태: **검수 대기**
IDLE 전: crownY 414 / chinY 694 / 2.8786등신. 등록본 픽셀 격자 재확인, 코브라 활 제외.
목표 계산: 고정 높이 806px / 2.68 = 머리 300.75px, 기존 280px 대비 ×1.0741.
판단: 머리를 조금 키우고 몸통·다리를 줄임. DOWN은 전체 균등 배율의 가로 한계 안에서 머리 −2.51%로 조율.
IDLE 후: crownY **414** / chinY **709** / 머리 높이 295px / **2.7322등신**.
N166: 적 단일 / 비접촉 / 사격·전기 부착. 한 무릎 조준→활시위 놓은 손 유지.
U166: 번개 아군 / 비접촉 / 특공 버프.
P366: 감전된 적 / 비접촉 / 특방 감소 촉진. SP 없음.
생성: 내장 image_gen, 각 등록 포즈 편집 대상 + 새 IDLE 비율 참조.
프롬프트: exact pose/outfit/face/cobra-bow; modest larger skull, shorter torso/legs; transparent. 전문은 _work_NEITH/prompts.txt.
시도 IDLE/WINDUP/STRIKE/HIT/DOWN = 1/1/1/1/1, 재생성 0회.
머리 w×h / √면적 / IDLE 대비: IDLE 340×295 / 316.70 / 0%; WINDUP 340×287 / 312.38 / −1.37%.
STRIKE 350×301 / 324.58 / +2.49%; HIT 330×291 / 309.89 / −2.15%; DOWN 297×321 / 308.77 / −2.51%.
후처리: PowerShell System.Drawing 전체 균등 배율 1회 + LockBits 정수 이동. 부위 변형·합성 없음.
5장 모두 1024×1280, PNG 8bit RGBA, 모서리 alpha 0, alpha≥129 최하단 y1220; x 범위 32..992 안.
직접 확인: 5종 시트·lineup·측정선. 두 코브라 활머리·손 위치·무릎·긴 생머리 유지. 비교 시트 칸 확장으로 활 잘림 해소.
남은 결함: 활머리·술장식 세부 미세 차이. DOWN 머리는 숙인 방향의 두개 ROI로 측정, 불확도 약 ±5px.
산출: measure_NEITH.png, sheets/NEITH_*.png, _work_NEITH/measurements.csv·head_measurements.png.
Assets/·Library/ 쓰기, Unity 실행, 커밋, 삭제 없음. Root 최종 검수·등록 전 단계.
