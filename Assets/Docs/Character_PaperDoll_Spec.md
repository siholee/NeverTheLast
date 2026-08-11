# 캐릭터 페이퍼돌(모듈러 도트) 시스템 규격서

메이플스토리식 모듈러 캐릭터를 NeverTheLast에 도입하기 위한 아트/구현 규격.
스킬 이펙트는 기존대로 **별도**(Hovl 등)로 유지하고, 캐릭터는 "적당한 제스처 모션"만 담당한다.

## 0. 확정된 결정

| 항목 | 결정 |
|---|---|
| 시점 | 사이드뷰 **1방향(오른쪽 바라봄)** + `flipX` 좌우반전 |
| 고유 외형 | `Face`(표정), `Hair`(헤어) — 캐릭터별 1장 |
| 장비 외형 | 게임 아이템 슬롯을 그대로 사용: `MainHand`, `OffHand`, `Armor` |
| 모션 | `IDLE`, `SPAWN(등장)`, `ATTACK`, `HIT`, `EXIT(퇴장)` |
| 베이스 | **전 캐릭터가 동일 베이스 몸/머리 공유** → 모션 클립은 1세트만 제작 |
| 애니메이션 방식 | **A. 퍼펫(컷아웃) 리그** 기준. (대안 B는 §7) — *최종 확정 필요* |

## 1. 프레임/캔버스 규격

- **프레임 셀 크기**: `96 × 96 px` (무기·헤어가 몸 밖으로 삐져나올 여유 포함)
- **캐릭터 실루엣 높이**: 약 `64 px` (머리끝~발끝)
- **루트 피벗**: 바닥 중앙 `(0.5, 0)` — 발이 셀 바닥에 서도록
- **PPU(Pixels Per Unit)**: 한 유닛이 그리드 셀 하나를 채우도록 조정 (초기값 96, 스케일은 프리팹 transform에서 미세조정)
- 파츠별 개별 스프라이트의 **피벗은 각자의 관절점**(아래 §3)에 둔다. 예: 무기 피벗 = 손잡이, 머리 피벗 = 목.

## 2. 레이어 z-순서 (뒤 → 앞)

| z | 레이어 | 출처 | 비고 |
|---|---|---|---|
| 0 | `Weapon_Back` | 아이템(MainHand) | 활/지팡이가 몸 뒤로 갈 때. v1 생략 가능 |
| 1 | `Hair_Back` | 캐릭터 | 뒷머리 (없으면 생략) |
| 2 | `BackArm` | **베이스** | 몸 뒤쪽 팔 |
| 3 | `Body` | **베이스** | 몸통 + 두 다리 |
| 4 | `Armor` | 아이템(Armor) | 몸통 옷/갑옷 오버레이 |
| 5 | `Head` | **베이스** | 머리 베이스 형태 |
| 6 | `Face` | 캐릭터 | 표정 (눈/입) |
| 7 | `Hair_Front` | 캐릭터 | 앞머리 |
| 8 | `OffHand` | 아이템(OffHand) | 방패 등 |
| 9 | `FrontArm` | **베이스** | 앞쪽 팔 (ATTACK 시 움직임) |
| 10 | `MainHand` | 아이템(MainHand) | 무기 (손 그립에 부착) |

> `SpriteRenderer.sortingOrder` 또는 `Sorting Group` 내 순서로 구현. 좌우반전 시에도 z-순서는 유지된다.

## 3. 관절(앵커) 맵 — **전 캐릭터 공유 계약**

프레임 좌하단 `(0,0)` 기준 픽셀 좌표(오른쪽 바라보는 상태 IDLE 0프레임). **이 값은 모든 캐릭터/아이템이 반드시 지켜야 하는 계약**이다. 값 자체보다 "전원이 동일하게 지키는 것"이 중요.

| 관절 | 좌표(px) | 부착 파츠 |
|---|---|---|
| 바닥/발 중앙 (루트) | (48, 6) | 루트 피벗 |
| 골반 (몸 루트) | (48, 40) | Body, Armor |
| 목 | (48, 62) | Head |
| 머리 중심 | (48, 74) | Face 원점 |
| 헤어 원점 | (48, 80) | Hair |
| 앞 어깨 | (52, 58) | FrontArm |
| 손 그립 | (64, 46) | MainHand (IDLE 기준, ATTACK 시 이동) |
| 보조손 | (34, 46) | OffHand |

## 4. 모션 프레임 예산 (퍼펫 기준 = 트랜스폼 키프레임)

| 모션 | 프레임 | 재생 | 내용 |
|---|---|---|---|
| `IDLE` | 2~4 | Loop | 미세한 호흡/들썩임 |
| `SPAWN`(등장) | 6~10 | 1회 → IDLE | 페이드인 + 작은 착지/스케일업 |
| `ATTACK` | 4~6 | 1회 → IDLE | **이동 위주(찌르기·내려치기)**. 픽셀 보호를 위해 큰 회전 지양 |
| `HIT` | 2~3 | 1회 → IDLE | 살짝 뒤로 움찔 + 플래시 |
| `EXIT`(퇴장) | 6~10 | 1회 | 페이드아웃/디졸브/퇴장 |

- 모션 클립은 **베이스 뼈대에만** 걸며, 전 캐릭터가 공유한다(1회 제작).
- 얼굴/헤어/아이템은 각 1장이라, 뼈대가 움직이면 자식으로 따라 움직인다.

