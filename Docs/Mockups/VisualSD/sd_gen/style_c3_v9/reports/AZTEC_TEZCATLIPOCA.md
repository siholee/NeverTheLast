# AZTEC_TEZCATLIPOCA
상태: 검수 대기
수정일: 2026-11-05
근거: 60_enemies.yaml codes/reworkKit + 20_codes.yaml N/U/P (N/U=13010, P=23010).
N: 아군 단일 / 비접촉 / 피해 +15%(일반행동2회), 다른 아군 행동20% 전진. U: 적 전체 / 비접촉 / 주문(85%, 저주). P: 동료당 피해+3%, 최대12%.
SP: 별도 없음. 소환수: 현재 키트에 실제 소환/900번대 개체 없음.
해석·기존 SD 구분: 일반행동은 공격 없음. 거울 원반 지팡이와 열린 손의 칙령. 원화 연기 하체는 VFX 금지에 따라 불투명 천 자락으로 해석.
생성: 내장 image_gen, 투명 배경, IDLE 확정 후 같은 IDLE 참조로 4포즈. 전부 좌향.
실제 프롬프트 요약: 시각 특징만: 검은 깃·금 장식·해골·원반 지팡이·불투명 천 하체. 비공격 준비→열린 손 칙령, 반동·붕괴. 고유 인물·게임·작품·신화명 미사용; 원문은 _work_AZTEC_TEZCATLIPOCA/*_prompt.txt.
재생성 추가 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 2 / 1 / 1 / 1 / 1 (각 최대2회).
정규화: 생성 원본은 _work에 보존. 캔버스 초기 균등 축소 후 발 맞춤은 LockBits 정수 행 복사만 사용.
원본·registered 발 y: IDLE=1220 / WINDUP=1220 / STRIKE=1220 / HIT=1220 / DOWN=1220. 1024×1280,8bit RGBA,모서리alpha0,x30..994 확인.
보이는 머리 ROI 원본: IDLE=243×235, √=239.0(0.0%); WINDUP=248×240, √=244.0(+2.1%); STRIKE=238×235, √=236.5(-1.0%); HIT=225×231, √=228.0(-4.6%); DOWN=251×232, √=241.3(+1.0%)
보이는 머리 ROI registered: IDLE=232×224, √=228.0(0.0%); WINDUP=232×224, √=228.0(0.0%); STRIKE=229×227, √=228.0(0.0%); HIT=225×231, √=228.0(0.0%); DOWN=237×219, √=227.8(-0.1%)
원본 크기 편차는 보존하고 registered 사본만 균등 축소 보정. ROI 좌표·배율은 _work_AZTEC_TEZCATLIPOCA/measurements.json, 표시 시트는 sheets/AZTEC_TEZCATLIPOCA_head_roi.png.
시트: poses/silhouette/face/battle250 모두 registered 기준. battle250은 IDLE alpha 몸체 전체 높이250px 공통 배율.
흰 외곽: 확대·회색 배경에서 굵은 스티커 번짐 미관찰, 조건부 알파 제거 미실시. 얼굴·의상 내부 픽셀 별도 수정 없음.
남은 결함: HOLD 후보: 실제 보이는 해골 ROI 기준 IDLE 약3.1등신(2.9 이상). IDLE 재생성2회 소진. 정면 성향의 두개골과 문자처럼 보일 수 있는 이마 장식도 Root 검수 필요.
검수 범위: PNG·정지 시트 자체 점검. 최종 승인·등록·Unity 실제 Play 검수는 미실시.
