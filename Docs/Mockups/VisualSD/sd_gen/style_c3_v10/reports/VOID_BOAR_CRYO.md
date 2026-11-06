# 공허의 멧돼지 (얼음) (VOID_BOAR_CRYO / 11084)
상태: **검수 대기** · 2026-11-05
근거: SD_Spec 전체·v10·v9 머리 측정·60_enemies.yaml·20_codes.yaml. 실제 항목: _work 폴더 kit_source.md.
N: 멧돼지의 엄니 / 적 1명 / 접촉 타격 90%
U: 광란의 일격 / 적 1명 / 접촉 타격 165%·얼음 부착
P: 우격다짐 / 별도 추가 효과 없음; SP·실제 소환수 없음
포즈 해석: 기존 머리 낮추기 → 좌측 엄니 돌진을 승계. 적색 결정/판갑 틈을 얼음 청색으로 재채색.
변종 판정: 원화 일반/CRYO 형태 동일. Assets 등록 SD를 읽어 PowerShell System.Drawing 색상만 변경; 생성 없음.
재채색: 적색→청색, 원 휘도·금/아이보리/먹선 유지. 5장 알파 차이 합 0픽셀; 형태·발·머리·선 굵기 불변.
프롬프트/재생성: 해당 없음(색상 변환). 원본 해시·변경 픽셀: recolor_metrics.json.
머리 ROI: v8 등록본 measurements_registered 승계. 알파 동일 확인 및 표시 ROI 직접 대조.
보정: 루트 PNG는 생성 규격본 보존. §5 registered/ 사본에 발 중심 균등 배율·흰 경계 정리; sheets는 registered/ 기준.
최종 머리 √(w×h) px / IDLE 대비: IDLE 285.7(0.00%), WINDUP 285.7(0.00%), STRIKE 285.7(0.00%), HIT 285.7(0.00%), DOWN 292.3(+2.32%)
발 y(alpha≥129): IDLE 1220, WINDUP 1220, STRIKE 1220, HIT 1220, DOWN 1220.
규격: 5장 1024×1280 RGBA8·모서리 alpha 0·가로 30..994·좌향. 수치/배율/ROI/해시: measurements_final.json.
후처리: 색만 변환, alpha·리샘플링·윤곽 정리 없음.
시트: poses·silhouette·face·battle250(IDLE 실제 높이 250px, 5장 동일 배율) + head_roi. 머리/실루엣/초상 직접 확인.
남은 검수: 기존 WINDUP/DOWN 외곽이 유사한 약점 승계. 색상만 변경하라는 규칙에 따라 형태 수정하지 않음. 최종 승인·게임 표시 미검수.
