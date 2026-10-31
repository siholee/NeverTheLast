# P1 작업 지시서 — 무대 모드 1차(시안 전투 화면 재현)

작성: 2026-10-29 · 발주: Root(PM) · 수행: Codex · 상태: 🔸 미착수

[SD 디오라마 전장 제안](../../../Assets/Docs/Proposals/Visual_SD_Remake.md)의 1차 구현이다. 화면 기준은 같은 폴더의 `index.html`(시안 v0.3)이고,
좌표·크기의 원본은 시안 스크립트의 `ART`·`sdBox`·`sdParts`·`colDist`·`place`·`anchor`·`cutin`이다.
작업 전 `AGENTS.md`와 `Assets/Docs/Design/README.md`를 읽는다. 그 규칙이 이 문서보다 우선한다.

## 목표

개발 전용 **Stage 표시 모드**에서 시안의 전투·준비 화면을 Unity로 재현한다. 무대 좌표, 현행 배경 원화, 스탠딩을 변형한 SD 프록시,
발밑 HUD 묶음, 초월기 컷인까지 포함한다. 전투 판정·행동 순서·피해·저장은 바꾸지 않고, 기본값인 Card 모드 화면도 바꾸지 않는다.

## 진행 방식

S1~S6을 순서대로 한다. 각 단계가 끝나면 `Logs/VisualSDP1_<날짜>/S<n>.md`에 짧은 보고를 남기고 **멈춘다**. 다음 단계는 Root가 확인한 뒤 시작한다.
단계 보고는 30줄 안으로 쓴다: 바꾼 파일, 한 일, 실행한 검사와 결과(실패 포함), 촬영 경로, 막힌 점.

## 공통 금지와 중단 조건

- `Combat/`·`ActionScheduler`·`Unit.TakeDamage`·상태·자원·소환 판정, YAML 수치, 저장 v17 DTO, 사용자 설정 DTO를 수정하지 않는다.
- 연출 콜백 수·타이밍과 행동 대기 시간을 바꾸지 않는다. 새 연출은 기존 대기 안에서만 재생한다. 1~5배와 별도 일시정지를 따른다.
- Card 모드의 화면과 앵커 값을 바꾸지 않는다. 기존 `.meta` GUID를 재생성하지 않고 구매팩 파일을 추가·삭제하지 않는다.
- `Assets/Docs/Design/`은 고치지 않는다. 커밋하지 않는다.

판정 코드 수정이 필요하거나, Card 모드 값이 달라지거나, 저장 형식이 바뀌거나, 아래 “건드릴 곳” 밖의 파일 10개 넘게 바꿔야 하면 그 자리에서 멈추고 보고한다.

## 건드릴 곳

| 파일 | 지금 하는 일 | 이번에 하는 일 |
| --- | --- | --- |
| `Managers/GridManager.cs` | `LayOutColumn`이 셀·유닛 위치를 정하고 `FrameCamera`가 카드 경계로 카메라를 맞춘다 | Stage 모드면 `StageLayout`으로 위치·정렬, 고정 카메라 |
| `Entities/Cell.cs` | `UnitCardView.Attach`로 카드 뷰를 붙이고 `Cell.Card`로 노출 | 변경 최소. 모드 변경 알림 전달 |
| `Entities/View/UnitCardView.cs` | 카드 원화·`Body`(모션)·`OrbHud`(초월 원)·`Hud`(유리판: 체력·상태)·앵커 | Stage 모드 분기: 카드 장식 숨김, `StageFigure` 부착, HUD 묶음 배치, 앵커 계산 |
| `Effects/BattlefieldBackdrop.cs` | 테마 배경, 유리판 `SetBoard`, 초점·섬광 | Stage 모드면 배경 전체 화면 + 색 보정 층, 유리판 끔 |
| `Managers/FieldPointerInput.cs` | 셀 hover·드래그 | Stage 좌표의 히트 영역, hover를 HUD 묶음·패널과 연동 |
| `Managers/UI/HUD/ActionQueuePanel.cs` · `DamageMeterPanel.cs` | 행동 순서·피해 집계 | 항목 hover 시 해당 유닛 강조 |
| `Core/VerifyHarnessModels.cs` · `Managers/UI/DevTools/VerifyHarnessRunner.cs` | 검증 요청·실행 | 요청 필드 `visualMode` |

