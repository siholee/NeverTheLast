# 상세 기획서 02 — 전투

> **3계층 문서.** 전장 구조, 행동 순서, 피해 공식, 보호막, 제어, 피해 태그, 상태, 이벤트 훅, 전투 연출.
> 원소는 [Detail_16](Detail_16_Elements.md). 개념 정의는 [서브 기획서](GDD_Sub_Concepts.md) 2장을 본다.

최종 갱신: **2026-10-14**
관련 코드: `GridManager.cs`, `ActionScheduler.cs`, `Unit.cs`, `Combat/AttackTargetRouter.cs`, `Combat/DamageMitigation.cs`, `Combat/Concentration.cs`, `Effects/Vfx/*`

> 옛 5스탯 · 포켓몬식 위력 · 내구 · 회피 · 횟수제 무적 · 한 방 상한 · 소환수와 전원 공용
> 특수행동 규칙은 개편으로 걷어냈다. 현행 SP는 특정 유닛만 가진 트리거형 슬롯이다.

---

## 1. 전장 구조

세븐나이츠식 **전열/후열** 구조다. 각 진영은 2열이고, **한 열에 4명**이 세로로 늘어선다.

```
   아군 후열   아군 전열        적 전열   적 후열
    (x=-2)     (x=-1)          (x=1)    (x=2)
      □          □      ⚔       □         □      y=1
      □          □              □         □      y=2
      □          □              □         □      y=3
      □          □              □         □      y=4

              대기석  □ □ □ □ □   (전투 미참여)
```

| 항목 | 값 | 코드 |
| --- | --- | --- |
| x 범위 | `[-2, 2]`, **`x = 0` 없음** | `GridManager.xMin/xMax` |
| y 범위 | `[1, 4]` | `GridManager.yMin/yMax` |
| 진영당 | **2열 × 4행 = 8칸** | |
| 전열 / 후열 | `x = ±1` / `x = ±2` | `GetFrontColumn()` / `GetRearColumn()` |
| 대기석 | **5칸** | `GridManager.benchSize` |

### 1.1 화면 배치 상수

| 상수 | 값 | 의미 |
| --- | --- | --- |
| `ColumnSpacing` | 13.2 | 같은 진영 두 열 사이. 돌진하는 카드가 옆 열을 스치지 않게 벌린다 |
| `BattleColumnMinGap` | 0.9 | 전투/기본 준비에서 같은 진영 인접 카드의 **실제 확대 폭** 사이에 보장하는 최소 틈 |
| `SlotSpacing` | 12.8 | 한 열 안 4명의 간격. 카드 + 아래 HUD의 세로 영역(12.2)보다 크다 |
| `CenterGap` | 7 | 양 진영 사이 추가 거리 |
| `BattleCenterGap` | 1 | 양 진영 전열 카드의 실제 폭 바깥에 남기는 중앙 틈 |
| `Cell.CardSize` | 칸(10.16) × 0.96 | 카드 한 변 |

### 1.2 배치 규칙

| 대상 | 규칙 |
| --- | --- |
| 아군 | 전열(−1) 우선, 가운데 행(y=2,3)부터 채운다 — `TryFindOpenAllyPosition` |
| 적 | `archetype` 7·8·9 → 전열(x=1), 그 외 → 후열(x=2). 각 열 최대 4 |

실제 출전 인원은 파티 상한(5명)에 묶인다. 대기석 유닛은 전투 판정에서 제외한다. 고정 파티 HUD에는 ‘대기석’으로 구분해 남긴다.

## 2. 행동 순서 — 턴제 + 행동치(AV)

**시간 축은 턴이다.** 벽시계는 연출(투사체 비행 · 카드 모션)에만 쓴다. 지속시간 · 주기 · 내부 쿨다운 ·
제어 · 집중은 전부 턴으로 센다. 초는 전투 판정 어디에도 남아 있지 않다.

### 2.1 턴의 정의

> **턴 = 그 효과를 들고 있는 유닛이 행동하는 것.**

| 대상 | 누구의 턴으로 세는가 |
| --- | --- |
| 버프 · 디버프 · 지속피해 · 원소 부착 | 상태가 붙어 있는 유닛 |
| 주기형 특성 | 특성 보유자 (`PeriodicTurnPassive`) |
| **집중** | **시전자** — [Detail_03 §4.5](Detail_03_Character.md) |

진행 지점은 `Unit.BeginTurn()` 한 곳이다.

### 2.2 AV — 다음 행동자 결정

```
AV = 10000 / 속도          (속도 = 최종 Speed 스탯, 작을수록 먼저 행동)
```

