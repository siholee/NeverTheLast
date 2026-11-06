# AZTEC_JAGUAR
상태: 검수 대기
수정일: 2026-11-05
근거: 60_enemies.yaml codes/reworkKit + 20_codes.yaml N/U/P (N/U=11040, P=21040).
N: 적 단일 / 접촉 / 흑요석 곤봉 타격(공격력 90%). U: 적 단일 / 접촉 / 대지 분쇄(165%, 바위 부착). P: 추가 효과 없음.
SP: 별도 없음. 소환수: 현재 키트에 실제 소환/900번대 개체 없음.
해석·기존 SD 구분: 원화의 납작한 흑요석 곤봉 유지, 긴 칼·역수 베기와 구분.
생성: 내장 image_gen, 투명 배경, IDLE 확정 후 같은 IDLE 참조로 4포즈. 전부 좌향.
실제 프롬프트 요약: 시각 특징만: 고양잇과 투구·녹금 의상·납작한 흑요석 곤봉. 준비→접촉 타격, 뒤 반동, 앉은 붕괴. 고유 인물·게임·작품·신화명 미사용; 원문은 _work_AZTEC_JAGUAR/*_prompt.txt.
재생성 추가 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 1 / 0 / 2 / 0 / 0 (각 최대2회).
정규화: 생성 원본은 _work에 보존. 캔버스 초기 균등 축소 후 발 맞춤은 LockBits 정수 행 복사만 사용.
원본·registered 발 y: IDLE=1220 / WINDUP=1220 / STRIKE=1220 / HIT=1220 / DOWN=1220. 1024×1280,8bit RGBA,모서리alpha0,x30..994 확인.
보이는 머리 ROI 원본: IDLE=427×352, √=387.7(0.0%); WINDUP=427×360, √=392.1(+1.1%); STRIKE=355×292, √=322.0(-17.0%); HIT=350×296, √=321.9(-17.0%); DOWN=453×408, √=429.9(+10.9%)
보이는 머리 ROI registered: IDLE=355×292, √=322.0(0.0%); WINDUP=351×296, √=322.3(+0.1%); STRIKE=355×292, √=322.0(0.0%); HIT=350×296, √=321.9(0.0%); DOWN=339×305, √=321.6(-0.1%)
원본 크기 편차는 보존하고 registered 사본만 균등 축소 보정. ROI 좌표·배율은 _work_AZTEC_JAGUAR/measurements.json, 표시 시트는 sheets/AZTEC_JAGUAR_head_roi.png.
시트: poses/silhouette/face/battle250 모두 registered 기준. battle250은 IDLE alpha 몸체 전체 높이250px 공통 배율.
흰 외곽: 확대·회색 배경에서 굵은 스티커 번짐 미관찰, 조건부 알파 제거 미실시. 얼굴·의상 내부 픽셀 별도 수정 없음.
남은 결함: 원본 STRIKE/HIT 머리 축소와 DOWN 확대가 큼. 보정 사본 사용 필요. 포즈별 곤봉 길이·이빨 수 및 투구 세부 차이 검수 필요.
검수 범위: PNG·정지 시트 자체 점검. 최종 승인·등록·Unity 실제 Play 검수는 미실시.