VFX·피드백 소비자는 `UnitCardView`의 공개 앵커만이 아니라 `UnitCardView.PortraitRenderer`(bounds·sprite·transform)와 `Cell.CardCenter`·`Cell.CardSize`를 직접 읽는다.
확인된 파일은 `VfxSpace`·`DamagePopup`·`StatusPopup`·`CardLifecycleFx`·`PortraitAccentFx`·`ProjectileFx`·`PurchasedArcherFx` 등 12개 이상이고, `CombatFeedbackSurface`는 `Cell.CardSize`를 쓴다(S1 보고, Root 확인).
소비자 파일을 하나씩 고치지 말고, Stage 모드에서 이 멤버들이 SD 기준 값을 돌려주게 해서 호환을 유지한다(S3·S5 참조).
토글 UI는 `Managers/UI/DevTools/DebugOverlay.cs`에 있다.

## 단계

### S1 토글과 좌표 계산

새 파일 `Managers/Stage/VisualModeState.cs`(namespace `Managers.Stage`):

```csharp
public enum VisualMode { Card, Stage }
public static class VisualModeState
{
    public static VisualMode Current { get; private set; } = VisualMode.Card;
    public static event System.Action<VisualMode> Changed;
    public static void Set(VisualMode mode);   // 같은 값이면 무시, 바뀌면 Changed 발행
}
```

개발 전용이다. DebugMode 패널 토글과 검증 요청에서만 바꾸고 저장하지 않는다. 검증 요청은 요청 전체에 `visualMode`("Card"|"Stage", 기본 Card)를 두고 시나리오별로 덮어쓸 수 있게 한다.

새 파일 `Managers/Stage/StageLayout.cs`: 1920×1080 기준 화면 픽셀로 계산하는 순수 함수다.

```csharp
public static class StageLayout
{
    public const float CenterX = 960f, FarY = 590f, NearY = 880f, BodyHeightPx = 250f;
    public readonly struct Slot { public readonly Vector2 Pixel; public readonly float Depth01, Scale; }
    // 전투: 한 열 count명 중 index번째. t = count==1 ? .5 : count==2 ? {.22,.78}[index] : index/(count-1)
    public static Slot ForBattle(int side, int column, int index, int count);
    // 준비: 4레인 고정. t = row/3, 지그재그 순번 = row
    public static Slot ForPreparation(int side, int column, int row);
    // 중앙에서의 거리 = ((column==0?230:480) + (0.5-t)*100 + (i%2==0 ? 70 : -50)) * (0.86 + 0.14*t)
    public static float ColumnDistance(int column, float t, int i);
    public static float DepthScale(float t) => 0.84f + 0.22f * t;
    public static Vector3 PixelToWorld(Vector2 pixel, Camera camera);   // 직교 카메라 기준
}
```

화면 x = `CenterX + side * ColumnDistance`, y = `FarY + (NearY-FarY)*t`(위가 0인 픽셀). 전투 중 전열이 비면 후열을 column 0 거리로 당기는 현행 규칙을 따른다.
검사: 시안의 `colDist`·`place`와 같은 입력 20개에서 값이 ±0.5px 안인지 표를 S1 보고에 붙인다. 컴파일과 Console 오류 0.

### S2 카메라·배경·배치

- Stage 모드에서 `GridManager`가 셀과 유닛을 `StageLayout` 위치에 두고, 정렬 순서를 화면 y로 준다(`ApplyDepth`와 같은 정렬 체계).
- 카메라는 1920×1080 무대를 고정으로 담는다. `FrameCamera`의 카드 경계 계산과 밀도 배율(`ApplyDensityScale`)은 Stage 모드에서 쓰지 않는다.
- 16:9가 아닌 화면에서는 무대 높이 1080을 기준으로 맞추고, 좌우는 배경으로 채우거나 잘라낸다. 무대 좌표를 가로로 늘리지 않는다. `StageLayout.PixelToWorld`가 이 규칙을 따르도록 고친다.
  1920×1080과 2560×1080 두 해상도로 촬영해 확인한다.
- 배경은 현행 테마 `backgroundKey` 원화를 화면 전체에 깔고 유리판을 끈다. 배경 바로 위에 시안 `.grade` 층을 둔다: 상단 14%·하단 26% 먹군청 그라데이션과 가장자리 비네트.
- 이 단계에서는 카드가 그대로 보여도 된다. 위치와 정렬만 확인한다.