## 5. 아트 파일/네이밍 규약 (Aseprite 권장)

- **베이스 몸**: `BASE_body.aseprite`
  - 레이어: `BackArm / Body / Head / FrontArm`
  - 태그(프레임 구간): `IDLE / SPAWN / ATTACK / HIT / EXIT`
  - 내보내기: 파츠별 시트 `BASE_body_{part}.png` + JSON, 또는 파츠별 개별 PNG
- **캐릭터 고유**: `{UNIT}_face.png`, `{UNIT}_hair.png` (표정 변형 시 `{UNIT}_face_{expr}.png`)
- **아이템**: `ITEM_{id}_{slot}.png` (예: `ITEM_4102_mainhand.png`, `ITEM_4114_armor.png`)
- 모든 PNG는 §1 캔버스/피벗, §3 관절 좌표를 준수.

배치 경로(제안):
```
Resources/Sprite/Char/Base/        # BASE_body_*.png (공유)
Resources/Sprite/Char/Face/        # {UNIT}_face.png
Resources/Sprite/Char/Hair/        # {UNIT}_hair.png
Resources/Sprite/Char/Item/        # ITEM_{id}_{slot}.png
```

## 6. Unity 조립 (퍼펫 기준)

**패키지**: `com.unity.2d.animation` (Sprite Library, Sprite Resolver, Skinning Editor)

**프리팹 `CharacterView` 계층**:
```
CharacterView (root, pivot bottom-center)
├─ Hair_Back      (SpriteResolver: cat=Hair, label=back)
├─ BackArm        (base bone)
├─ Body           (base bone)
├─ Armor          (SpriteResolver: cat=Armor)
├─ Head           (base bone)
│  ├─ Face        (SpriteResolver: cat=Face)
│  └─ Hair_Front  (SpriteResolver: cat=Hair, label=front)
├─ OffHand        (SpriteResolver: cat=OffHand)
├─ FrontArm       (base bone)
│  └─ MainHand    (SpriteResolver: cat=MainHand)  # 손 그립에 부착
└─ (Animator)     # Idle/Spawn/Attack/Hit/Exit 상태 + 트리거
```

- **애니메이션 클립 5종**은 베이스 뼈대 트랜스폼을 애니메이트 → 전 캐릭터 공유.
- **외형 교체**: 캐릭터별 Sprite Library 변형 에셋을 할당하거나, 슬롯별 런타임 교체
  `spriteResolver.SetCategoryAndLabel("MainHand", "ITEM_4102");`
- **좌우반전**: 루트의 `localScale.x = -1` (또는 각 SpriteRenderer.flipX).

**Animator 상태**: `Spawn`(Entry) → `Idle`(loop) ←→ `Attack`/`Hit`(트리거, 종료 후 Idle 복귀) → `Exit`(트리거, 1회).

## 7. 코드 연동 (구현 시)

기존 이벤트 시스템에 그대로 훅한다:

| Unit 이벤트 | 모션 |
|---|---|
| `OnSpawn` / ActivateUnit | `SPAWN` → `IDLE` |
| `OnNormalActivates` | `ATTACK` |
| `OnAfterDamageTaken` | `HIT` |
| `OnDeath` / DeactivateUnit | `EXIT` |

- 현재 `Cell.portraitRenderer`(정적 1장, `Unit.LoadSprite`)를 셀 위 `CharacterView` 프리팹 인스턴스로 대체. 상점/인포탭용 **정적 포트레이트는 병행 유지**.
- 신규 `CharacterView` MonoBehaviour API(예정):
  - `SetAppearance(faceId, hairId)`
  - `EquipVisual(string gameSlot, int itemId)` — 게임 슬롯(MainHand/OffHand/Armor)→레이어 매핑
  - `PlayMotion(MotionType)`
- 유닛의 장착 아이템 id를 시각 슬롯으로 넘겨 무기/방패/갑옷 외형 반영.
  (구현 전 유닛의 장비 보관 방식 확인 필요.)

## 8. 대안 B — 프레임 바이 프레임 (참고)

퍼펫 대신 진짜 메이플식으로 갈 경우 §4·§5·§6이 아래로 바뀐다:
- 각 파츠(얼굴/헤어/무기/갑옷)가 **모션별 모든 프레임 시트**를 가져야 함.
- 관절 대신 **프레임별 앵커 오프셋** 테이블 필요.
- 장점: 픽셀 최상, 자유로운 휘두르기. 단점: 아이템/캐릭터당 아트량 폭증 → 다수 캐릭터엔 비현실적.
- 절충: 대부분 퍼펫, **시그니처 히어로 무기만** 프레임 아트로 수작업.

## 9. 작업 분담

- **Claude가 가능**: 규격서(본 문서), `CharacterView` 및 매핑/모션 훅 C# 스크립트, 폴더/네이밍 규약, 외형 데이터 정의(ScriptableObject/UnitData 확장).
- **Unity 에디터에서 사용자 작업**: Sprite Library 에셋, 5종 애니메이션 클립, `CharacterView` 프리팹 리깅/조립, 아트(Aseprite) 제작.
- Claude가 C# 스캐폴딩을 먼저 제공 → 에디터 조립 단계별 가이드 제공 가능.
