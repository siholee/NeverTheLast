# 로마 군단·콜로세움 SD 7차 제작

시작: 2026-11-04 · 상태: 21종 105장 제작·로컬 등록 완료 / 최종 검수 대기

기준: ../../SD_Spec.md. 기존 회차·원화를 보존한다. 좌향 SD 등록 지원을 위해 StageFigureData·StageFigure·SdFrameRig의 원본 방향 처리만 수정했다.
사용자 추가 지시(2026-11-04): 적의 5포즈는 모두 좌향이다. 공통 SD 명세의 우향 규정보다 이 지시를 우선한다. 먼저 생성된 콜로세움 IDLE 9종은 원본 보존 후 정수 픽셀 좌우 반전, 이후 생성은 좌향 프롬프트를 사용한다.
사용자 추가 지시(2026-11-04): 로마 일반병 HASTATI/PRINCIPES/TRIARII/EQUITES/CENTURION/VELITES/SCORPIO/OPTIO/TRIBUNE는 5포즈 모두 눈을 완전히 가린 익명 모브다. 중복 배치·닮은 형태를 허용한다. 네임드 AGRIPPA/OCTAVIA/CAESAR와 콜로세움 검투사는 얼굴을 유지한다. 생성 도중 눈이 보였던 후보는 별도 eye revision으로 교체하며 이전본을 보존한다. 이 규칙은 SD_Spec.md에도 반영한다.
SD 원본 방향은 sd.facing:left로 등록했다. 생략한 기존 49개 SD 설정의 기본값은 right이며, 상위 facing은 대체 스탠딩 방향으로 유지한다. SD 리그의 반전·접점 앵커에 같은 방향을 적용했다. 실제 Unity 검증은 실행하지 않았다.
60_enemies.yaml의 themeId 105(9종)·106(12종), 총 21종×5포즈=105장.
현재 80_stages.yaml에서 사용하지 않는 11024·11025·11037도 같은 테마 등록 적으로 포함한다. 편성은 변경하지 않는다.
키트 원본: roster.json, 20_codes.yaml, RestoredEnemyCodes.cs, DamageTags.cs.
아군 캐릭터 상세 문서는 이 적들에게 존재하지 않으므로 적 상세 카탈로그·실행 키트로 대체 대조한다.
50001 베기·50002 찌르기·50003 사격·50004 타격·50005 주문. 타격은 방패/손잡이/무기 측면, 지휘는 비공격 손짓으로 해석한다.
실제 소환체는 없음. 사비나의 기술명과 에퀴테스의 병과명만으로 동물 소환체를 추가하지 않는다.
이미지 원본은 _work_{ID}/*_raw.png, 내보낸 포즈는 루트, 이후 크기 보정은 registered/ 사본에 한정한다.
내장 image_gen 사용·transparent_background=true. 프롬프트는 시각 특징만 쓰며 고유 이름을 넣지 않는다.
자체 검사는 최종 승인이 아니다. Root(Claude) 최종 검수·Unity 임포트/실제 Play/릴리스 검증은 대기다. 새 PNG 105개의 meta/GUID는 다음 Unity 임포트에서 자동 생성하며 직접 작성하지 않았다.

사용자 확정: 옥타비아는 보주 마술사다. 청록 보주·금색 받침을 추가하고 전 5포즈에서 유지했다. 빈손 시전 후보는 _work_OCTAVIA/에 보존했다.
선정본은 이 폴더의 105장, 크기 보정본은 registered/, 실제 등록은 Assets/Resources/Sprite/SD/와 97_stage_figures.yaml이다. 교체 전 선정본은 각 _work_ID/_superseded/에 보존했다.
내장 image_gen 총 131회(최초 105 + 재생성/수정 26). 실제 전체 프롬프트는 prompts_final.json, 선정·등록 해시는 generation_manifest.json·registration_manifest.json에 있다.
105장 모두 1024×1280 8bit RGBA, 모서리 alpha0, alpha≥129 발 y1220·가로30..994, 등록본 해시 일치. 추정 두개부 ROI 기준 최대 편차 2.9031%. 머리카락·투구에 가려진 두개부는 직접 측정할 수 없어 독립 재측정을 최종 승인으로 남긴다.
검수 시트: sheets/ID_poses.png·ID_silhouette.png·ID_face.png·ID_head_roi.png. 보고서 21개는 모두 20줄 이내의 검수 대기 상태다.
검증 결과·실패 후 수정 이력: Logs/SDPoseV7_20261104/report.md. 보조 MSBuild 컴파일과 실제 DTO/YamlDotNet 검증은 Unity Play 검증을 대신하지 않는다.