검사: `five_party`·`prep`을 Stage 모드로 1회씩 실행하고 1920×1080 촬영 1장씩.

### S3 SD 프록시

새 데이터 `Assets/Resources/Data/97_stage_figures.yaml`과 DTO·로더(기존 `DataManager` 방식):

```yaml
# 스탠딩 원화 높이 기준 비율. top=머리(머리카락·투구) 위, chin=턱, cx=얼굴 중심 x, feet=발바닥
# facing=원화가 바라보는 방향. 아군은 오른쪽, 적은 왼쪽을 보도록 필요할 때만 반전한다.
figures:
  - { sprite: SURTR_STANDING,      top: 0.140, chin: 0.235, cx: 0.46, feet: 0.975, facing: right }
  - { sprite: INDRA_STANDING,      top: 0.145, chin: 0.240, cx: 0.45, feet: 0.960, facing: left  }
  - { sprite: KUBERA_STANDING,     top: 0.040, chin: 0.160, cx: 0.50, feet: 0.970, facing: left  }
  - { sprite: MANSAMUSA_STANDING,  top: 0.030, chin: 0.146, cx: 0.51, feet: 0.970, facing: right }
  - { sprite: SURYA_STANDING,      top: 0.040, chin: 0.170, cx: 0.51, feet: 0.960, facing: left  }
  - { sprite: SEI_STANDING,        top: 0.030, chin: 0.155, cx: 0.52, feet: 0.960, facing: right }
  - { sprite: ATALANTE_STANDING,   top: 0.050, chin: 0.170, cx: 0.55, feet: 0.960, facing: right }
  - { sprite: AZTEC_JAGUAR_STANDING,       top: 0.140, chin: 0.265, cx: 0.42, feet: 0.945, facing: left }
  - { sprite: AZTEC_ELITE_JAGUAR_STANDING, top: 0.100, chin: 0.235, cx: 0.55, feet: 0.930, facing: left }
  - { sprite: AZTEC_EAGLE_STANDING,        top: 0.080, chin: 0.220, cx: 0.43, feet: 0.970, facing: right }
  - { sprite: AZTEC_SERPENT_PRIEST_STANDING, top: 0.100, chin: 0.230, cx: 0.51, feet: 0.930, facing: left }
default: { top: 0.05, chin: 0.17, cx: 0.50, feet: 0.97, facing: right }
```

위 값은 시안에서 눈으로 잡은 시드다. `facing`은 원화를 보고 다시 확인한다. 목록에 없는 원화는 `default`를 쓰고 S3 보고에 “미보정” 목록으로 남긴다.

새 파일 `Entities/View/StageFigure.cs`: `UnitCardView`의 `Body` 아래에 붙는다. 그래서 기존 전진·반동·흔들림 모션이 그대로 적용된다.

- 같은 스탠딩 스프라이트를 `SpriteRenderer` 두 개(Head, Lower)로 그린다. 셰이더 `Resources/Shaders/StageFigureSlice.shader`가 sprite UV(`textureRect` 기준)로 잘라낸다.
  - `_ClipV`(min,max): 세로 잘라낼 범위.
  - `_Ellipse`(cx,cy,rx,ry)와 `_Feather`: Head 전용 타원 마스크. 타원 반경의 70%부터 100%까지 서서히 지운다.
  - `_KeepAboveV`: 이 값보다 위(머리카락·깃털)는 타원과 상관없이 남긴다.
- 배율은 머리 `SH=1.5`(가로세로 같음), 몸 세로 `SB=0.5`·가로 `SBX=0.8`이고, `neck = chin + 0.012`이다. 몸 높이 T = `BodyHeightPx × DepthScale × 크기등급`(정예·대형 1.15).
  - `Hi = T / ((neck-top)*SH + (feet-neck)*SB)`
  - Head: 원본 [0, neck] 구간을 Hi*SH로 키운다. 타원 중심은 ((top+chin)/2), 반경은 rx = 0.95·(chin−top), ry = 0.74·(chin−top)이다.
  - Lower: [neck, feet] 구간을 (Hi*SBX, Hi*SB)로 줄인다.
  - 두 조각 모두 가로 기준점은 얼굴 중심 `cx`, 발은 유닛 원점에 둔다.