AV가 가장 낮은 유닛이 0에 닿을 만큼만 전원의 AV를 한 번에 깎고, 그 유닛이 행동한 뒤 자기 AV를 채운다.
동점이면 속도가 빠른 쪽이 먼저다.

**추월 제한.** A는 B보다 이번 라운드에 `floor(A속도 ÷ B속도)`번까지만 더 턴을 열 수 있다.
속도 2배 미만이면 번갈아 행동한다. 행동을 앞당기는 효과(`AdvanceAction`)는 앞당긴 비율만큼 추월 몫을 준다.

### 2.3 우선순위 — 한 번에 하나만

| 순위 | `ActionKind` | 행동 | 턴 |
| ---: | --- | --- | :-: |
| 0 | `PriorityAdditional` | 우선 추가행동 — 특성이 부른 것(세이의 빅뱅) | 안 쓴다 |
| 2 | `Additional` | 추가행동 | 안 쓴다 |
| 3 | `Coordinated` | 협동행동 — 🔸 enum만 남아 있고 쓰는 코드가 없다 | 안 쓴다 |
| 4 | `Special` | 특정 트리거가 연 특수행동 | 안 쓴다 |
| 5 | `Ultimate` | 궁극기 | 안 쓴다 |
| 6 | `Normal` | 일반행동 또는 대체행동 | 쓴다 |

0과 2는 성질이 같고 둘 다 `OnAdditionalActivates`를 발행한다(`ActionScheduler.IsAdditional`).
같은 `(유닛 + 종류 + 키)`는 큐에 하나만 존재한다.

**특수행동 묶음.** `Special`(4)은 `Combat.SpecialAction.OpenGate(opener, gateTag)`가 **유닛 태그**(`gateTag`)가 같은 참여자의
특수행동을 `SpecialCode.OpenOrder` 순으로 한꺼번에 예약한 것이다. 현재 묶음은 로카팔라 하나이고, 이를 여는 것은
**인드라의 초월기(팔방의 소집)뿐**이다(F1 디버그도 양측 궁극기를 채워 인드라가 스스로 쓰게 할 뿐 따로 열지 않는다). 표준 순서는 디버프 · 버프를 거는 참여자가 음수(바유 -100 · 바루나 -50),
피해가 주인 참여자가 0 이상(쿠베라 0 · 인드라 +50 · 야마 +75), 회복만 하는 참여자가 맨 끝(찬드라 +100)이다 —
[Detail_08 §13.5](Detail_08_Confirmed_Characters.md). 묶음 안의 임시 효과는 마지막 특수행동이 끝날 때 걷는다. 시의 처치 재행동 멸참처럼
묶음 없이 한 유닛이 스스로 예약하는 특수행동도 같은 `Special`이다. 행동이 끝나면 `ActionScheduler.AnyActionFinished`가 나간다.

**궁극기는 턴을 쓰지 않는다.** 마나가 차는 즉시 줄을 서고, 발동해도 AV가 리셋되지 않는다.

### 2.4 라운드 제한

라운드는 **전역 80턴**으로 끝난다(`GameManager.roundTurnLimit`). 추가행동 · 특성 발동은 세지 않는다.

### 2.5 궁극기 자원 — 행동 · 피격

마나 획득 규칙과 수치는 [Detail_03 §3](Detail_03_Character.md)에 한 벌만 둔다.
전투 시간에 비례하던 옛 충전(`INT × 전투 초`)은 없어졌다 — `ActionScheduler.ActionValuePerSecond`는
통계 표기에만 남았다.

전투가 끝나도 남은 마나는 다음 스테이지로 이어지고, 테마 라운드가 바뀔 때만 0이 된다. 단,
`ultimateResourceType: Stack`인 사바흐는 공용 행동·피격·처치 마나를 받지 않고 특성 이벤트만으로 아즈라엘을 채우며,
전투 밖 복원에서는 0으로 돌아간다. `Passive`인 바스테트는 자원을 소모하거나 자동 시전하지 않는다. HUD는 충전된
초월기와 같은 테두리와 가득 찬 내부를 상시 표시하고, 초월기 코드는 라운드 시작부터 종료·사망까지 구독형으로 동작한다.

### 2.6 어떤 코드에도 재사용 대기 시간이 없다

일반행동은 AV가, 궁극기는 마나가 주기를 정한다. 코드 안쪽 내부 쿨다운은 `Combat.TurnCooldown` /
`TargetTurnCooldown`으로 **턴**을 잰다.

### 2.7 직접 공격 대상 교체와 선제 개입

