# AZTEC_ELITE_EAGLE
상태: 검수 대기
수정일: 2026-11-05
근거: 60_enemies.yaml codes/reworkKit + 20_codes.yaml N/U/P (N/U=12011, P=22011).
N: 적 단일 / 비접촉 / 활 사격(90%). U: 적 단일 / 비접촉 / 왕의 맹독 화살(165%, 맹독). P: 추가 효과 없음.
SP: 별도 없음. 소환수: 현재 키트에 실제 소환/900번대 개체 없음.
해석·기존 SD 구분: 일반형보다 판금·금 장식·큰 활 강화.
생성: 내장 image_gen, 투명 배경, IDLE 확정 후 같은 IDLE 참조로 4포즈. 전부 좌향.
실제 프롬프트 요약: 시각 특징만: 새부리 투구·깃·각진 활. 화살통 준비→좌향 조준, 뒤 반동, 앉은 붕괴. 고유 인물·게임·작품·신화명 미사용; 원문은 _work_AZTEC_ELITE_EAGLE/*_prompt.txt.
재생성 추가 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 0 / 1 / 0 / 0 / 0 (각 최대2회).
정규화: 생성 원본은 _work에 보존. 캔버스 초기 균등 축소 후 발 맞춤은 LockBits 정수 행 복사만 사용.
원본·registered 발 y: IDLE=1220 / WINDUP=1220 / STRIKE=1220 / HIT=1220 / DOWN=1220. 1024×1280,8bit RGBA,모서리alpha0,x30..994 확인.
보이는 머리 ROI 원본: IDLE=416×340, √=376.1(0.0%); WINDUP=405×323, √=361.7(-3.8%); STRIKE=405×300, √=348.6(-7.3%); HIT=370×320, √=344.1(-8.5%); DOWN=385×338, √=360.7(-4.1%)
보이는 머리 ROI registered: IDLE=381×311, √=344.2(0.0%); WINDUP=385×307, √=343.8(-0.1%); STRIKE=400×296, √=344.1(0.0%); HIT=370×320, √=344.1(0.0%); DOWN=367×322, √=343.8(-0.1%)
원본 크기 편차는 보존하고 registered 사본만 균등 축소 보정. ROI 좌표·배율은 _work_AZTEC_ELITE_EAGLE/measurements.json, 표시 시트는 sheets/AZTEC_ELITE_EAGLE_head_roi.png.
시트: poses/silhouette/face/battle250 모두 registered 기준. battle250은 IDLE alpha 몸체 전체 높이250px 공통 배율.
흰 외곽: 확대·회색 배경에서 굵은 스티커 번짐 미관찰, 조건부 알파 제거 미실시. 얼굴·의상 내부 픽셀 별도 수정 없음.
남은 결함: 활·화살·깃 및 판금 장식 세부가 포즈마다 조금 다름. 금장 기하문양의 문자 오인 가능성 검수 필요.
검수 범위: PNG·정지 시트 자체 점검. 최종 승인·등록·Unity 실제 Play 검수는 미실시.
