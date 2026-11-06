# AZTEC_SERPENT_PRIEST
상태: 검수 대기
수정일: 2026-11-05
근거: 60_enemies.yaml codes/reworkKit + 20_codes.yaml N/U/P (N/U=11042, P=21042).
N: 아군 단일 / 비접촉 / 피해 +15%(일반행동 2회), 다른 아군 행동 20% 전진. U: 최저 체력비 아군 / 비접촉 / 시전자 최대 HP 5.5% 치유. P: 동료당 피해 +3%, 최대12%.
SP: 별도 없음. 소환수: 현재 키트에 실제 소환/900번대 개체 없음.
해석·기존 SD 구분: 희생의 창이라는 이름과 달리 공격하지 않음. 뱀 지팡이와 펼친 손으로 축복; 기존 합장·부채·서판과 구분.
생성: 내장 image_gen, 투명 배경, IDLE 확정 후 같은 IDLE 참조로 4포즈. 전부 좌향.
실제 프롬프트 요약: 시각 특징만: 뱀 투구·예복·뱀 지팡이. 준비→열린 손 축복, 뒤 반동, 무릎 붕괴. 공격·VFX 금지. 고유 인물·게임·작품·신화명 미사용; 원문은 _work_AZTEC_SERPENT_PRIEST/*_prompt.txt.
재생성 추가 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 1 / 0 / 0 / 0 / 0 (각 최대2회).
정규화: 생성 원본은 _work에 보존. 캔버스 초기 균등 축소 후 발 맞춤은 LockBits 정수 행 복사만 사용.
원본·registered 발 y: IDLE=1220 / WINDUP=1220 / STRIKE=1220 / HIT=1220 / DOWN=1220. 1024×1280,8bit RGBA,모서리alpha0,x30..994 확인.
보이는 머리 ROI 원본: IDLE=368×360, √=364.0(0.0%); WINDUP=357×368, √=362.5(-0.4%); STRIKE=358×379, √=368.4(+1.2%); HIT=393×373, √=382.9(+5.2%); DOWN=400×355, √=376.8(+3.5%)
보이는 머리 ROI registered: IDLE=366×358, √=362.0(0.0%); WINDUP=357×368, √=362.5(+0.1%); STRIKE=352×373, √=362.3(+0.1%); HIT=372×353, √=362.4(+0.1%); DOWN=385×341, √=362.3(+0.1%)
원본 크기 편차는 보존하고 registered 사본만 균등 축소 보정. ROI 좌표·배율은 _work_AZTEC_SERPENT_PRIEST/measurements.json, 표시 시트는 sheets/AZTEC_SERPENT_PRIEST_head_roi.png.
시트: poses/silhouette/face/battle250 모두 registered 기준. battle250은 IDLE alpha 몸체 전체 높이250px 공통 배율.
흰 외곽: 확대·회색 배경에서 굵은 스티커 번짐 미관찰, 조건부 알파 제거 미실시. 얼굴·의상 내부 픽셀 별도 수정 없음.
남은 결함: 지팡이를 잡는 손·좌우 위치가 포즈별로 바뀜. 금장·예복의 기하문양 문자 오인 가능성 검수 필요. 일반행동은 공격 아님.
검수 범위: PNG·정지 시트 자체 점검. 최종 승인·등록·Unity 실제 Play 검수는 미실시.