직접 공격은 원래 대상 목록을 모두 고른 직후 `AttackTargetRouter.Prepare`로 해석한다. 먼저 바스테트 경호처럼
대상을 교체하고, 이어 `ResolvePreAttack()`이 최종 대상의 선제 개입을 **동기적으로 끝까지 기다린다.** 이 두 단계는
`Targeting.Announce`, 행동 VFX, `TakeDamage`보다 앞이므로 공지·피격 연출·마나·반격이 모두 최종 대상과 일치한다.
일반행동은 `BaseNormalCode`, 초월기는 `BaseUltimateCode.PrepareTargets`, 특수행동은 같은 라우터를 직접 사용한다.

```text
원래 대상 목록 확정
→ AttackTargetRouter.Prepare(공격자, 전체 원래 대상, 코드 종류, 피해 태그)
→ 경호 등 대상 교체
→ ResolvePreAttack(최종 대상의 선제 개입을 동기 해결)
→ 공격자가 사망·제어되었으면 원래 공격 취소
→ Targeting.Announce(최종 대상)
→ 시전 1회 + 최종 대상별 피격 VFX
→ TakeDamage
```

바스테트 P322는 이 단계에서 바스테트 한 자리만 경호자로 바꾼다. 지속피해(`Effect`)·특성 간접 피해
(`PassiveAttack`)·원소 반응·덧붙는 피해(`BonusDamage`)는 직접 공격이 아니므로 교체하지 않는다. 추가행동처럼
스케줄러의 행동으로 실행되는 공격은 직접 공격이다. 다중 대상 목록에 이미 견고/반격 경호자가 있으면 대상 수나
중복 피격을 늘리지 않기 위해 교체하지 않는다. 다단 공격에서 교체된 경호자가 사망하면 잠근 대상이 사라진 것이므로
남은 타수는 취소한다.

잔 P207은 경호 교체까지 끝난 최종 대상이 잔일 때, 원래 공격보다 먼저 `PriorityAdditional` 자식 행동으로 N7을
한 번 실행한다. 같은 부모 행동·공격 키·잔 조합은 중복 처리하지 않는다. 반격 피해는
`NormalAttack + AdditionalAttack + CounterAttack + ContactAttack`이며, 반격 정점 뒤 공격자가 사망하거나 제어되면
원래 공격을 공지·연출·피해 전에 취소한다. 바스테트 U122 추격은 반격 피해를 감지하되 원래 행동이 끝나거나 취소된 뒤
예약 순서대로 실행하며, 선제 처치로 대상이 죽으면 만들지 않는다.

선제 개입에는 부모와 다른 행동 ID를 부여한다. 부모가 반격 정점에서 쓰러져도 자식의 연출과
출혈 등 완료 판정을 먼저 끝내고, 부모의 시작 ID로 부모 종료를 알린다. 그때까지 다음 행동과
행동서열 종료를 진행하지 않는다. 라운드가 종료되면 이전 개입의 실행 문맥은 복원하지 않는다.
대체행동 공지는 일반행동 카탈로그 이름 대신 해당 코드의 `AnnouncementName`을 사용한다.

---

## 3. 피해 계산

### 3.1 파이프라인

```
0) Unit.AnyDamageIncoming         (정적 이벤트 — 경감 · 보호막 전. 세이의 빅뱅이 여기서 센다)
1) OnBeforeDamageTaken            (자신, 공격자)
2) CalculateFinalDamage           경감 (아래 3.2)
3) OnTakingDamage → 보호막 흡수 → 체력 차감
4) 사망 방지 판정                   BaseEffect.TryPreventDeath
5) 피격 마나 +5                    (지속피해 · 덧붙는 피해 제외)
6) OnAfterDamageTaken / OnDamageDealt / Unit.AnyDamageDealt
7) 체력 0 이하 → Die()             파괴 연출 → 처치 마나 → 비활성화
```

`HpDamage`는 실제 잃은 양의 체력이며 초과 처치 피해를 포함하지 않는다. `ResolvedDamage`는
실제 보호막 감소량과 실제 체력 감소량의 합이다. 흡혈·피해 집계·실피해 연동 효과도 이 값을 사용한다.
사망 처리 중의 추가 피해·치유와 중복 사망 호출은 취소하며, 죽은 유닛에게 양의 보호막을 새로 부여하지 않는다.
살아 있는 유닛의 최대 체력을 재계산할 때는 현재 비율을 유지하되 반올림만으로 체력이 0이 되지 않는다.

### 3.2 공식

