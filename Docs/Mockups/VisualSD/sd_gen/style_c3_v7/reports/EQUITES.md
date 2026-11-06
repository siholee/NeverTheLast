# 에퀴테스 (EQUITES / 11033)
상태: **검수 대기** · 2026-11-05 머리 크기 보정
키트: roster.json + 20_codes.yaml + RestoredEnemyCodes.cs 대조. 현행 편성·수치 변경 없음.
N 일반행동: 적 1명 / 비접촉 / 주문.
U 쌍기창: 적 2명 / 비접촉 / 주문.
P 기병의 회피: 별도 능동 행동 없음 / 대상·접촉 해당 없음 (passiveMode=None).
포즈 해석: 가슴 앞에 손을 모아 준비한 뒤 왼쪽으로 손바닥을 펴는 주문.
콘셉트·추가 소품: 원화에 말이 없어 말을 추가하지 않음. 불꽃을 제거하고 내부의 무광 붉은 장식만 유지.
표시: 5포즈 모두 눈 완전 차폐. 익명 병과의 중복·유사 형태 허용.
프롬프트: 시각 특징만 사용, C3 남색선·저채도 회화 명암·투명 배경·좌향·VFX/글자 금지. 실제 전문은 prompts_final.json.
재생성 횟수(최초 제외): IDLE 0, WINDUP 0, STRIKE 0, HIT 0, DOWN 0.
발 y(alpha≥129): IDLE 1220, WINDUP 1220, STRIKE 1220, HIT 1220, DOWN 1220.
머리 √(w×h) px / IDLE 대비 / 원본 배율: IDLE 260.93px(0%) x0.77201, WINDUP 260.93px(0%) x0.88771, STRIKE 260.93px(0%) x0.88771, HIT 260.93px(0%) x0.94631, DOWN 260.93px(0%) x0.89996
측정 기준(2026-11-05): 투구 셸~턱·괴수 주둥이~뒤통수·주 코어 직접 재확인. head_rois.json 및 _v78_fix_head_roi 시트. bare head는 귀·장발·장식 제외.
등록: registered/ 크기 보정 사본 → Resources/Sprite/SD, sd.facing:left. PNG 8bit RGBA·모서리0·가로30..994 확인.
남은 검수: 작은 장식의 포즈별 차이, Unity 임포트·실제 선택 렌더러·Play/릴리스·Root 최종 미술 승인. 자체 검사로 대체하지 않음.