- 호환 규칙: Stage 모드에서 `UnitCardView.PortraitRenderer`는 숨긴 카드 원화가 아니라 `StageFigure`의 Lower 렌더러를 돌려준다. bounds·transform 기반 소비자가 SD 몸통을 기준으로 자동으로 바뀌게 하려는 것이다.
  `Cell.CardCenter`는 chest를, `Cell.CardSize`는 SD 몸 높이 기준의 등가 크기를 돌려준다. Card 모드 반환값은 그대로 둔다. 이 방식으로 안 되는 소비자가 있으면 목록만 S3 보고에 적고 고치지 않는다(S5에서 처리).
- Stage 모드에서는 `UnitCardView`의 카드 원화·창·테두리·바닥 패드를 숨기고, 접지 그림자 타원(150×30px × DepthScale)을 그린다. 행동 중에는 `ElementPalette` 색 바닥 고리를 켠다.
- 모션 거리를 카드 크기 비율 대신 픽셀로 바꾼다: 전진 36px(초월기 48), 반동 12px(초월기 20). 이 값은 Stage 모드에서만 쓴다.

검사: `five_party`·`combat_1v1` Stage 촬영. 시안 같은 배치와 나란히 놓은 비교 이미지 1장.

### S4 발밑 HUD 묶음

기존 `OrbHud`(초월 원)와 `Hud`(체력·보호막·상태)의 바인딩과 갱신 로직은 그대로 두고 배치만 Stage용으로 바꾼다.

- 묶음 크기 158×46px, 발 아래 12px에 가운데 정렬한다. 반경 12px, 배경은 아군 `rgba(14,22,40,.72)`·적 `rgba(48,18,30,.72)`, 위쪽 꼭지는 7px이다.
- 구성(왼쪽부터): 초월 원 34px(마나형 연속 호·스택형 분절·상시형 점선, 퍼센트 글자 없음), 체력판 104px.
  - 체력판 첫 줄: 숫자 16px와 상태 아이콘 최대 3개(14px).
  - 둘째 줄: 체력 막대 6px. 감소분 잔상은 현행 규칙을 따른다.
  - 보호막은 막대 위 2px 금색 선, 강인도는 생성된 적에게만 막대 아래 2px.
- 고유 자원 배지(만종·텐션·치유 에너지·영혼 등)는 현행 표시 규칙과 값을 유지한 채 묶음 안에 둔다. 다른 자원으로 덮어쓰지 않는다.
- 정렬값은 주인 유닛 정렬값 +1로 둔다. 앞 SD가 뒤 묶음을 가려도 되며, 남의 몸 위에 다른 유닛의 묶음이 얹히지 않는 것이 목적이다.
- 강조 연동: SD·묶음·행동 순서 항목·피해 집계 행 중 하나에 올리면 넷이 함께 강조된다. 묶음은 흰 테두리와 1.08배, SD는 밝기 1.12, 패널 항목은 테두리로 표시한다.
  행동 중인 유닛의 묶음은 원소색 테두리로 빛난다.
- 상단 바·행동 순서(왼쪽 세로)·피해 집계(오른쪽)·재보(우하단)는 현행 위치와 모양을 유지한다.
- 행동 순서·피해 집계·현재 행동 카드의 얼굴은 SD가 아니라 캐릭터 초상화 원화(`UIPortraitCrop.Face`)를 쓴다(사용자 지시, 2026-10-29). SD 원화가 등록되어도 바꾸지 않는다.

검사: `five_party` Stage 촬영(5:5에서 모든 묶음의 숫자가 읽히는지), `card_hud_containment`를 Card 모드로 실행해 기존과 같은지 확인.

### S5 앵커·피격·숫자

Stage 모드에서 `UnitCardView`의 공개 앵커를 SD 기준으로 계산한다. Card 모드 계산은 손대지 않는다.

| 멤버 | Stage 모드 값 |
| --- | --- |
| `CenterWorld`·`PortraitFocusWorldPoint` | chest = Head 아래 끝 + Lower 높이의 20% |
| `ClampPortraitHitPoint` | Lower 사각형 안으로 제한 |
| `PortraitFocusScreenRect` | Head 타원의 외접 사각형 |
| `HudScreenRect` | 발밑 묶음 사각형 |
| `ResourceBadgeScreenRect` | 묶음 안 고유 자원 배지 사각형 |