```
기본 피해   = 코드가 정한다 — 고정값 + 스탯 × 계수 (Code.StatDamage)
            대부분 공격력 계수. 극소수가 방어력 · 최대 체력 계수
치명타      = 확률 CritRate, 배율 1 + CritDamage
경감 스탯   = 방어력        (일반 피해)
            = 특수방어력    (지속피해 CodeType.Effect · 원소 반응 · SpecialDamage 태그)
            = 없음          (TrueDamage)
기준값 K    = 100 + 6 × (공격자 레벨 − 1)
경감 배율   = K / (K + 경감 스탯 × 방어배율)      경감 스탯 ≥ 0
            = 2 − K / (K − 경감 스탯 × 방어배율)  경감 스탯 < 0
최종 피해   = max(1, round(기본 피해 × 치명타 × 주는 피해 보정 × 경감 배율 × 받는 피해 보정))
```

| 요소 | 출처 |
| --- | --- |
| 경감 스탯 선택 | `DamageMitigation.TryGetDefenseStat` |
| K | `UnitStats.ArmorConstant(attackerLevel)` — 상수 `ArmorConstantBase` 100, `ArmorConstantPerLevel` 6 |
| 경감식 | `UnitStats.MitigationMultiplier` |
| 방어배율 | `DamageContext.DefenseStatMultiplier` × 대상의 `DefenseStatMultiplierModifier` 등 (방어 무시 · 약화) |
| 주는 / 받는 피해 보정 | `OutgoingDamageModifier` / `ReceivingDamageModifier` 누적곱 |

#### 왜 공격자 레벨의 K인가

무한 모드에서 레벨은 끝이 없고 모든 스탯은 선형으로 자란다. K를 100으로 묶으면 방어력만 커져 경감률이
100%로 수렴해 고레벨 전투가 끝나지 않는다. K를 표준 방어 성장과 같은 기울기로 키우면 **같은 레벨끼리의
경감률은 레벨과 무관하게 일정**하고, 레벨이 높은 공격자는 낮은 방어자를 더 잘 뚫는다(원신식).

| 방어력 / 레벨 비 | 같은 레벨 경감률 |
| --- | --- |
| 표준 성장(방어 +5/Lv) 방어자 | 약 45% |
| 세이 (85 + 6.5/Lv) | 약 46~52% |

### 3.3 덧붙는 피해

다른 피해에 반응해 따라붙는 피해(세이의 초월기 고정 피해)는 `DamageTag.BonusDamage`를 단다.
덧붙는 피해는 **다시 덧붙는 피해를 부르지 않고**, 피격 마나를 주지 않는다.

### 3.4 치명타

| 항목 | 값 |
| --- | --- |
| 확률 | `CritRate` 최종값 + 시전자 효과의 가산, 0~1로 자른다 |
| 배율 | `1 + CritDamage` (기본 5% / 50%) + 시전자 효과의 가산 |

굴림은 `Code.RollCritical(target, out mult)` 한 곳이다 — 대상 · 행동 종류(`CodeType`)를 알아야 하는 보상 코드의
치명타 효과를 한 번에 처리한다. 대상을 모르는 공유 판정(한 번 굴려 전원에게 쓰는 궁극기 등)은 `RollCritical(out mult)`가
대상 없이(`target = null`) 같은 경로를 탄다. 시전자의 효과(`BaseEffect`)는 아래 훅으로 끼어든다.

| 훅 | 뜻 | 쓰는 보상 코드 |
| --- | --- | --- |
| `CritChanceBonusAgainst(attacker, target, codeType)` | 확률 가산 (0.15 = +15%p) | 결전의 일격(궁극기에만 +15%p) |
| `CritDamageBonusAgainst(attacker, target, codeType)` | 피해 가산 | 화무십일홍(확률의 2배) |
| `ForcesCriticalAgainst(attacker, target, codeType)` | 확정 치명타 | 기습(체력 가득한 적) · 화무십일홍(체력 15% 미만) |
| `CritOverflowToDamageRatio(attacker)` | 100%를 넘는 확률을 치명타 피해로 환산 (1 = 1:1) | 암살자 |

### 3.5 효과 명중과 저항

2026-10-06 확정·구현. 적에게 거는 **원소 부착(재부착 포함)·지속피해·디버프·제어·중립 표식**은
시전자의 최종 특수공격력과 대상의 최종 특수방어력으로 판정한다.

```
명중 확률 = clamp01(기본 확률 × 2 × 특공 / (특공 + 특방))
기본 확률 = 0.75; 비교 스탯은 최소 1
```

| 특공 : 특방 | 기본 명중률 |
| --- | --- |
| 1 : 2 | 50% |
| 1 : 1 | 75% |
| 2 : 1 이상 | 100% |

