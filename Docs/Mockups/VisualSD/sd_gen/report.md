# SD 전투 스프라이트 시범 생성
2026-10-29 · 전체 상태: **검수 대기** · 시범 원화이며 최종 미술 승인이 아님.
방식: 내장 image_gen, transparent_background=true. 총 15회 생성(최초 5회 + 재생성 10회), 캐릭터별 최종 IDLE 1장만 저장. 원소: SHI Anemo, SEI Geo, SURTR Pyro, SKADI Cryo, FREYA Dendro.
참조: 매번 Assets/Resources/Sprite/Standings/Allies/{ID}_STANDING.png 원화 첨부. 시·세이·스카디 방향 보정 회차에는 수르트 최초 SD도 첨부; 최종 세이·수르트·스카디·프레이아에는 최종 시 SD를 화풍/비율 참조로 추가.
출력: {ID}_SD_IDLE.png 5장 + contact_sheet.png(2500×1450, 상단 원화/하단 SD) + 이 보고서.
내보내기: 도구 원본 1122×1402 RGBA → 투명 외곽 범위 기준 균등 축소·중앙 배치 → 1024×1280 RGBA. 생성 알파 보존, 배경 제거·마스킹·부분 수정·좌우 반전 없음.
5장 모두 알파 가시 영역 아래 51px(3.98%) 여백 확인. 시는 발이 가려져 옷자락 하단을 기준으로 하며 실제 발 기준 확인이 필요함.
작업 범위: 이 폴더만 작성. Assets·Library/NeverTheLastVerify·Unity·Git 인덱스/커밋 미조작.

## 실제 사용 프롬프트
아래 공통 본문에서 {keep}/{color}를 다음 표의 문자열로 치환한 것이 각 최종 호출의 전체 프롬프트다. 포즈만 IDLE로 선택했으며 별도 지시문을 덧붙이지 않았다.
```text
Full-body super-deformed (SD) battle sprite for a mobile gacha RPG, based on the attached character illustration.
Proportions: about 3 heads tall, slender short limbs, detailed hands, mature refined face with small sharp eyes (not a toddler chibi).
Keep from the reference: {keep}.
Style: thin clean lineart, painterly cel shading in 2-3 tones, soft warm rim light in {color}, costume ornaments kept, weapon slightly exaggerated.
Pose: idle battle stance.
View: three-quarter view facing right. Transparent background, no ground shadow, no text, no frame. Character centered, feet near the bottom edge. 1024x1280.
Avoid: thick black outlines, round mitten hands, oversized baby head, blob body, flat vector look, background scenery.
```
| ID | {keep}: 실루엣 핵심 3요소(세미콜론 구분) | {color}: YAML 원소/ElementPalette 색 |
|---|---|---|
| SHI | black hair in a low side bun with a red flower hairpin and tassel; flowing charcoal kimono with dark red lining and a black obi; long wide draped sleeves framing empty detailed hands with no weapon | mint-green (#5CDAB3) |
| SEI | very long straight silver-white hair with pale gold tips; flowing white robes with fine gold trim and wide sleeves; geometric gold waist clasp with a long pendant and tassel, both hands empty with no weapon | golden-yellow (#FFC836) |
| SURTR | long wild crimson hair with an ahoge; long black coat with crimson lining and gold trim; huge black-and-gold greatsword with glowing red runes | orange-red (#FF6633) |
| SKADI | long slate-blue hair with a small silver star hairpin; layered ice-blue and white robes with a fur shoulder mantle and silver geometric embroidery; dark twisted staff enclosing a blue orb with dangling crystal ornaments | ice-blue (#99E5F6) |
| FREYA | long blonde high ponytail with side braids and brass goggles; forest-green and ivory dress with gold embroidery and a white fur shoulder mantle; brown leather tool belts and pouches with hanging wrenches and metal rings, both hands empty with no weapon | yellow-green (#96CB2D) |
초기 시의 {keep}: black hair in a low side bun with a red flower hairpin and tassel; flowing charcoal kimono with dark red lining and a black obi; long wide draped sleeves and a trailing layered hem
초기 세이의 {keep}: very long straight silver-white hair with pale gold tips; flowing white robes with fine gold trim and wide sleeves; geometric gold waist clasp with a long pendant and tassel
그 외 초기·재생성 본문/색은 최종과 동일. 시·세이 초기 결과의 원화에 없는 검/지팡이는 최종 결과에서 제외됨.
## 체크리스트 자체 판정
| ID | 실루엣/얼굴·비율 | 선·손/방향 | 가장자리/남은 문제 | 재생성 | 상태 |
|---|---|---|---|---:|---|
| SHI | 검은 올림머리·꽃 비녀/기모노/넓은 소매 확인; 약 3등신, 원화보다 눈·머리 과장 | 가는 선, 분리된 손가락, 오른쪽 3/4 | 발이 옷자락에 가림; 일부 녹색 테두리 강함 | 2 | 검수 대기 |
| SEI | 은백 장발/백금 로브/허리 술 확인; 머리 비율이 시보다 작아 보임 | 가는 선, 빈손 손가락, 오른쪽 3/4 | 머리·옷 가장자리 흰 번짐; 얼굴이 원화보다 어려 보일 수 있음 | 3 | 검수 대기 |
| SURTR | 붉은 장발/검붉은 코트/룬 대검 확인; 시와 비율 근접 | 가는 선, 손가락·검 파지 확인, 오른쪽 3/4 | 검·머리 림 과다; 룬은 무기 장식이며 별도 글자 없음 | 1 | 검수 대기 |
| SKADI | 청회 장발/모피·빙색 로브/구슬 지팡이 확인; 약 3등신 계열 | 가는 선, 지팡이 손 유지, 오른쪽 3/4 | 머리·로브에 흰 번짐/청색 림 과다; 별 장식·땋은 머리 변형 | 3 | 검수 대기 |
| FREYA | 금발 포니테일·고글/녹백 의상/공구 벨트 확인; 시와 비율 근접 | 가는 선, 빈손 장갑의 손가락 분리, 오른쪽 3/4 | 머리 끝 흰 번짐/노란 림 과다; 원화보다 무표정 | 1 | 검수 대기 |
공통: 전신·투명 배경·배경 풍경/프레임/접지 그림자/독립 글자 없음 확인. 두꺼운 먹선·벙어리장갑·아기 몸통은 보이지 않으나 성숙도와 3등신/머리 크기의 완전한 세트 일치는 추가 검수 필요.
원문 체크리스트의 동일 캐릭터 5포즈 간 의상·무기 손 일관성은 이번 범위(IDLE 1장씩)에서 검증 불가. 비교 시트의 체크 무늬는 투명도 확인용이며 개별 PNG에는 없음.
