# SD 포즈 5차 지시서 · 2026-10-30

발주: 사용자 → Root(PM) → 서브 에이전트(2명씩) → Codex(image_generation) · 상태: 🔸 진행
공통 규격·키트 먼저·금지 사항은 [3차 지시서](SD_Pose_v3_Brief.md)와 같다(초월자 톤 절은 적용하지 않는다). 소환수 규격은 [4차 지시서](SD_Pose_v4_Brief.md)를 따른다.

## 범위(사용자 지정 콘셉트 없음 — 원화와 키트로 정한다)

| 담당 | 캐릭터(원화 키) |
| --- | --- |
| A | 아누비스 ANUBIS · 세트 SET |
| B | 바루나 VARUNA · 인드라 INDRA |
| C | 바유 VAYU · 토트 THOTH |
| D | 아스톨포 ASTOLFO · 네프티스 NEPHTHYS |
| E | 에레쉬키갈 ERESHKIGAL · 네이트 NEITH |
| F | 오르페우스 ORPHEUS · 사바흐 SABAH |
| G | 이슈타르 ISHTAR · 만사 무사 MANSAMUSA |

- 같은 담당 안의 두 캐릭터도, 기존 SD(3·4차)와도 무기·자세·실루엣이 겹치지 않게 한다. 특히 사격(아마테라스·아탈란테·수리야·오리온)과 손짓 주문(세이·츠쿠요미·피그말리온) 계열과 구분한다.
- 키트에 실제 소환수(`Summons.Register`, 900번대 소환수)가 있으면 4차 소환수 규격으로 소환수 5장도 만든다. 소환수 판정만 있거나 표시 대상이 없으면 만들지 않고 보고한다.

## 4차에서 확정된 추가 기준

- 머리 크기는 두개부 ROI의 √(폭×높이)로 재고 같은 캐릭터 IDLE ±3%.
- 장식·명암 밀도는 3·4차 승인본(`style_c3_v3/sheets/_ALL_v3_overview.png`, `style_c3_v4/sheets/_ALL_v4_overview.png`)과 맞춘다. 너무 단순하면 안 된다.
- 부적·문양·책·악보 등 소품 무늬는 어떤 문자로도 읽히지 않게.

## 산출물

`sd_gen/style_c3_v5/{ID}_SD_{IDLE|WINDUP|STRIKE|HIT|DOWN}.png`, `sheets/{ID}_poses.png`·`{ID}_silhouette.png`·`{ID}_face.png`, `reports/{ID}.md`(20줄 이내, 상태 "검수 대기").