- 아군·자기 자신에게 주는 효과는 효과 명중과 코드의 추가 상태 저항을 모두 건너뛴다. 출처 없는 시스템 부여도 확정이다.
- 실패해도 공격 피해는 들어간다. 카드의 `저항`은 부여만 실패했다는 뜻이며 피해 회피가 아니다.
- 실패한 재부착·갱신은 기존 원소·상태·남은 기간을 바꾸지 않는다. 부착 실패 시 반응·원소 소모·부착 성공 이벤트도 없다.
- **원소 부착 → 반응 상태**는 같은 부여라 특공/특방 판정을 한 번만 한다. 반응 제어·감전·화상·풍화·둔화·취약은 부착 성공을 상속한다.
  직접 부여하는 공통 화상·맹독·저주는 통상 기본 확률 75%를 사용한다.
- 확산은 새 수신자마다 명중을 판정한다. 기존 원소가 남았다는 이유만으로 성공 목록에 넣지 않는다.
  바유의 확산 취약과 보상 코드 712 원소 침식은 실제 부착 성공에서 파생하므로 특공/특방을 다시 굴리지 않는다.
- 성공 후 코드가 제공하는 **추가 상태 저항**은 별도로 작용한다. 동일 진영 부여에는 적용하지 않는다.
  야마 선고와 히미코 맹화 폭발은 이미 적용된 원본 상태에서 파생하므로 두 번째 관문을 만들지 않는다.
- 바유의 전 속성 취약은 대상당 상태 하나·판정 한 번이다. 속성별로 일곱 번 굴리지 않는다.
- 이미 붙은 지속피해의 자연 틱과 야마의 즉시 발동은 재명중하지 않으며 상태·기간을 소모하지 않는다.
- 실패한 제어는 기존 제어를 지우거나 제어 분쇄 횟수·제어 성공 이벤트를 올리지 않는다.

구현: `DamageMitigation` · `Unit.GrantCombatElement` · `UnitStatusController`.
검증: 실제 Unity `effect_hit_core`, 실패·갱신·진영·중립 지속피해·확산·제어·즉시 발동을 확인한다.

## 4. 보호막

| 규칙 | 내용 |
| --- | --- |
| 우선순위 | 체력보다 **먼저** 소모 |
| 우회 | `ShieldPenetration` 태그, 지속피해(`CodeType.Effect`)는 보호막을 무시하고 체력에 들어간다 |
| 소진 | `ShieldCurr`가 0이 되면 `ShieldMax`도 0 |
| 지속 | 라운드 내 유지, `OnRoundEnd`에 정리 |

## 5. 제어

행동만 막는 제어(빙결 · 에어본 · 기절 · 속박)는 모두 `ControlStatuses.ApplyControl`을 지난다.
지속 턴은 **특수공격력 ÷ 특수방어력** 비율로 정한다.

| 제어 | 지속 턴 | 범위 |
| --- | --- | --- |
| 빙결 | `round(2 × 특공/특방)` | 1~3 |
| 에어본 | `round(1.5 × 특공/특방)` | 1~2 |
| 기절 | `round(특공/특방)` | 1~2 |
| 고정 기절 | 코드가 정한 턴 (`ApplyFixedStun`) | — |

- 재부여하면 남은 턴과 새 턴 중 긴 쪽을 남긴다.
- **제어 분쇄** — 같은 대상에게 반복되면 100% → 60% → 30% → 면역. 제어 없이 자기 턴 3번이면 초기화.
- 제어가 걸리면 `OnControlStarts`가 나가고, 그 유닛이 건 **집중이 해제된다**(공허 분쇄자의 대지 분쇄로 검증).
- **제어 지속 훅** — 걸리는 쪽 효과의 `BaseEffect.ControlDurationModifier(unit)`이 위 지속 턴에 더해진다(음수면 줄인다).
  걸리기로 한 제어는 **최소 1턴**이 남는다(`ControlStatuses.ApplyControl`). 정신력(보상 코드)이 -1턴을 건다.

## 6. 피해 태그

`BaseClasses/DamageTags.cs`. 만 자리가 분류이며, 한 공격은 각 자리에서 하나씩 붙인다.

| 분류 | 태그 |
| --- | --- |
| 10000 대상 범위 | `SingleTarget` · `MultiTarget` · `AllTarget` |
| 20000 공격 종류 | `NormalAttack` · `UltAttack` · `SpecialAttack` · `AdditionalAttack` · `SummonAttack`(20006) · `CounterAttack` · `PassiveAttack` · `ElementalReaction` |
| 30000 접촉 | `ContactAttack` · `NonContactAttack` |
| 40000 관통 · 변환 | `ShieldPenetration` · `TrueDamage`(경감 없음) · `SpecialDamage`(특수방어력 경감) · `BonusDamage`(덧붙는 피해) |
| 50000 공격 형태 | `Slash`(베기 50001) · `Pierce`(찌르기 50002) · `Shot`(사격 50003) · `Strike`(타격 50004) · `Spell`(주문 50005) |

