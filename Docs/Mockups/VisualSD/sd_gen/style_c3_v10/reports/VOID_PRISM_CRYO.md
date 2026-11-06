# 공허의 프리즘 (얼음) (VOID_PRISM_CRYO / 11068)
상태: **검수 대기** · 2026-11-05
근거: SD_Spec 전체·v10·v9 머리 측정·60_enemies.yaml·20_codes.yaml. 실제 항목: _work 폴더 kit_source.md.
N: 공허 광선 / 적 1명 / 비접촉 주문 90%
U: 공허 포격 / 적 1명 / 비접촉 주문 165%·얼음 부착
P: 프리즘 동조 / 추가 효과 없음; SP·실제 소환수 없음
포즈 해석: 기존 코어 덮개 접기·좌측 전개 주문을 승계. 원화의 얼음 팔레트에 맞춰 적색 코어/조각만 청색 계열로 변환.
변종 판정: 원화 일반/CRYO 형태 동일. Assets 등록 SD를 읽어 PowerShell System.Drawing 색상만 변경; 생성 없음.
재채색: 적색→청색, 원 휘도·금/아이보리/먹선 유지. 5장 알파 차이 합 0픽셀; 형태·발·머리·선 굵기 불변.
프롬프트/재생성: 해당 없음(색상 변환). 원본 해시·변경 픽셀: recolor_metrics.json.
머리 ROI: v8 등록본 measurements_registered 승계. 알파 동일 확인 및 표시 ROI 직접 대조.
보정: 루트 PNG는 생성 규격본 보존. §5 registered/ 사본에 발 중심 균등 배율·흰 경계 정리; sheets는 registered/ 기준.
최종 머리 √(w×h) px / IDLE 대비: IDLE 328.7(0.00%), WINDUP 328.7(0.00%), STRIKE 328.7(0.00%), HIT 328.7(0.00%), DOWN 328.7(0.00%)
발 y(alpha≥129): IDLE 1220, WINDUP 1220, STRIKE 1220, HIT 1220, DOWN 1220.
규격: 5장 1024×1280 RGBA8·모서리 alpha 0·가로 30..994·좌향. 수치/배율/ROI/해시: measurements_final.json.
후처리: 색만 변환, alpha·리샘플링·윤곽 정리 없음.
시트: poses·silhouette·face·battle250(IDLE 실제 높이 250px, 5장 동일 배율) + head_roi. 머리/실루엣/초상 직접 확인.
남은 검수: 기존 STRIKE/HIT 외곽이 다소 비슷함. 원화의 발광은 추가하지 않아 청색 코어가 어두움. 기존 형태 보존 규칙에 따라 재생성하지 않음.
