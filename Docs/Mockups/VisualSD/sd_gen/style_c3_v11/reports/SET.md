# SET · 세트 · SD v11 F1 Root HOLD 수정
수정일: 2026-11-05 · 상태: **재검수 대기**
Root 요청 반영: 정면 가슴이 보이던 낮은 베기를 폐기하고, 오른손 곡도를 어깨 높이로 뻗으며 어깨 뒤·등·옆구리가 드러나는 큰 회전으로 교체. 허리·허리띠를 비틀고 반대 주먹은 뒤 허리로 회수.
N123: 단일 적 / 접촉 / 곡도 베기·출혈.
P323: 직접 공격자 / 접촉 / 반격 베기; 자신 / 비접촉 / 흡혈.
U123: 적 전체 / 비접촉 / 출혈; 자신 / 비접촉 / 치유·반격 예산 갱신. SP 없음.
키트 해석: 기존 오른손 곡도의 접촉 베기를 유지하며, 잔·바스테트 확정 STRIKE의 어깨 전진·몸통 회전을 참조. 게임 판정 변경 없음.
생성: 내장 image_gen, transparent_background=true. 등록본 IDLE은 외형 마스터, 확정 STRIKE 두 장은 회전 참고로만 사용.
실제 프롬프트 요약: three-quarter back/side torso, tattooed right shoulder driven forward, hidden chest, twisted waist/diagonal belt, right-hand horizontal hooked-blade slash, left fist at rear waist; same wolf pelt/red-black/gold costume, screen right, no VFX.
추가 재생성: 2/2회 사용, STRIKE_fix1_candidate2.png 채택. 후보1의 회전은 좋았으나 망토·곡도 끝이 경계에 닿아 후보2에서 여백 보정. v11 총 생성3회.
유지 포즈: IDLE/WINDUP/HIT/DOWN PNG 4장은 이번 수정 시작 해시와 완전히 동일. 이전 STRIKE·시트·보고서·metrics는 _work_SET/_superseded/fix1_before/에 보존.
STRIKE 변환: 원본1072×1467 → 전체 균등 배율1.002554 → LockBits 정수 이동(-42,-67). 발 정렬 단계 리샘플링 없음.
머리 √(폭×높이) px / IDLE 대비: I352.96(0%) / W352.97(+0.00%) / S353.04(+0.02%) / H353.01(+0.01%) / D353.05(+0.02%).
측정: 보이는 늑대 가죽 두개부 셸~턱, 귀·장발·장식 제외. 새 원본 ROI=(400,300,335,370); 수동 측정이므로 Root 독립 확인 필요. metrics.json·head_roi_final.png 참조.
규격: 5/5 PNG1024×1280·8bit RGBA·모서리alpha0·alpha≥129 최하단y1220·x30..994. 새 STRIKE bbox=(123,154,901,1220).
후처리: 경계6px 안·휘도>185·채도<0.18 픽셀248개만 alpha0. SET_fringe_2x.png 전후 확인.
시트 갱신: SET_{poses,silhouette,face,battle250,before_after,fringe_2x}.png; SET_rotation_fix_compare.png에 HOLD→수정→확정 회전 참고 비교. battle250 동일 공통 배율.
남은 차이: 측면/등 회전으로 목걸이·가슴 장식이 가려지고 곡도 끝 방향·곡률·망토 주름이 달라짐. 회전 강도·무기 동일성은 Root 최종 재검수 대상.
인계: _work_SET/fix1_validation.json에 5장 재검증·유지4장 해시 보존. Assets/Library 쓰기·Unity 실행·커밋·삭제 없음. 최종 승인 대기.