**50000번대는 무기가 아니라 공격의 생김새다.** 옛 무기 분류(50000)는 장비와 함께 지웠지만, "베기 공격 피해 +15%" 같은
보상 코드([Detail_17 §3.1](Detail_17_Reward_Codes.md))가 무엇을 키우는지 말하려고 같은 자리에 되살렸다. 공격 하나는 형태 하나를 가지며,
코드가 `Code.AttackForm`으로 선언한다(`Combat/AttackForms.cs`가 파티에 그 형태가 있는지 묻는다). 지속피해 · 원소 반응 · 덧붙는 피해는 형태가 없다.
접촉 · 비접촉(30000)과는 별개 축이고, 경감 스탯은 형태와 무관하다(`DamageMitigation.TryGetDefenseStat`).

**`SummonAttack`(20006)** 은 소환수가 가한 피해다. 공격자는 소환자로 기록하되(처치 · 마나 귀속) 소환수 피해 증가
(갈라테아 · 소환술사 · 쿠베라 후원)가 이 태그를 본다 — [Detail_03 §4.7](Detail_03_Character.md).

**지속피해에는 직접 공격 태그가 없다.** 감전·화상·풍화의 반응 분류 태그는 유지한다. 접촉 판정은 `ContactAttack`이 **있는지**를 묻는다.
카드 흔들림 연출도 `ContactAttack`에만 붙는다.

## 7. 상태(Status) 시스템

`UnitStatus` + `BaseEffect`. 같은 키 = 같은 상태이고, `StatusStackPolicy`가
Replace / ExtendDuration / Stack / ReplaceIfStronger / Ignore 중 하나를 고른다.

### 7.1 BaseEffect 훅

| 분류 | 훅 |
| --- | --- |
| 생명주기 | `OnApply` · `OnOwnerTurn` · `OnRemove` |
| 스탯 | `StatFlatModifier(stat)` · `StatPercentModifier(stat)` · `ManaRecoveryMultiplierModifier` |
| 피해 | `OutgoingDamageModifier` · `ReceivingDamageModifier(unit, ctx)` · `DefenseStatMultiplierModifier` · `OwnedDefenseStatMultiplierModifier` · `TryPreventDeath` |
| 상태 · 대상 | `NegativeStatusResistanceChanceModifier` · `TargetPriorityAdditiveModifier` · `BlocksNormalAttack` · `RoundStartActionAdjustment` |
| 회복 · 보호막 | 치유 · 보호막 배율 |
| 지속피해 | `IsDamageOverTime` · `EstimateDamagePerTurn` · `CreateDamageOverTimeTick`(원시 한 번분 피해 정보 — 자연 발동과 즉시 발동이 함께 쓰는 단일 입구, 상태를 바꾸지 않는다) |
| 치명타 | `CritChanceBonusAgainst` · `CritDamageBonusAgainst` · `ForcesCriticalAgainst` · `CritOverflowToDamageRatio` — §3.4 |
| 제어 · 상태 지속 | `ControlDurationModifier(unit)`(걸리는 제어 지속 가산, 최소 1턴 — §5) · `OutgoingStatusDurationModifier(caster, status)`(시전자가 **거는** 상태의 지속 가산 — 죽음의 계약의 지속피해 +1턴) |

공용 효과는 `Effects/Buffs/SharedModifierEffects.cs`에 있다 — `StatModifierEffect(stat, 값 또는 Func, percent)`,
`ReceivingDamageMultiplierEffect`, `OutgoingDamageMultiplierEffect`, `ArmorShredEffect`, `ManaEfficiencyEffect`.
한 줄짜리 무조건 보정을 새 이름으로 다시 선언하지 않는다.

감전·화상·저주·출혈의 틱 피해는 **생성자 특수공격력 × 계수**, 맹독은 **대상 최대 체력 × 계수**이며
특수방어력이 경감한다. 발동 시점·기간·계수는 [Detail_16](Detail_16_Elements.md)의 공통 지속피해 규격을 따른다.