- S3 보고의 “호환 안 되는 소비자” 목록을 여기서 처리한다. 효과 형상·판정 타이밍은 바꾸지 않고 기준점만 SD 앵커로 바꾼다.
- 상태 알림 글자는 head 위 26px에서 뜬다. 피해 숫자는 chest 위 60px에서 시작하고, 연타 누계(`N타 합계`)는 현행 규칙을 따른다.
- VFX는 지금처럼 유닛·HUD 앞에 온전한 형상으로 그린다. 접점 데이터(`portraitContacts`)는 Stage 모드에서 쓰지 않는다.

검사: `effect_hit_core`·`combat_feedback`을 Stage 모드로 실행한다. 피격 접점이 대상 몸통에 오는지 정점 촬영 3장으로 확인한다.

### S6 초월기 컷인·준비 모드·마무리

**초월기 컷인**(새 파일 `Effects/Vfx/UltimateCutIn.cs`, Stage 모드 전용)

- 초월기 시전 시작에 맞춰 재생하는 **차단하지 않는 오버레이**다. 대기·콜백을 추가하지 않는다.
- 초월기별로 시전 시작부터 첫 피해 콜백까지의 시간을 재서 S6 보고에 표로 남긴다. 컷인은 그 안에 끝나야 한다.
  1배 기준 창이 1.15s 이상이면 전체판, 0.5~1.15s면 같은 동작을 그 길이로 압축, 0.5s 미만이면 생략한다.
- 구성(시안 `cutin`):
  - 화면 어둡게 누르기(최대 50%).
  - −6° 사선 띠 250px가 세로로 열린다.
  - 스탠딩 원화가 들어온다. 높이 1100px, 띠와 반대로 +6° 세운다.
  - 원소색 광과 속도선, 기술명은 명조 굵게 78px로, 시차를 두고 들어온다.
- **얼굴 프레이밍 규칙.** 원화의 얼굴점 (cx·W, (top+chin)/2·H)을 띠 안의 고정점 (480, 112)에 맞추고, 그 점을 회전 축으로 쓴다.
  어떤 캐릭터도 얼굴이 띠 밖으로 잘리거나 기술명 글자에 가리면 안 된다. 기술명은 x 860부터 시작한다. 값은 `97_stage_figures.yaml`을 공유한다.
- 화면 섬광은 낮은 명도차로 한 번만 쓴다(Detail_07 광과민성 기준). 1~5배와 일시정지를 따른다.

**준비 모드**

- 아군 8칸을 바닥 타원 표식(170×46px)으로 보인다. 목표 칸은 진한 테두리, 비어 있는 원래 칸은 점선이다.
- 기존 드래그·교체·중복 금지·모바일 드롭 대상이 Stage 좌표에서 그대로 동작해야 한다. 콜라이더도 같은 위치로 옮긴다. 들어올림 연출은 P5다.

**소환수**: `SummonCardView`는 주인 feet 옆에 두는 최소 이동만 한다. 더 필요하면 하지 말고 제한으로 남긴다.

검사(마무리, 실제 Play Mode):

| 시나리오 | 모드 | 판정 |
| --- | --- | --- |
| `five_party` `combat_1v1` `speed8` | Card·Stage 둘 다 | 두 모드의 행동/피해 사건 순서·수·결과가 같다 |
| `prep` | Stage | 드래그·교체·중복 금지 통과 |
| `card_hud_containment` | Card | 기존과 같다 |

촬영 4장: 5:5 전투, 1:1 전투, 준비 화면, 초월기 컷인 정점. 컷인 1장에는 얼굴이 띠 안에 온전히 보여야 한다.
마지막 보고 `Logs/VisualSDP1_<날짜>/report.md`에는 단계 보고 링크, 마무리 검사 표, 촬영본과 시안 비교 이미지, 남은 제한, 미보정 원화 목록만 적는다.
`Assets/Docs/Proposals/Visual_SD_Remake.md`의 P1 행을 `🔸 검증 필요`와 날짜로 갱신한다. 테스트 산출물은 AGENTS.md 정리 규칙대로 지우고 결과만 남긴다.

## 검증 요청 예시

```json
{
  "visualMode": "Stage",
  "lineup": "party5",
  "captureWidth": 1920,
  "captureHeight": 1080,
  "scenarios": [
    { "name": "five_party" },
    { "name": "combat_1v1" },
    { "name": "card_hud_containment", "visualMode": "Card" }
  ]
}
```

정지 촬영 PASS는 미술 승인이 아니다. 미술 판정은 Root가 따로 한다.
