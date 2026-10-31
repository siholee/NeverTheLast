# EBISU SD 6차 · 2026-10-31 · 상태: **검수 대기**
근거: SD_Pose_v6·v3(초월자 톤 제외)·v4·v5, report_v7 실제 C3 프롬프트, v5 THOTH 보고서·전체 시트, ebisu.md 및 20_codes.yaml의 N163/P363/U163. 소환수 없음.
| 키트 | 대상·접촉·종류 | 포즈 대응/금지 |
| --- | --- | --- |
| 일반 N163 | 단일 적·비접촉 주문, 자기 최대HP6% 피해 후 번개 부착 시도 | 낚싯대 캐스팅 준비→전방 캐스팅 정점; 접촉 타격·찌르기·사격·손가락 지시 금지 |
| 특수 / 추가 | 독립 행동 없음 / 없음 | 전용 공격·반격 동작을 창작하지 않음 |
| 특성 P363 | 다른 아군의 경감 뒤 적 피해65% 분담; 직접 피격 대상의 가하는 피해 중첩 | 수동 효과이며 별도 시전·반격·대상 교체로 묘사하지 않음; HIT은 직접 피격 반응 |
| 초월 U163 | 자신 최대HP50% 치유→생존 아군 전원 정화·비접촉 지원 | 공용 행동 포즈는 빈도가 높은 N163 우선; 광역 공격·타인 치유·부활 연출 없음 |
포즈: IDLE=허리 높이 양손 릴 대기 / WINDUP=양손 유지·대를 좌상향으로 당김 / STRIKE=대를 낮춰 우전방으로 캐스팅 / HIT=감은 눈·왼쪽 반동·한 손 열림·한 발 들림 / DOWN=무릎 꿇고 주저앉음·감은 눈·무릎 위 처진 손·낮춘 대.
실제 공통 프롬프트 요약: “ONE static full-body SD, transparent RGBA 1024x1280, adult-impression woman, about2.5heads, uniform dark NAVY ink, soft PAINTERLY volume, RIGHT-facing”; 보라 반묶음/아이보리 리본·남색/아이보리 나선 겉옷·버건디 치마·주머니1·황동 릴 낚싯대1·손잡이 무광 연보라 포인트. 게임·작품·신화/실존 인물·캐릭터 이름 없이 시각 특징만 전달.
실제 포즈 프롬프트 요약: “wind back a short fishing cast / forward fishing CAST, noncontact / backward LEFT recoil, exactly TWO hands / defeated seated low on heels”; 배경·그림자·글자·VFX·낚싯줄/바늘·흰 외곽선 금지. IDLE 먼저 채택 후 나머지 전부 같은 IDLE 참조. 전문·입력 역할은 [generation_ledger.json](../_work_EBISU/generation_ledger.json).
내장 image_gen 총10회, 전부 transparent_background=true. 재생성(최초 제외): IDLE2 / WINDUP0 / STRIKE2 / HIT1 / DOWN0. 채택 원본03/01/03/02/01. IDLE 몸 길이, STRIKE 작은 머리·좌측 잘림, HIT 손3개 보정; 포즈당 최대2회 준수.
최종5장 실측: 1024×1280·8bit RGBA(PNG color type6), 네 모서리alpha0; alpha≥129 최하단 y=IDLE1220/WINDUP1220/STRIKE1220/HIT1220/DOWN1220. 5열 컬러·단색·초상화 vs IDLE 시트 작성.
머리 실측(수동 두개부 ROI 폭×높이→√면적, IDLE 대비): IDLE402×323→360.34(0%) / WINDUP407×318→359.76(-0.16%) / STRIKE412×314→359.68(-0.18%) / HIT414×313→359.97(-0.10%) / DOWN372×348→359.80(-0.15%). 투영/가려짐을 포함한 수동 ROI 추정이며 해부학적 크기의 독립 확정은 아님.
후처리: PowerShell System.Drawing 전체 균등 리샘플만 적용 후 정수 평행이동, 부분 변형·회전 없음. 머리 ROI 안팎6px 범위의 alpha0 경계에서 거리≤6px·RGB최솟값≥218·RGB차≤16인 밝은 무채색만 제거(71/52/109/44/74px). 눈·얼굴/의상 내부 채색은 유지; 최종 육안상 머리 바깥 흰 테두리 미발견.
자체 검수: 동일 배율 약250px 컬러/단색에서5포즈 구분, 전원 우향(반응은 눈 감음), 최종 팔/손2개·대/릴1개·주머니1개, 원화 얼굴·리본·의상, 5차 수준 명암/문양 밀도 확인. 배경·VFX·읽히는 문자 미발견. 접촉 공격·궁수·손짓 주문 실루엣과 구분.
남은 결함: IDLE 두개부 높이 기준 약2.7등신으로 목표2.5보다 김. 나선 문양·릴·가이드 링 수/간격이 포즈마다 다르고 DOWN 주머니가 반대쪽으로 이동함. 낮은alpha1..128 미세 잔여는 발선 아래 y1223~1279까지 존재. STRIKE의 낮은 대는 축소 실루엣만 보면 조준처럼 읽힐 여지가 있어 PM 판단 필요. 전체 PASS로 보고하지 않음.
근거 보존: [_work_EBISU](../_work_EBISU/)의 모든 후보·전문·ROI 표시본·export_config.json·metrics.json·validation_summary.json. 파일 삭제·Assets/Library 쓰기·Unity 실행·커밋·다른 캐릭터/구버전 수정 없음.