**지속피해 즉시 발동** (`Combat.DamageOverTimeDetonation`, 야마 — [Detail_08 §15.3](Detail_08_Confirmed_Characters.md)). 대상의 모든 지속피해 효과를 스냅샷으로 한 번 읽어
`CreateDamageOverTimeTick()` × 비율을 `Unit.TakeDamage`에 넘긴다. 시전자 · `Effect` 종류 · 반응 태그는 그대로이고 직접 공격 태그를 붙이지 않으므로
원 시전자의 주는 피해 보정과 대상 경감이 자연 틱과 똑같이 한 번씩 걸린다. 지속시간 · 경과 턴 · 중첩은 건드리지 않고, 대상이 쓰러지면 남은 발동을 멈추며,
같은 대상의 발동 중 재진입은 막는다. 새 지속피해 효과는 `IsDamageOverTime`과 `CreateDamageOverTimeTick`만 재정의하면 즉시 발동에 참여한다.

**상태 증감 알림** (`Unit.AnyStatusChanged(보유자, 상태, 붙었는가)`). 상태가 새로 붙거나 떨어진 사실을 **바깥 호출(Add · Remove · TickTurn)이 끝난 뒤** 알린다 —
받는 쪽이 같은 유닛의 상태를 고쳐도 순회가 어긋나지 않는다. 지속 연장 · 라운드 종료의 일괄 정리는 알리지 않는다.
다른 해로운 상태에서 파생돼 원본이 남아 있는 동안 저절로 다시 붙는 오라(야마의 선고)는 `UnitStatus.SkipStatusResistanceRoll`로 상태 저항을 다시 굴리지 않는다.

틱 순회 도중 제거된 상태는 더 이상 틱하지 않는다. 전체 정리는 상태 목록을 먼저 분리하고 각 효과의
`OnRemove`를 한 번씩 실행하며, 정리 중 재진입·새 상태 부착을 막고 마지막에 스탯을 갱신한다.
기간 연장의 부여 알림에는 실제로 유지 중인 상태 객체를 전달한다.

### 7.2 상태 부여

```csharp
target.AddStatus(BuffStatus.Create(
    statusId, key, "버프 이름", Caster, target,
    new StatModifierEffect(BaseEnums.StatType.Attack, () => bonus),
    duration: 3));
```

카드에 오라를 그리려면 효과가 `ICardAura`(Glow · Ring)를 구현한다.

## 8. 유닛 이벤트 훅

`BaseEnums.UnitEventType` — `OnSpawn` · `OnDeath` · `OnControlStarts/Ends` · `OnPassive/Normal/Special/UltimateActivates` ·
`OnAdditionalActivates` · `OnTargeted`(맞기 전) · `OnNormalAttackHit` · `OnNormalActionResolved` ·
`OnBeforeDamageTaken` · `OnTakingDamage` · `OnAfterDamageTaken` · `OnDamageDealt` · `OnKill` ·
`OnTurnStart/End` · `OnStage/RoundStart/End`.

**정적 이벤트** (필드 전체를 듣는 특성용) — `Unit.AnyDamageIncoming`(경감 전) · `AnyDamageDealt` ·
`AnyUnitDied` · `AnyNegativeStatusGranted` · `AnyElementalReaction` · `AnyCombatElementGranted` ·
`AnyHealed`(대상 · 치유자 · 실제 회복량 · 최대 체력을 넘겨 버려진 양) · `AnyShieldGranted`(대상 · 부여자 · 보호막량) ·
`AnyPermanentStackGained`([Detail_03 §3.1](Detail_03_Character.md)). 소환수 생성은 `Combat.Summons.AnySummonCreated`,
행동 종료는 `ActionScheduler.AnyActionFinished(유닛, 종류)`다.
구독한 코드는 사망 · 라운드 종료에 반드시 구독을 끊는다.

## 9. 라운드 시작/종료

**시작** — 원소 부착 초기화 → 특성 활성화(`CastPassiveCode`).
**종료** — 상태 전부 제거(`OnRemove`로 리스너 해제) → 원소 · 보호막 · 고유 전투 자원 · 집중 초기화 →
아군 필드를 전투 시작 스냅샷으로 복원(마나는 유지).


## 10. 전투 표시

행동/카드/타격/지원 연출은 [07 UI·연출](Detail_07_UI_Tech.md)을 따른다.

## 11. 강인도와 처치 연계

오르페우스가 제공하는 적 전용 전투 자원이며, 보호막처럼 체력 피해를 흡수하지 않는다. 일반30·정예90·보스150. 모든 원소 공격이 삭감할 수 있다. 강인도를 가진 적의 체력이 남은 상태에서 0이 되면 격파하며 다음 일반행동 시작에 최대치로 회복한다. 추가·특수·초월기는 회복하지 않는다. **별도 격파 피해·행동 지연·제어·취약·잔향 없음.** 부여자 없는 적/소환수/아군에게 임의로 강인도를 추가하지 않는다.

