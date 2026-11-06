# QUETZALCOATL
상태: 검수 대기
수정일: 2026-11-05
근거: 60_enemies.yaml codes/reworkKit + 20_codes.yaml N/U/P (N/U=13201, P=23201).
N: 적 단일 / 비접촉 / 주문(공격력90%). U: 적 전체 / 비접촉 / 주문(85%, 풀 부착). P: 동료당 피해 +3%, 최대12%.
SP: 별도 없음. 소환수: 현재 키트에 실제 소환/900번대 개체 없음.
해석·기존 SD 구분: 실제 키는 Allies 원화의 인간형. 흰 상의·연녹 바지·양갈래 유지, 감은 눈을 반개안으로 변경. 양손의 안쪽/바깥쪽 회전으로 주문.
생성: 내장 image_gen, 투명 배경, IDLE 확정 후 같은 IDLE 참조로 4포즈. 전부 좌향.
실제 프롬프트 요약: 시각 특징만: 연녹 양갈래·흰 상의·연녹 바지·운동화. 머리 위 양손 준비→양손 비접촉 주문, 반동·앉은 붕괴. 고유 인물·게임·작품·신화명 미사용; 원문은 _work_QUETZALCOATL/*_prompt.txt.
재생성 추가 횟수(IDLE/WINDUP/STRIKE/HIT/DOWN): 1 / 1 / 0 / 0 / 0 (각 최대2회).
정규화: 생성 원본은 _work에 보존. 캔버스 초기 균등 축소 후 발 맞춤은 LockBits 정수 행 복사만 사용.
원본·registered 발 y: IDLE=1220 / WINDUP=1220 / STRIKE=1220 / HIT=1220 / DOWN=1220. 1024×1280,8bit RGBA,모서리alpha0,x30..994 확인.
보이는 머리 ROI 원본: IDLE=440×430, √=435.0(0.0%); WINDUP=440×430, √=435.0(0.0%); STRIKE=420×424, √=422.0(-3.0%); HIT=438×428, √=433.0(-0.5%); DOWN=455×410, √=431.9(-0.7%)
보이는 머리 ROI registered: IDLE=427×417, √=422.0(0.0%); WINDUP=427×417, √=422.0(0.0%); STRIKE=420×424, √=422.0(0.0%); HIT=427×417, √=422.0(0.0%); DOWN=445×401, √=422.4(+0.1%)
원본 크기 편차는 보존하고 registered 사본만 균등 축소 보정. ROI 좌표·배율은 _work_QUETZALCOATL/measurements.json, 표시 시트는 sheets/QUETZALCOATL_head_roi.png.
시트: poses/silhouette/face/battle250 모두 registered 기준. battle250은 IDLE alpha 몸체 전체 높이250px 공통 배율.
흰 외곽: 확대·회색 배경에서 굵은 스티커 번짐 미관찰, 조건부 알파 제거 미실시. 얼굴·의상 내부 픽셀 별도 수정 없음.
남은 결함: WINDUP을 머리 위 양손 준비로 재제작해 실루엣 분리. 원화보다 머리색이 회황색으로 읽히는 편이며, 긴 양갈래의 곡선·끝말림이 증가함.
검수 범위: PNG·정지 시트 자체 점검. 최종 승인·등록·Unity 실제 Play 검수는 미실시.
