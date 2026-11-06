# AZTEC_ELITE_JAGUAR
상태: 검수 대기
수정일: 2026-11-05
근거: 60_enemies.yaml codes/reworkKit + 20_codes.yaml N/U/P (N/U=12010, P=22010).
N: 적 단일 / 접촉 / 흑요석 곤봉 타격(90%). U: 적 단일 / 접촉 / 왕의 대지 분쇄(165%, 바위 부착). P: 추가 효과 없음.
SP: 별도 없음. 소환수: 현재 키트에 실제 소환/900번대 개체 없음.
해석·기존 SD 구분: 일반형보다 중갑·망토·어깨 장식 강화.
생성: 내장 image_gen, 투명 배경, IDLE 확정 후 같은 IDLE 참조로 4포즈. 전부 좌향.
실제 프롬프트 요약: 시각 특징만: 고양잇과 투구·녹금 의상·납작한 흑요석 곤봉. 준비→접촉 타격, 뒤 반동, 앉은 붕괴. 고유 인물·게임·작품·신화명 미사용; 원문은 _work_AZTEC_ELITE_JAGUAR/*_prompt.txt.
재생성 추가 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 0 / 0 / 0 / 0 / 0 (각 최대2회).
정규화: 생성 원본은 _work에 보존. 캔버스 초기 균등 축소 후 발 맞춤은 LockBits 정수 행 복사만 사용.
원본·registered 발 y: IDLE=1220 / WINDUP=1220 / STRIKE=1220 / HIT=1220 / DOWN=1220. 1024×1280,8bit RGBA,모서리alpha0,x30..994 확인.
보이는 머리 ROI 원본: IDLE=404×370, √=386.6(0.0%); WINDUP=400×347, √=372.6(-3.6%); STRIKE=354×290, √=320.4(-17.1%); HIT=410×321, √=362.8(-6.2%); DOWN=410×350, √=378.8(-2.0%)
보이는 머리 ROI registered: IDLE=335×307, √=320.7(0.0%); WINDUP=344×298, √=320.2(-0.2%); STRIKE=354×290, √=320.4(-0.1%); HIT=362×284, √=320.6(0.0%); DOWN=347×296, √=320.5(-0.1%)
원본 크기 편차는 보존하고 registered 사본만 균등 축소 보정. ROI 좌표·배율은 _work_AZTEC_ELITE_JAGUAR/measurements.json, 표시 시트는 sheets/AZTEC_ELITE_JAGUAR_head_roi.png.
시트: poses/silhouette/face/battle250 모두 registered 기준. battle250은 IDLE alpha 몸체 전체 높이250px 공통 배율.
흰 외곽: 확대·회색 배경에서 굵은 스티커 번짐 미관찰, 조건부 알파 제거 미실시. 얼굴·의상 내부 픽셀 별도 수정 없음.
남은 결함: 원본 STRIKE 머리 축소가 큼. 보정 사본 사용 필요. 타격 정점 곤봉 끝이 발선에 가까우며, 금장 문양의 문자 오인 가능성 검수 필요.
검수 범위: PNG·정지 시트 자체 점검. 최종 승인·등록·Unity 실제 Play 검수는 미실시.
