# NICOLE SD 6차 · 2026-10-31 · 상태: **검수 대기**
| 키트 | 대상·접촉·종류 / 포즈 대응 |
| --- | --- |
| N168 | 생존 적 무작위6회·공50%/타격·번개 부착·독립 치명/명중, 다중·일반·비접촉·주문 → 회중시계형 장치 조정/작동 |
| 특수 / 추가 | 별도 행동 없음. 바운스6타를 추가행동이나6개의 독립 포즈로 표현하지 않음 |
| U168 | 마나100·적 전체·공120%·번개 부착/방어-20%(대상 일반2회), 초월·비접촉·주문 → 동일 장치 작동 문법 |
| P368 | 아군 전체 경감/증폭 횟수 부여·누적, 상시 사건 반응이며 별도 공격·접촉·소환 동작 없음 |
근거: nicol.md·20_codes.yaml·NicolAlternatingMotor.cs·NicolThunderGrid.cs. 소환수 없음. 장치는 원화 회중시계의 시각적 해석이며 런타임 장비 추가 아님.
포즈: IDLE=허리 옆 장치 대기 / WINDUP=가슴 앞 테두리 조정 / STRIKE=양손으로 오른쪽 장치 작동 / HIT=좌후방 반동·눈 감음·머리 보호 / DOWN=다리를 오른쪽으로 접고 주저앉음. 사격·타격·맨손 지시 주문 금지.
생성: 내장 image_gen, transparent_background=true 전6회. 재생성(최초 제외) IDLE1/WINDUP0/STRIKE0/HIT0/DOWN0; 첫 IDLE의 긴 몸통·다리를 교정한02를 확정 후 나머지4장 모두 그 IDLE만 참조.
실제 프롬프트 요약: “ONE static full-body SD, transparent RGBA1024x1280, mature2.5heads, DARK NAVY ink, soft painterly shadows, RIGHT gaze”; 원화 단발·측면 땋음·보라 고글/라펠·기어·검정 작업복·벨트 공구·회중시계 정체성 고정. 이름·작품·신화/실존 인물명 미사용.
포즈별 실제 지시: LOW waist controller → turn rim at chest → two hands activate ONE controller outside torso RIGHT → closed-eye LEFT recoil/shield temple → closed-eye slumped side-sit. 전문6개·입력·채택 사유는 [generation_ledger.json](../_work_NICOLE/generation_ledger.json)과 같은 폴더의 *_prompt.txt.
후처리: PowerShell System.Drawing 전체 균등 리샘플+정수 이동. 경계6px 이내 밝은 무채색(minRGB≥220·채널차≤18)만 제거(183/111/233/169/135px), 머리/몸 부분 변형 없음. STRIKE 머리끝1px 추가 정리는 final_edge_fix.json.
기술 실측: 전5장1024×1280·PNG8bit/type6 RGBA·네 모서리alpha0. alpha≥129 최하단 y=IDLE1220/WINDUP1220/STRIKE1220/HIT1220/DOWN1220. alpha1..128 미세 잔여 최하단은1222/1224/1270/1277/1272.
머리 실측(수동 두개부 투영 ROI·폭×높이→√값·IDLE 대비): IDLE437×454→445.42(0%) / WINDUP452×438→444.94(-0.106%) / STRIKE447×444→445.50(+0.018%) / HIT455×436→445.40(-0.005%) / DOWN438×452→444.94(-0.106%). 고글·흩날린 머리끝·땋음 제외, 가려진 두개 경계는 추정. ROI 표시본·원본 좌표·배율은 _work_NICOLE/cranial_roi_review.png·roi_config.json·metrics.json.
자체 검수:5열 컬러/단색 및 IDLE 높이 약250px 동일 배율에서5포즈 구분, 우향(반응은 감은 눈), 팔2·손2·장치1·벨트 렌치 유지, VFX·글자·배경·바닥 그림자 없음. 초상화/새 IDLE 비교 직접 확인. 가시적 흰 머리 외곽 번짐 미발견; 최종 밝은 경계 검출 잔여 STRIKE147/HIT38은 소매·피부 재질로 확인해 보존(나머지0).
남은 결함: 기어 톱니·버튼·체인 링 세부 수와 굴곡 편차, DOWN의 장치손/지지손 전환. 두개부 ROI 추정 오차는 독립 확정 못함. HIT 상단 여백88px. 전체 미술 승인·게임 검증으로 확대하지 않음.
산출: NICOLE_SD_*.png5장·sheets/NICOLE_poses.png(기준선/250px)·NICOLE_silhouette.png·NICOLE_face.png. 검증·해시·경계 표시본·작업 원본은 [작업 폴더](../_work_NICOLE/validation_summary.json)에 보존.
범위: NICOLE 전용 산출물/작업 파일만 작성. Assets/·Library/·다른 캐릭터·구버전 수정, Unity 실행, 커밋·스테이지·파일 삭제 없음. 입력 원화2장·5차 overview SHA-256 불변. 작업에서 시작한 상주 프로세스 없음.