| 공격 | 대상당 기본 삭감량 |
| --- | --- |
| 일반·특수 단일 | 30 |
| 일반·특수 광역/다중 | 15 |
| 초월 단일 | 60 |
| 초월 광역/다중 | 30 |
| 추가·반격·소환 단일 | 15 |
| 추가·반격·소환 광역/다중 | 10 |
| 오르페우스 일반 | 캐릭터 사양 참조 |

`기본 삭감량 × (1 + 삭감 효율 보너스 합계)`를 반올림한다. 체력/보호막에서 실제 피해가 발생한 공격만 센다. 무효화·지속피해·원소 반응·덧붙는 피해·특성 피해는 제외한다. 치명타와 실제 피해량은 삭감량을 바꾸지 않는다. 같은 스케줄러 행동의 다단은 대상·피해 명의별로 한 번만 삭감한다. 나폴레옹의 일제 사격은 각 참여자의 공격을 각각 센다. 보호막으로 막힌 공격도 실제 보호막 감소가 있으면 삭감한다.

격파는 `CombatDefeatContext.IsToughnessBreak=true`로 `Toughness.EnemyDefeated`와 공격자 `OnKill`·처치 마나를 발행한다. `Toughness.Broken`은 격파 전용 코드가 받는다. 실제 사망만 `Unit.AnyUnitDied`·`OnDeath`·사망 퇴장·골드·EXP·드랍·영혼·축전기를 발행한다. **체력도 같은 타격으로 0이 되면 실제 처치만 한 번 처리한다.** 부여자 사망/라운드 정리는 격파가 아니다. 잔향산가의 별도 고정피해로 실제 사망하면 그 별도 타격의 처치자를 기록한다.

구현은 `Combat/Toughness.cs`·`Entities/Unit.Toughness.cs`와 공통 피해/사망 입구다. 적 HUD 내부에 얇은 중성 강인도 선, 격파 시 테두리 없는 짧은 상태 알림을 제공한다. 강인도 없는 적에게 HUD 선을 보이지 않는다. 기간과 라운드 자원은 저장하지 않으며 준비 저장/복원은 기존 보상 코드 ID 규격을 그대로 따른다.

### 11.1 피격 가능한 동반형 소환수

토트의 서기관은 `SummonSpec.Targetable=true`. 일반·초월기 공통 적 대상 풀은 `CombatTargets.AliveEnemies`로 합쳐 본체와 반대 진영의 대상 가능한 소환수를 함께 조회한다. 칸이 없어도 `IsOnField` 및 우선도를 따른다. 기존 대상 불가 분신·펜리르는 후보에 들어가지 않는다. 아군 치유 대상은 스킬별로 명시한다. 소환수 사망은 전투 승패/적 골드 정산의 본체 수에 포함하지 않는다.

### 11.2 중단된 개입과 가상 처치

강인도 회복은 실제 일반행동의 시작 훅에서 진행한다. 행동불능으로 차례를 넘겼을 때는 회복하지 않는다.
초월기 피해 태그를 함께 가진 특수행동(인드라 바즈라 등)도 기본 삭감량은 특수행동의 단일30/광역15다.
선제 개입 역시 게임 시간8초의 연출 상한을 감시한다. 중단된 코루틴 때문에 완료 훅이 오지 않아도 자식과 부모의
종료 문맥을 정리하고 다음 행동으로 진행하며, 늦은 개입이 새 문맥을 복원하지 않는다. 강제 종료는 판정 코루틴도 중단한다.

**2026-10-14 치명상 방지 치유 집계:** 체력 차감은0을하한으로한다. 초과피해가음수체력으로남아
찬드라/복원 적의치명상방지회복을부풀리지않는다. 실제순수치유는복구한양의체력만기록한다.
초과피해집계·치명상방지·흡혈·온기저축은이같은체력기준을사용한다.

**2026-10-14 폐기 수명:** 비활성아군도라운드정리를받는다. 유닛/소환수폐기는파괴전에코루틴·상태·정적구독을해제한다.
소환수의실제일반행동완료에도공통지속피해기간시계를진행한다. 일반공격중시전자가사망/퇴장하면남은타격과
일반완료/사망후적중이벤트를발생시키지않는다. 임시최대체력버프정리후의체력을정산스냅샷에적어
비율을보존하고, 마나·영구성장은정리전기록으로이월한다.

## 12. 체력 비용과 실제 치유

체력 비용은 일반 적 피격·반격·지속피해 트리거와 구분한다. 수르트의 체력 비용과 프레이아의 실제 치유 저장은 [아군 사양](Detail_08_Confirmed_Characters.md)을 따른다. 순수 치유량은 실제 회복한 HP이며 초과 치유는 저장하지 않는다.
