# 혹한의 거인 (FROST_GIANT / 13061)
상태: **재검수 대기** · 2026-11-05 Root STRIKE 재보정
근거: root_review_pm.md 끝 절. 크기·머리는 PASS, 정지 STRIKE만 HOLD였으므로 STRIKE만 교체. SD_Spec·v10/v9 준수.
N 서리 짓밟기: 적 전체 / 접촉 타격 45%·얼음 부착. U 혹한의 강림: 적 전체 / 접촉 타격 85%.
P 혹한의 노심: 추가 효과 없음. SP·실제 소환수 없음. 키트 수치·판정·데이터 변경 없음.
타격 해석: Root가 허용한 양손 철퇴 바닥 내려찍기. 상체를 좌측으로 숙여 대각선 자루를 양손으로 밀고 낮은 철퇴 머리를 접지한 타격 정점.
프롬프트: 동일 백/청색 판갑·은색 관·좌향 큰 투구와 체급 유지, 양손 하강·철퇴 접지·투명 배경·문자/VFX/그림자 금지; _fix2_root/prompts.json 전문.
이번 추가 생성: STRIKE 2회(두 번째 폭 정리본 선택), 나머지 4포즈 0회. 내장 image_gen·transparent background. 이전 시도와 분리 기록.
통과한 IDLE/WINDUP/HIT/DOWN registered PNG 4장과 루트 생성 원본 5장 해시 보존. 이전 STRIKE·시트·보고서는 _superseded/root_review_strike_fix2/.
선택 규격본→STRIKE 전신 균등 배율 1.0903; 정수 가로/세로 이동 0/-1px. 세부 수치: measurements_final.json.
보이는 투구 셸 ROI 정수 실측 √(폭×높이) px / IDLE 대비: IDLE 380.8(0.00%), WINDUP 381.0(+0.06%), STRIKE 388.2(+1.94%), HIT 380.5(-0.07%), DOWN 380.9(+0.02%)
발 y(alpha≥129): 5포즈 모두 1220. 1024×1280 RGBA8·모서리 alpha 0·좌향·가로 30..994 확인; STRIKE x58..987.
타격 때 숙이는 자세는 유지. IDLE 높이 250px의 동일 배율 나란히 보기·단색 실루엣·투구 ROI·2배 외곽 비교를 직접 확인.
기존 보스 크기 기준 유지: IDLE 높이 1020px·폭 934px, 동일 배율 CRYO 일반병 대비 불투명 면적 1.84~2.82배.
갱신: registered/FROST_GIANT_SD_STRIKE.png, 시트 4종 poses/silhouette/face/battle250 및 head_roi, 측정·검수 묶음. 다른 캐릭터 수정 없음.
후처리: PowerShell System.Drawing 전신 균등 배율·발 정수 이동. 외부 투명과 연결된 6px 내 휘도>185·채도<0.18 흰 외곽만 정리.
재현 자료: _fix2_root/ 원시 2장·프롬프트·ROI·배율·STRIKE_fix_edge_2x.png·before_after_same_scale.png. 배경·균열·참격·충격 VFX 없음.
남은 확인: 새 투구/판갑의 미세 차이와 숙인 투구의 투영은 Root 최종 미술 재검수 대상. Unity·런타임 검수 미수행.
